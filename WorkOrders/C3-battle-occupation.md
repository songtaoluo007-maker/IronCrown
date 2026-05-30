# 工作单 C3 — 战斗与占领（多回合简化 HoI 风格 · 军事阶段第三步）

| 项 | 值 |
|---|---|
| 工作单号 | C3（玩家移动部队到敌方控制邻省 → 进入持续战斗状态 → 多回合 tick → 一方耗尽组织度则结束 → 守方败则攻方占领） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数据 | Claude（规则 13 + 人类确认"简化 HoI 多回合风格"；规则 14 人类终审） |
| 分支 | `feature/c3-battle-occupation`（从 main 切，main 须先合入 C2b） |
| 前置 | C2b 已审查通过；Play 截图 `Design/screenshots/c2b-*.png` 归档 |
| 角色边界 | 规则 12：只实现本单。**勿做** AI 主动进攻（C4）、战争状态/胜负终局（C4）、撤退寻路、增援、战斗宽度、将领/天气/补给修正、BattleResolver float→int 重构（单独债）、首都陷落投降；遇未覆盖点停 `[需 Claude 决策]` |

## 0. 目标与范围
玩家用 C2b 的 `MoveUnit` 命令点**敌方控制邻省**时：① 不立即解算；② 创建 `ActiveBattle`，攻守双方"卡在战场"无法被再次操作；③ 每回合 `Settlement` 阶段对每个活动战斗调用 `BattleResolver.ResolveBattle` 跑一帧伤害（沿用现有 float 公式 + ±20% 随机抖动）；④ 任一方 `organization ≤ 0` → 战斗结束：败方部队消灭（含同省未参战守方"清场"），守方败则攻方进省 + `controllerCountry` 改；双方齐崩则两方主力消灭、省不变。

**空城特例**：玩家点入无守方的敌方控制省 → **立即** 占领（不入战斗状态），与多回合分开走。

## Phase 0（C2b 收尾 + 起点）

### 0.1 C2b artifact / 截图归档
- 删除 `artifacts/c1-editmode.xml / c1-editmode3.xml / c1-editmode4.xml / c1-editmode-final.xml / c1-playmode.xml / c1-playmode2.xml / b15-*.xml / b2-*.xml / b3-*.xml`；**只保留** C1/C2a/C2b 三对权威 + 本单新产出。
- 确认 `Design/screenshots/c2b-select-unit.png` + `c2b-after-move.png` 已存在；缺则由人类 Play 时补，OpenClaw 提醒。

### 0.2 起点 & 卫生
- 从 main（C2b 合入后）切 `feature/c3-battle-occupation`；起点 EditMode + PlayMode 全绿。
- **改 Resolver/Service 构造签名时必须 grep 全工程 `new <ClassName>(` 同步所有调用点**（含测试）；提交 PR 前 Unity Editor Console **0 error**（USS 已知 2 条 `text-align` warning 可豁免）。
- 不直接编辑 `CHANGELOG.md / PROJECT_STATE.md / PROJECT_RULES.md / ARCHITECTURE.md`；改动摘要写 PR 描述。
- UTF-8；artifacts 命名 `artifacts/c3-editmode.xml` + `artifacts/c3-playmode.xml`。

## 1. 数据（Claude 代拟，规则 14 人类可调）

| 项 | 值 | 备注 |
|---|---|---|
| 战斗模型 | **多回合持续战斗**，每回合 Settlement 阶段 tick 一次 | 简化 HoI 风格 |
| 战斗 tick 内容 | 调既有 `BattleResolver.ResolveBattle(attacker, defender, province)` 跑一帧 | 含 ±20% 随机、地形系数（沿用现有 float 公式） |
| 战斗结束条件 | 任一方 `organization ≤ 0`（`IsShattered`）| 沿用 BattleResolver 现有判定 |
| 1v1 / 多兵 | **1v1**：InitiateAttack 时取守方 id 升序 [0] 为主战 defender；其他守方不参战 | 多兵种联合留 C4+ |
| 攻方胜处理 | 守方 [0] 死 → 清场该省其他非己方部队 → 攻方 currentProvinceId=target + controllerCountry=攻方 | "清场占领" |
| 攻方败处理 | 攻方部队消灭 → ActiveBattle 移除 → 省份不变 → 守方剩余部队留场 | 攻方"开赴战场未归" |
| 双方齐崩 | 双方主力消灭 → ActiveBattle 移除 → 省不变 → 未参战守方留场 | BattleResolver `draw` 字段 |
| 空城进驻 | 不入 ActiveBattle，立即 currentProvinceId=target + controllerCountry=攻方 | 与多回合分流 |
| 战斗中部队状态 | unit.currentProvinceId 保留原省（攻方未真进入敌省）；攻防双方均不能再下 MoveUnit | 等战斗结束才解锁 |
| movesLeft 扣减 | InitiateAttack 时扣 1（与 MoveUnit 一致）；战斗多回合期间不重复扣 | 投入战场即一次"移动" |
| 战斗超时 | **不做**（无超时；打到一方崩为止）| 防 BattleResolver 退化为零伤害死循环：本单 ResolveBattle 已确保至少 1 点伤害（既有 `Math.Max(1, ...)`）|
| 新 json 数值 | **零** | 一切沿用 BattleResolver 既有常量 + UnitConfig.attack/defense |

## 2. 架构决策（写死）

1. **新增 Domain 类型** `Domain/State/ActiveBattle.cs`：
   ```csharp
   public sealed class ActiveBattle {
       public string id;            // "{attackerUnitId}_vs_{defenderUnitId}"
       public string attackerUnitId;
       public string defenderUnitId;
       public string provinceId;
       public int turnsElapsed;     // 已 tick 次数，UI/debug 用
   }
   ```

2. **`WorldState` 加** `public List<ActiveBattle> activeBattles = new();`（确定性：按 id 升序操作）。

3. **`MovementResolver.TryMove` 保持纯和平**（target.controllerCountry == owner 才行）。

4. **`BattleResolver` 三个新方法**（既有 `ResolveBattle` / 公式 / `ApplyRandom` **不动**）：
   - `CommandResult InitiateAttack(WorldState world, string attackerUnitId, string targetProvinceId, string playerCountryId)`：
     - 校验链：unit 存在 / unit.owner==player / target 存在 / 邻接 / target.controllerCountry != owner / movesLeft >= 1 / **该 unit 未在 activeBattles** / **该 unit 未作为 defender 被锁在战斗**。失败 Rejected。
     - 取守方 = world.units.Values where currentProvinceId==target && owner!=attacker.owner OrderBy id Ordinal。
     - 守方空 → 立即占领（attacker.currentProvinceId=target / target.controllerCountry=attacker.owner / movesLeft -=1 / 发 `ProvinceOccupiedEvent`）→ 返回 Accepted。
     - 守方非空 → 取 [0] 作为 defender → 新建 ActiveBattle 加 world.activeBattles → movesLeft -= 1 → 发 `BattleInitiatedEvent`（新事件）→ 返回 Accepted。
   - `void TickBattles(WorldState world)`：
     - 遍历 world.activeBattles **拷贝**（按 id Ordinal 升序）以允许遍历中移除。
     - 每个 battle：取 attacker / defender / province，若任一不存在（防御性）→ 移除 battle。
     - 否则调 `ResolveBattle(attacker, defender, province)` 拿 `BattleResult`（既有方法，会发 `BattleResolvedEvent`、扣双方伤害、设 attackerWon/defenderWon/draw）。
     - battle.turnsElapsed++。
     - 按 result 收尾：见 §2.5。
   - `(注：原 ResolveBattle 既有方法保持原签名)`

5. **TickBattles 收尾分支**（C3 战斗结束逻辑）：
   ```
   result.attackerWon  → DestroyUnit(defender, "battle")
                         + 清场该省其他非己方部队 DestroyUnit(_, "occupation")
                         + attacker.currentProvinceId = battle.provinceId
                         + target.controllerCountry = attacker.ownerCountry
                         + activeBattles.Remove(battle)
                         + 发 ProvinceOccupiedEvent + BattleConcludedEvent
   result.defenderWon  → DestroyUnit(attacker, "battle")
                         + activeBattles.Remove(battle)
                         + 发 BattleConcludedEvent
   result.draw          → DestroyUnit(attacker, "battle")
                         + DestroyUnit(defender, "battle")
                         + activeBattles.Remove(battle)
                         + 发 BattleConcludedEvent
   else (双方都未崩)    → 不动 activeBattles，继续下回合
   ```
   `DestroyUnit` 私有方法：`world.units.Remove(id) + country.unitIds.Remove(id) + 发 UnitDestroyedEvent`。

6. **`TurnResolver.ExecuteTurn` 接线**：在既有结算次序后段加 `_battle.TickBattles(world)`，**放在 Settlement 概念位置**（即所有 Resolver 已跑完后、回合结束前）。**严格位置写死**：`MovementResolver.ResetMovement` → `EconomyResolver` → `UnitProductionResolver` → `ConstructionResolver` → **`BattleResolver.TickBattles`**。

7. **`GameSessionService.IssueCommand(MoveUnit)` 分流**：
   ```
   var unit = world.units[cmd.unitId];                 // 加 NullCheck
   var target = world.provinces[cmd.targetProvinceId]; // 加 NullCheck
   if (target.controllerCountry == unit.ownerCountry)
       → _movement.TryMove(...)
   else
       → _battle.InitiateAttack(...)
   ```
   **若 unit 已在 activeBattles**（攻或守一侧）→ MoveUnit 命令直接 Rejected "部队正在战斗中"（在 IssueCommand 入口先查；不要让 MovementResolver 和 BattleResolver 各查一遍）。

8. **地图主色按 `controllerCountry`**（不是 ownerCountry）：`ReadModelBuilder.BuildProvinceView` 中 `colorMap.TryGetValue(p.controllerCountry, ...)` fallback ownerCountry。占领后地图立即变色。

9. **存档完整性**：`WorldState.activeBattles` 必须进存档；`controllerCountry` 已在 ProvinceSaveData。新增 `ActiveBattleSaveData` + `GameState.activeBattles[]` + SaveMapper 双向。**HashWorld 必须含 activeBattles**（按 id 升序写 attackerUnitId/defenderUnitId/provinceId/turnsElapsed）。

10. 规则 4：UI 只读 ReadModel；规则 3：战斗逻辑全在 Simulation；规则 8：不新建平行战斗管线；规则 9：**不重构 BattleResolver float 公式**（列入技术债单独清）。

## 3. 实现规格

### 3.1 Contracts
- `Contracts/Events/BattleInitiatedEvent.cs`（新）：`string battleId; string attackerUnitId; string defenderUnitId; string provinceId;`
- `Contracts/Events/BattleConcludedEvent.cs`（新）：`string battleId; string provinceId; string winnerKind; // "Attacker"|"Defender"|"Draw" string attackerUnitId; string defenderUnitId; int turnsElapsed;`
- `Contracts/Events/UnitDestroyedEvent.cs`（新）：`string unitId; string ownerCountry; string provinceId; string cause; // "battle"|"occupation"`
- `Contracts/Events/ProvinceOccupiedEvent.cs`（新）：`string provinceId; string newControllerCountry; string previousControllerCountry; string attackerUnitId;`
- `Contracts/BattleResolvedEvent.cs`：**已有**，沿用（每 tick 发一次）。
- `Contracts/ReadModels/ProvinceView.cs`：加 `string controllerCountry; bool isOccupied; bool hasActiveBattle;`（`isOccupied = controllerCountry != ownerCountry`；`hasActiveBattle = world.activeBattles 含 provinceId==id`）。
- `Contracts/ReadModels/UnitView.cs`：加 `bool isInBattle;`（unit.id 出现在 activeBattles 任一侧）。
- `Contracts/ReadModels/WorldView.cs`：加 `List<ActiveBattleView> activeBattles;`。
- `Contracts/ReadModels/ActiveBattleView.cs`（新）：`string id; string attackerUnitId; string defenderUnitId; string provinceId; int turnsElapsed; int attackerOrg; int attackerMaxOrg; int defenderOrg; int defenderMaxOrg;` —— UI 详情用。

### 3.2 Domain
- `Domain/State/ActiveBattle.cs`（新）：见 §2.1。
- `Domain/State/WorldState.cs`：加 `activeBattles` list。

### 3.3 Simulation
- `Simulation/BattleResolver.cs`：加 `InitiateAttack` + `TickBattles` + 私有 `DestroyUnit`；不动既有 `ResolveBattle / CalculateAttack / CalculateDefense / CalculateArmorModifier / GetTerrainDefenseMultiplier / GetSupplyModifier / ApplyRandom`。

### 3.4 Application
- `Application/Services/GameSessionService.cs`：注入 `BattleResolver`；`IssueCommand` 按 §2.7 分流 + 入口加"部队是否在战斗中"统一拒绝。
- `Application/Queries/ReadModelBuilder.cs`：
  - `BuildProvinceView`：按 controllerCountry 取色 + 新字段；`hasActiveBattle` 来自 `world.activeBattles.Any(b => b.provinceId == p.id)`。
  - `BuildUnitView`：`isInBattle = world.activeBattles.Any(b => b.attackerUnitId == u.id || b.defenderUnitId == u.id)`。
  - `BuildWorldView`：拼装 `activeBattles` 列表（按 id Ordinal 升序），每项含双方当前 org / maxOrg。
- `Application/Persistence/SaveModels.cs`：
  - `GameState` 加 `ActiveBattleSaveData[] activeBattles;`
  - 新增 `[Serializable] class ActiveBattleSaveData { public string id; public string attackerUnitId; public string defenderUnitId; public string provinceId; public int turnsElapsed; }`
- `Application/Mapping/SaveMapper.cs`：`ToSave` / `ToRuntime` 双向 activeBattles。

### 3.5 Simulation — `TurnResolver`
- 注入 `BattleResolver battle`；ExecuteTurn 在既有结算尾端调 `_battle.TickBattles(world)`。

### 3.6 Bootstrap
- `Bootstrap/GameLifetimeScope.cs`：`BattleResolver` 已注册（B1 起）；确认它注入到 `GameSessionService`+`TurnResolver`；改构造签名时**全工程 grep 同步**。

### 3.7 Presentation — `MainHudController`
- **地图块颜色** 已变（ReadModelBuilder 改 controllerCountry 取色）。
- **战斗中省份**：tile 加 USS class `province-tile-in-battle`（红色脉冲边框 + 中央 `⚔战`图标 Label）。`hasActiveBattle == true` 时挂。
- **进攻目标高亮**：邻接敌方控制省 → `.province-tile-attack-target`（红边框）；邻接己方 → C2b 的 `.province-tile-move-target`（绿边框）。
- **战斗中部队**：选中 selectedUnitId 若 isInBattle=true → 状态栏 `{unitId} 正在 {provinceId} 战场（{turnsElapsed} 回合）`；不可下 MoveUnit。
- **详情栏**：选中省 hasActiveBattle 时显 `战斗中: {attackerUnitId}({attackerOrg}/{attackerMaxOrg}) vs {defenderUnitId}({defenderOrg}/{defenderMaxOrg}) — {turnsElapsed} 回合`；被占领省（isOccupied）显 `法理: {ownerCountry} / 控制: {controllerCountry}`。
- **事件订阅**（状态栏文字）：
  - `BattleInitiatedEvent` → `⚔ {attackerId} 开赴 {省名} 战场`
  - `BattleResolvedEvent` → 不在状态栏显（每 tick 都发太吵）；详情栏已显示双方 org。
  - `BattleConcludedEvent` winnerKind == "Attacker" → `⚔ 占领 {省名}！` ；"Defender" → `⚔ 攻势受挫：{attackerId} 阵亡`；"Draw" → `⚔ 两败俱伤`
  - `ProvinceOccupiedEvent` → 已被 BattleConcluded 覆盖文案，但**空城进驻**走该事件 → `旗 进驻 {省名}（无抵抗）`

### 3.8 UXML/USS
- `Assets/UI/MainHud.uss`：
  ```css
  .province-tile-attack-target { border-color: rgba(220, 80, 80, 0.95); border-width: 3px; }
  .province-tile-in-battle { border-color: rgba(255, 60, 60, 1); border-width: 4px; }
  /* 简化"脉冲"为静态强红边即可，动画后续美术阶段加 */
  ```
- UXML 本单不改。

## 4. 测试

### EditMode — `BattleResolverInitiateAttackTests`（新文件）
- `InitiateAttack_EmptyProvince_OccupiesImmediately_NoActiveBattle`：守方空 → 攻方进省 + 立即占领 + activeBattles.Count == 0 + 发 ProvinceOccupiedEvent。
- `InitiateAttack_EnemyDefender_CreatesActiveBattle`：1 守方 → activeBattles +1、ActiveBattle.attackerUnitId/defenderUnitId/provinceId/turnsElapsed=0 + 攻方 currentProvinceId 不变 + movesLeft -=1 + 发 BattleInitiatedEvent。
- `InitiateAttack_MultipleDefenders_PicksLowestId`：3 守方 id="z","a","m" → defenderUnitId == "a"。
- `InitiateAttack_NotNeighbor_Rejects`：非邻 → rejected "非邻接省份"、activeBattles 不变。
- `InitiateAttack_NoMovesLeft_Rejects`：movesLeft=0 → rejected "移动力不足"。
- `InitiateAttack_FriendlyTarget_Rejects`：controllerCountry==owner → rejected "非敌方控制省份"（防御性）。
- `InitiateAttack_NotOwnedByPlayer_Rejects`：owner != player → rejected "非己方部队"。
- `InitiateAttack_UnitAlreadyInBattle_Rejects`：已在 activeBattles → rejected "部队正在战斗中"。

### EditMode — `BattleResolverTickBattlesTests`（新文件）
- `TickBattles_NoActiveBattles_Noop`：空 list → 无副作用。
- `TickBattles_OneTickBothSurvive_TurnsElapsedIncrements`：构造 attacker/defender 都强 → 一次 tick 后双方 org 都 >0、activeBattles 仍 1 个、turnsElapsed=1。
- `TickBattles_AttackerKilledOverMultipleTurns_AttackerLosesProvinceUnchanged`：构造弱 attacker（org=20、低 attack）vs 强 defender（高 defense）→ 跑 N 次 tick 直到 attacker.IsShattered → world.units 不含 attackerId、activeBattles 移除、province.controllerCountry 不变、发 BattleConcludedEvent winnerKind="Defender"。
- `TickBattles_DefenderKilledOverMultipleTurns_AttackerOccupies`：反向构造 → defender shattered → world.units 不含 defenderId、attacker.currentProvinceId == target、target.controllerCountry == attacker.owner、activeBattles 移除、发 ProvinceOccupiedEvent + BattleConcludedEvent winnerKind="Attacker"。
- `TickBattles_AttackerWins_ClearsAllDefendersInProvince`：target 含 3 支同方守军 → 主战胜利后 3 支全消灭、各发 UnitDestroyedEvent（cause: 1 个 "battle"、2 个 "occupation"）。
- `TickBattles_Draw_BothMainsDestroyedProvinceUnchanged`：双方差不多 → 某 tick draw → 双方主力死、其他守方留场、controllerCountry 不变。
- `TickBattles_DeterministicOrder`：3 个 activeBattle 乱序加入 → tick 内部按 id 升序处理（用日志或事件发布顺序验证）。

### EditMode — `GameSessionServiceTests`（追加）
- `IssueCommand_MoveUnit_ToEnemyProvince_InitiatesBattle`：邻接敌省 → accepted、activeBattles +1。
- `IssueCommand_MoveUnit_UnitInBattle_Rejects`：部队已在 activeBattles → rejected "部队正在战斗中"。
- `BattleResolution_MultiTurnAdvance_ResultObserved`：弱 attacker 发动战斗 → 推 N 回合（每回合 TurnResolver.ExecuteTurn 调 TickBattles）→ 最终 view.activeBattles 空 + view.provinces 控制者按结果。

### EditMode — `ReadModelBuilderTests`（追加）
- `BuildProvinceView_OccupiedProvince_UsesControllerColor`。
- `BuildProvinceView_HasActiveBattle_Flagged`。
- `BuildUnitView_IsInBattle_TrueWhenInActiveBattles`。
- `BuildWorldView_ActiveBattles_PopulatedAndSortedById`。

### EditMode — `SaveLoadEquivalenceTests`（追加）
- `SaveLoad_ActiveBattle_Preserved`：发起战斗 → 1 tick → 存 → 读 → hash 等价 + activeBattles.Count==1 + turnsElapsed==1 + 双方 org 一致。
- `SaveLoad_OccupiedProvince_Preserved`：战斗结束占领 → 存 → 读 → controllerCountry 持久 + world.units 已删 defender。
- 既有 `SaveLoad_*` 系列 + `HashWorld` 扩展含 activeBattles（按 id 升序写 id/attackerUnitId/defenderUnitId/provinceId/turnsElapsed）。

### PlayMode — `MvpSmokeTests`（追加 1 用例）
- `Battle_MultiTurnTick_AttackerOccupiesEventually`：玩家发起战斗 → 多次 ClickButton("advance-btn") 推回合 → 最终 map 上目标省 tile 颜色变玩家国 mapColor、徽章变玩家方驻军、状态栏含"占领"。**注意 flaky 风险**：构造强 attacker（如在 BuildWorldWithProvinces 测试分支临时 +baseAttack 100）确保攻方必胜，避免随机抖动让测试不稳定。

## 5. 文件清单
| 动作 | 路径 |
|---|---|
| 新增 | `Domain/State/ActiveBattle.cs` |
| 改 | `Domain/State/WorldState.cs`（+activeBattles list） |
| 改 | `Simulation/BattleResolver.cs`（+InitiateAttack +TickBattles +DestroyUnit；不动既有公式） |
| 改 | `Simulation/TurnResolver.cs`（注入 BattleResolver + ExecuteTurn 尾段调 TickBattles） |
| 新增 | `Contracts/Events/BattleInitiatedEvent.cs`、`BattleConcludedEvent.cs`、`UnitDestroyedEvent.cs`、`ProvinceOccupiedEvent.cs`、`Contracts/ReadModels/ActiveBattleView.cs` |
| 改 | `Contracts/ReadModels/ProvinceView.cs`（+controllerCountry / +isOccupied / +hasActiveBattle）、`UnitView.cs`（+isInBattle）、`WorldView.cs`（+activeBattles list） |
| 改 | `Application/Services/GameSessionService.cs`（注入 BattleResolver + IssueCommand 分流 + 战斗锁定校验） |
| 改 | `Application/Queries/ReadModelBuilder.cs`（按 controllerCountry 取色 + 新字段 + ActiveBattleView 拼装） |
| 改 | `Application/Persistence/SaveModels.cs`（GameState +activeBattles[] / +ActiveBattleSaveData）、`Application/Mapping/SaveMapper.cs`（双向 + HashWorld 扩展） |
| 改 | `Bootstrap/GameLifetimeScope.cs`（确认 BattleResolver 注入；改构造签名同步 grep） |
| 改 | `Presentation/MainHudController.cs`（攻击高亮 + 战斗中标记 + 详情栏战况 + 事件订阅状态栏） |
| 改 | `Assets/UI/MainHud.uss`（+ `.province-tile-attack-target` + `.province-tile-in-battle`） |
| 新增/改 | 上述测试文件 |
| 清理 | C2b 残留中间 artifact（详见 Phase 0.1） |

## 6. 验收门禁（DoD）
- [ ] Phase 0 起点全绿；分支干净；artifacts 命名规范。
- [ ] EditMode 全绿：C2b 终态 + 本单新增（约 24 个：Initiate 8 + Tick 7 + Session 3 + ReadModel 4 + SaveLoad 2）。
- [ ] PlayMode 全绿：C2b 终态 7/7 + 本单 +1 = 8/8。
- [ ] Play `Main`：选玩家国 → 选首都部队 → 邻接己方省**绿**、敌方省**红** → 点红省 → 状态栏"开赴战场"+ 该省加 `⚔战` 中央图标 + 详情显双方 org → 推几回合 → 最终状态栏出"占领"或"攻势受挫"+ 地图色按结果（**截图为证**：`Design/screenshots/c3-battle-initiated.png` 进入战斗、`c3-battle-tick.png` 战斗中详情、`c3-after-occupy.png` 占领后）。
- [ ] 续跑等价：战斗中 1 tick → 存 → 读 → 继续推 → 与不存档直跑等价（hash 一致）；占领后 → 存 → 读 → controllerCountry/units 一致。
- [ ] 规则 4 守住；零新 json 数值；未做 AI 进攻/战争状态/撤退/地形重构；未改 BattleResolver 既有 `ResolveBattle / CalculateAttack / CalculateDefense / CalculateArmorModifier / GetTerrainDefenseMultiplier / GetSupplyModifier / ApplyRandom` 任一签名/常量；未改 EconomyResolver / PoliticsResolver / AIResolver / ConstructionResolver / UnitProductionResolver / MovementResolver / SupplyResolver。
- [ ] batchmode 0 error（含 Unity Editor Console）；`artifacts/c3-editmode.xml` + `artifacts/c3-playmode.xml`；PR 在 `feature/c3-battle-occupation`；CHANGELOG 写 PR 描述。
- [ ] commit diff 不含 `PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md / economy.json / units.json`。

## 7. 歧义处理
- BattleResolver float 公式 / 地形系数 / 随机抖动幅度 → **本单不动**，列入技术债"BattleResolver float→int 重构"。
- 战斗中部队的 UI 显示（"⚔战"图标位置/颜色脉冲）→ 最小清晰实现，截图为准。
- 占领后 resistance / 经济产出归属 / VP 转移 / 民心 / 抵抗事件 → **本单不做**，留 C4+。
- 多兵种联合 / 多攻方协同 / 增援 / 撤退寻路 → **本单不做**。
- 战斗超时 / 战斗自动结束（防 BattleResolver 出现零伤害死循环）→ `ResolveBattle` 内部 `Math.Max(1, ...)` 已保证至少 1 点伤害；本单**不需要**额外超时。
- 玩家试图操作"战斗中部队"→ IssueCommand 入口直接 Rejected "部队正在战斗中"（不调 MovementResolver/BattleResolver，避免重复校验）。
- 改 `GameSessionService` 构造签名时**务必** grep 全工程 `new GameSessionService(` + `new BattleResolver(` + `new TurnResolver(` 全部同步；提交前 Unity Editor Console 必须 0 error。
- reason 字符串与 MovementResolver 严格一致（"非邻接省份" / "移动力不足" / "非己方部队" / "目标省份不存在" / "非敌方控制省份" / "部队正在战斗中"），测试用 `Assert.AreEqual` 严格匹配。
- 严禁在本单"顺手"做 AI 进攻 / 战争状态 / 撤退 / 重构 BattleResolver 公式 / 增援 / 多兵种联合；如发现既有 bug 单独写 `[需 Claude 决策]` 报告。

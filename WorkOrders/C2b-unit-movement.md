# 工作单 C2b — 单步邻接移动（军事阶段第二步 · 部分二）

| 项 | 值 |
|---|---|
| 工作单号 | C2b（玩家可移动己方部队到邻接己方控制省份，消耗 movesLeft） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数据 | Claude（规则 13；移动模型按人类已锁定决策"单步邻接 + movesLeft"出单；规则 14 人类终审） |
| 分支 | `feature/c2b-unit-movement`（从 main 切，main 须先合入 C2a） |
| 前置 | C2a 已审查通过（造兵闭环达成，PR 截图：`Design/screenshots/c2a-build-completed.png`） |
| 角色边界 | 规则 12：只实现本单（玩家移动）。**勿做**战斗/占领（C3）、AI 移动（C4）、扩省份、多步路径规划、敌方/中立省进入；遇未覆盖点停 `[需 Claude 决策]` |

## 0. 目标与范围
玩家可点选地图上自己拥有的部队，再点**邻接的己方控制省份**完成单步移动，扣 1 点 `movesLeft`；每回合开始时所有部队 `movesLeft` 重置为 `unit.speed`（infantry=3，故玩家一回合内可连走 3 步）。**不做**：进入敌方/中立省、战斗、AI 部队移动、寻路、扩省份、撤回。

## Phase 0（起点 & 卫生）
- 新建分支 `feature/c2b-unit-movement`，**从 main**（C2a 合入后）切；起点跑 EditMode + PlayMode 同 C2a 终态全绿。
- 不直接编辑 `CHANGELOG.md` / `PROJECT_STATE.md` / `PROJECT_RULES.md` / `ARCHITECTURE.md`（C1/C2a 都吃过这亏，本单务必干净），改动摘要写 PR 描述。
- `economy.json` 本单**不动**（无新数值；speed 取自 `units.json.infantry.speed=3`，已存在）。
- 文件 UTF-8（无 BOM）。
- artifacts 命名：`artifacts/c2b-editmode.xml` + `artifacts/c2b-playmode.xml`（不带 -final / 数字后缀）。

## 1. 数据
本单**零新数值**。移动速度全部取自 `UnitConfig.speed`（infantry 模板已含 `speed:3`）；邻接关系全部取自 `ProvinceState.neighbors`（C1 已建）。**严禁**给移动编造未在 config 的常量（如"每步消耗 2 点"之类）。

## 2. 架构决策（写死）

1. **新增独立 Resolver** `Simulation/MovementResolver.cs`。理由：职责分离，C3 战斗会复用其校验链。
2. **MoveUnit 命令** 通过 `GameCommand`（加 `unitId`, `targetProvinceId`）走 `GameSessionService.IssueCommand` → `MovementResolver.TryMove(world, unitId, targetProvinceId, playerCountryId)`。
3. **校验链**（按此顺序，前面失败就返回，确定）：
   1. unit 存在；
   2. unit.ownerCountry == playerCountryId（只能动自己的）；
   3. targetProvince 存在；
   4. targetProvince.id ∈ unit.currentProvinceState.neighbors（必须邻接）；
   5. targetProvince.controllerCountry == unit.ownerCountry（**本单仅己方控制省**，敌方/中立 C3 才打仗）；
   6. unit.movesLeft >= 1（移动力足）。
   失败返回 `CommandResult.Rejected("..."")`；成功：`unit.currentProvinceId = targetProvinceId; unit.movesLeft -= 1;` 发 `UnitMovedEvent`。
4. **回合开始重置移动力**：`MovementResolver.ResetMovement(world)`——遍历 `world.units.Values` 按 id 升序，每个 `u.movesLeft = u.speed`。在 `TurnResolver.ExecuteTurn` **最开头**调（早于 EconomyResolver），保证本回合玩家命令前 movesLeft 已就绪。
5. **不限游戏阶段**——玩家在 TurnStart/InternalAffairs/Military/Diplomacy 任一阶段都能下 MoveUnit（与 BuildCivilianFactory/BuildUnit 一致；阶段限制留待 C4）。
6. **SelectUnit 不是命令、是 UI 状态**——`GameSessionService.SelectUnit(string unitId)` 直写 `WorldState.selectedUnitId`（仿 SelectProvince），不走 IssueCommand、不进 CommandResult。
7. **存档**：`movesLeft` 已在 C2a 决策 A 进了 `UnitSaveData`；本单**不**改 SaveModels/SaveMapper 结构（仅验证续跑等价不退化）。
8. **同省多支部队**：ProvinceView 新增 `garrisonUnitIds: string[]`（按 unit.id 升序），UI 由此循环选择；本单不做"展开列表"，只让 UI 能枚举。
9. 规则 4：UI 只读 `WorldView.units` + `selectedUnitId` + `ProvinceView.garrisonUnitIds`；规则 3：移动校验全在 Simulation 层；规则 8：不新建平行移动管线（如有 BattleResolver 里类似校验请整理到 MovementResolver 复用，但**不要改 BattleResolver 现有 stub**——它 C3 才动）。

## 3. 实现规格

### 3.1 Contracts
- `Contracts/Commands/CommandType.cs`：加 `MoveUnit`。
- `Contracts/Commands/GameCommand.cs`：加 `public string unitId; public string targetProvinceId;`（仅 MoveUnit 用）。
- `Contracts/ReadModels/UnitView.cs`（新增）：
  ```csharp
  public sealed class UnitView {
      public string id;
      public string unitType;
      public string ownerCountry;
      public string currentProvinceId;
      public int manpower;
      public int maxManpower;
      public int organization;
      public int maxOrganization;
      public int movesLeft;
      public int speed;
  }
  ```
- `Contracts/ReadModels/WorldView.cs`：加 `public List<UnitView> units; public string selectedUnitId;`。
- `Contracts/ReadModels/ProvinceView.cs`：加 `public string[] garrisonUnitIds;`（按 id 升序）。
- `Contracts/Events/UnitMovedEvent.cs`（新增）：`public string unitId; public string fromProvinceId; public string toProvinceId; public int movesLeftAfter;`。

### 3.2 Domain
- `Domain/State/WorldState.cs`：加 `public string selectedUnitId;`。

### 3.3 Application
- `Application/Services/GameSessionService.cs`：
  - `IssueCommand` 加 `case CommandType.MoveUnit:` → 调 `_movement.TryMove(_world, cmd.unitId, cmd.targetProvinceId, _world.playerCountryId)`。
  - 新方法 `public void SelectUnit(string unitId)`：校验 unitId 存在或 null（仿 SelectProvince），写 `_world.selectedUnitId`。
- `Application/Queries/ReadModelBuilder.cs`：
  - `BuildWorldView` 加 `units = world.units.Values.OrderBy(u => u.id, Ordinal).Select(BuildUnitView).ToList(); selectedUnitId = world.selectedUnitId;`。
  - 既有 units 排序工作（C2a Phase 0.3）保留；BuildProvinceView 增 `garrisonUnitIds = sortedUnits.Where(u => u.currentProvinceId == p.id).Select(u => u.id).ToArray();`。
  - 新方法 `BuildUnitView(UnitState u)`：字段直接映射。
- `Application/Persistence/SaveModels.cs`：`GameState` 加 `public string selectedUnitId;`（同 `selectedProvinceId` 已经在做的话——若没在做，加 `selectedProvinceId` 也是这次顺补）。
- `Application/Mapping/SaveMapper.cs`：`ToSave`/`ToRuntime` 把 `selectedUnitId` 加进去（如 `selectedProvinceId` 已有则照葫芦画瓢；如没有，本单**仅顺补 selectedUnitId 不动 selectedProvinceId**，避免越界）。

### 3.4 Simulation — `MovementResolver`（新文件）

```csharp
namespace IronCrown.Simulation {
  public sealed class MovementResolver {
    public CommandResult TryMove(WorldState world, string unitId, string targetProvinceId, string playerCountryId) {
      if (!world.units.TryGetValue(unitId, out var unit)) return Rejected("部队不存在");
      if (unit.ownerCountry != playerCountryId) return Rejected("非己方部队");
      if (!world.provinces.TryGetValue(targetProvinceId, out var target)) return Rejected("目标省份不存在");
      if (!world.provinces.TryGetValue(unit.currentProvinceId, out var current)) return Rejected("当前省份不存在");
      if (current.neighbors == null || !current.neighbors.Contains(targetProvinceId)) return Rejected("非邻接省份");
      if (target.controllerCountry != unit.ownerCountry) return Rejected("非己方控制省份");
      if (unit.movesLeft < 1) return Rejected("移动力不足");

      string from = unit.currentProvinceId;
      unit.currentProvinceId = targetProvinceId;
      unit.movesLeft -= 1;
      return Accepted(); // 调用方负责发 UnitMovedEvent
    }

    public void ResetMovement(WorldState world) {
      foreach (var u in world.units.Values.OrderBy(u => u.id, StringComparer.Ordinal))
        u.movesLeft = u.speed;
    }
  }
}
```
- `CommandResult.Accepted()` / `Rejected(reason)` 复用现有静态方法。
- 事件发布在 `GameSessionService.IssueCommand` 的 `MoveUnit` 分支里——TryMove 成功后 `_events.Publish(new UnitMovedEvent {...})`，把 fromProvinceId（移动前的）+ unit 的最新值带上。

### 3.5 Simulation — `TurnResolver`
- 注入 `MovementResolver movement`。
- `ExecuteTurn(world)` 最开头加 `_movement.ResetMovement(world);`，**优先于** EconomyResolver。
- 既有结算顺序（EconomyResolver → UnitProductionResolver → ConstructionResolver）不动。

### 3.6 Bootstrap
- `GameLifetimeScope.cs`：注册 `MovementResolver`；注入到 `TurnResolver` 和 `GameSessionService`。

### 3.7 Presentation — `MainHudController`

UI 行为（**简洁、玩家直观**）：

1. **点击部队所在省**：
   - 若该省有自己拥有的部队 → 设 `_session.SelectUnit(garrisonUnitIds[0])`（默认选第一支）；
   - **再次点同省** → 循环选下一支：当前若选中第 i 支，下次选第 (i+1) % count；
   - 同时设 `selectedProvinceId = 该省`。
2. **已选部队时点邻省**：
   - 发 `MoveUnit` 命令 → accepted → 状态栏 `{unitId} → {targetProvinceName}（剩余 {movesLeftAfter}）`；rejected → `被拒: {reason}`；
   - 移动后保持选中（让玩家可连续走多步）。
3. **点非邻省或敌方省**：
   - 不发命令，仅切换 `selectedProvinceId`、清空 `selectedUnitId`（重新进入"选省"态）。
4. **地图高亮规则**：
   - 选中省份：现有 `province-tile-selected` 边框；
   - 选中部队所在省的**邻接己方控制省**：加新 USS class `province-tile-move-target`（淡色边框/外发光，与选中样式有区别）；
   - 既有 garrison 徽章 `⚔N` 保留。
5. **详情栏**（在 C1 已有"驻军: N 支"之后追加）：
   - 若 `selectedUnitId` 非空且该 unit 在 selectedProvinceId 这个省：`| 已选部队: {unitId} (剩 {movesLeft}/{speed})`；
   - 否则不显示这一段。

**MainHudController 已 369 行膨胀（触发技术债 C-4 提醒）**——本单**允许**新增方法（如 `OnProvinceClick / RefreshMoveTargets`）但**不必拆分组件化**（拆放 C-4 触发的专门一单）。

### 3.8 UXML/USS
- `Assets/UI/MainHud.uss`：加 `.province-tile-move-target { border-color: rgba(180, 220, 120, 0.85); border-width: 3px; }`（具体色调按现有风格小改即可）。
- UXML 本单**不改**（没新控件）。

## 4. 测试

### EditMode — `MovementResolverTests`（新文件）
- `TryMove_Valid_MovesAndDeductsMovesLeft`：合法邻接己方省 → accepted、currentProvinceId 改、movesLeft -1。
- `TryMove_NotNeighbor_Rejects`：目标非邻 → rejected "非邻接省份"、状态不动。
- `TryMove_EnemyControlled_Rejects`：target.controllerCountry != owner → rejected "非己方控制省份"。
- `TryMove_NotOwnedByPlayer_Rejects`：unit.ownerCountry != playerCountryId → rejected "非己方部队"。
- `TryMove_NoMovesLeft_Rejects`：movesLeft=0 → rejected "移动力不足"。
- `TryMove_UnitNotFound_Rejects`：unitId 不存在 → rejected "部队不存在"。
- `TryMove_ConsecutiveSteps_DepleteMoves`：infantry.speed=3，3 次合法移动后 movesLeft=0，第 4 次 rejected。
- `ResetMovement_RestoresToSpeed`：手动把所有 unit.movesLeft 置 0 → ResetMovement → 所有 movesLeft == unit.speed。
- `ResetMovement_DeterministicOrder`：用 id "z","a","m" 三支部队 → ResetMovement 内部遍历顺序为 a,m,z（用日志或公共 hook 验证；如不便直测，至少保证最终状态一致）。

### EditMode — `GameSessionServiceTests`（追加）
- `IssueCommand_MoveUnit_Valid_Accepts`：选 alliance_east（赤原首都），其部队 alliance_east_inf_1 移到 high_peak（邻接）→ accepted、对应 garrison 数变化。
- `IssueCommand_MoveUnit_NonPlayerUnit_Rejects`：玩家国 empire_north，下令移动 republic_west 的部队 → rejected。
- `SelectUnit_Valid_SetsSelected`：SelectUnit("empire_north_inf_1") → view.selectedUnitId == "empire_north_inf_1"。
- `SelectUnit_Invalid_Ignored`：SelectUnit("nonexistent") → view.selectedUnitId 不变（null 或之前值）。
- `SelectUnit_Null_Deselects`：SelectUnit(null) → view.selectedUnitId == null。
- `MoveUnit_TurnAdvance_ResetsMovesLeft`：3 步走完 movesLeft=0 → AdvancePhase 推完整一回合 → unit.movesLeft == speed。

### EditMode — `ReadModelBuilderTests`（追加）
- `BuildWorldView_UnitsList_Populated`：world.units 含 3 支不同 id → view.units.Count == 3 + 按 id 升序。
- `BuildProvinceView_GarrisonUnitIds_SortedById`：3 支 unit 同省、id 倒序加入 → view.garrisonUnitIds 升序。
- `BuildWorldView_SelectedUnitId_PassedThrough`。

### EditMode — `SaveLoadEquivalenceTests`（追加）
- `SaveLoad_UnitMovement_Preserved`：移动 unit 一步（currentProvinceId 改、movesLeft 减）→ 存 → 读 → hash 等价。**HashWorld 已含 currentProvinceId/movesLeft**（C2a Phase 0.1 已扩），本测试验证不退化。

### EditMode — `ConfigValidationTests`（追加）
- `UnitConfig_HasSpeed`：units.json 每个 unitType 的 speed > 0。

### PlayMode — `MvpSmokeTests`（追加 1 用例）
- `MoveInfantry_Click_Adjacent_GarrisonShifts`：找到玩家国首都的 tile → 点击（选省+选部队）→ 点击一个邻省 tile → 源省 garrison 徽章数 -1、目标省 garrison 徽章数 +1。

## 5. 文件清单
| 动作 | 路径 |
|---|---|
| 改 | `Contracts/Commands/CommandType.cs`（+MoveUnit）、`Contracts/Commands/GameCommand.cs`（+unitId/+targetProvinceId） |
| 新增 | `Contracts/ReadModels/UnitView.cs`、`Contracts/Events/UnitMovedEvent.cs` |
| 改 | `Contracts/ReadModels/WorldView.cs`（+units/+selectedUnitId）、`Contracts/ReadModels/ProvinceView.cs`（+garrisonUnitIds） |
| 改 | `Domain/State/WorldState.cs`（+selectedUnitId） |
| 新增 | `Simulation/MovementResolver.cs` |
| 改 | `Simulation/TurnResolver.cs`（注入 + ResetMovement 在 ExecuteTurn 最开头） |
| 改 | `Application/Services/GameSessionService.cs`（IssueCommand 分支 + SelectUnit 方法 + UnitMovedEvent 发布） |
| 改 | `Application/Queries/ReadModelBuilder.cs`（BuildUnitView/units 列表/garrisonUnitIds/selectedUnitId） |
| 改 | `Application/Persistence/SaveModels.cs`（GameState +selectedUnitId）、`Application/Mapping/SaveMapper.cs`（双向 selectedUnitId） |
| 改 | `Bootstrap/GameLifetimeScope.cs`（DI 注册 MovementResolver） |
| 改 | `Presentation/MainHudController.cs`（部队选中循环 + 移动目标高亮 + 详情栏部队行 + 状态栏移动反馈） |
| 改 | `Assets/UI/MainHud.uss`（+ `.province-tile-move-target`） |
| 新增/改 | 上述测试文件（MovementResolverTests 新增、其他追加） |

## 6. 验收门禁（DoD）
- [ ] Phase 0 起点全绿；分支干净；artifacts 命名规范。
- [ ] EditMode 全绿：C2a 终态 + 本单新增（约 13 个）；含 MovementResolverTests 全集、SaveLoad_UnitMovement_Preserved、ReadModelBuilder 列表/排序、GameSessionService Move/Select 用例。
- [ ] PlayMode 全绿：C2a 终态 6/6 + 本单 +1 = 7/7。
- [ ] Play `Main`：选玩家国 → 点首都（部队被选中）→ 邻省高亮 → 点邻省 → 部队徽章从首都迁到邻省（**截图为证**，存 `Design/screenshots/c2b-select-unit.png`+`c2b-after-move.png`）。
- [ ] 续跑等价：移动 1 步后存 → 读 → 跑 1 回合 → movesLeft 已正确重置为 speed、currentProvinceId 在目标省。
- [ ] 规则 4 守住（Presentation 不引用 Domain/Simulation）；零新数值入 json（speed 用现有）；未做战斗/AI 移动/扩省；未改 BattleResolver / SupplyResolver / AIResolver / EconomyResolver / PoliticsResolver / ConstructionResolver / UnitProductionResolver。
- [ ] batchmode 0 error；`artifacts/c2b-editmode.xml` + `artifacts/c2b-playmode.xml`；PR 在 `feature/c2b-unit-movement`；CHANGELOG 写 PR 描述（**不直接改 CHANGELOG.md**）。
- [ ] commit diff 不含 `PROJECT_RULES.md` / `ARCHITECTURE.md` / `PROJECT_STATE.md` / `CHANGELOG.md` / `economy.json`。

## 7. 歧义处理
- "部队选中"色调、邻省高亮强度、移动状态栏文案 → 最小清晰实现，截图为准。
- 玩家点己方非邻省/敌方省的处理：按 §2.3 校验链拒绝后**不变**已选 unit（让玩家继续可点邻省）；点己方非邻省同样不变 unit 但**切换** selectedProvinceId（参考 §3.7 第 3 条）。如这与玩家直觉冲突，**最小修**只切 selectedProvinceId 不动 selectedUnitId 即可。
- 同省多部队循环选：当前 6 国 6 部队各驻己方首都，**只有玩家造兵且未移动出去**才会出现同省 2 支——C2b 必须能在这场景下让玩家选中第二支移走（否则永远卡在 1 支）。测试 `MoveUnit_PlayerCanMoveSecondUnit`：玩家国 capital 有 2 支（C2a 完工 +1），第一次点选 inf_1，移走 inf_1 至邻省，剩 inf_2 在首都 → 此时再点首都应自动选 inf_2（因为只剩 1 支了）→ 移动 inf_2 → 验证两支都迁走。
- **严禁**：在本单"顺手"做敌方省进入、战斗、占领、AI 部队移动、寻路、回退、传送 —— 任何超范围功能停 `[需 Claude 决策]`。
- **严禁**改既有结算器（EconomyResolver/PoliticsResolver/AIResolver/ConstructionResolver/UnitProductionResolver/BattleResolver/SupplyResolver）。如发现既有 bug，单独写 `[需 Claude 决策]` 报告。
- 校验失败时 reason 字符串保持简短中文；测试用 `Assert.AreEqual` 严格匹配（避免 OpenClaw 写"非邻接的省份"而我写"非邻接省份"对不上的反复试错——以本单 §2.3 写的为准）。

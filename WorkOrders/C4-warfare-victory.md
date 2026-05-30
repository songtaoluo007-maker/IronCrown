# 工作单 C4 — 战争状态 + 胜负终局 + 军事 AI（C 阶段收官）

| 项 | 值 |
|---|---|
| 工作单号 | C4（AI 主动进攻 + 双边战争状态 + 首都全占判胜负） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数据 | Claude（规则 13 + 经授权代拟 AI 启发式/战争状态机/胜负条件；规则 14 人类终审） |
| 分支 | `feature/c4-warfare-victory`（从 main 切，main 须先合入 C3 + P1 收尾） |
| 前置 | C3 已审查通过（含 P1 三项收尾：ReadModelBuilderTests 4 用例 + artifacts c3-* 归档 + Play 截图）+ P3 优化已完成（同省多攻方拒绝、garrison cycling 修正、真多 tick 累积测试） |
| 角色边界 | 规则 12：只实现本单（军事 AI + 战争状态 + 胜负）。**勿做**停战/和谈/外交关系深化（C5+）、AI 调防/撤退/造兵决策改动、stability/economy/warSupport 受战争影响（C5+）、扩省份、首都迁移、AI 部队也走移动管线（C5+）；遇未覆盖点停 `[需 Claude 决策]` |

## 0. 目标与范围

让游戏"打得起来"——AI 在 Military 阶段会**主动进攻邻接弱者**（复用 C3 的 `BattleResolver.InitiateAttack`）；进攻自动把双方关系从 AtPeace 转 AtWar（双边 `WarRelation`）；当玩家国所有首都被占 → `GameOver=Defeat`，当玩家占领所有其他国首都 → `GameOver=Victory`。**不做**：停战谈判、外交宣战仪式、warSupport/stability 受战争影响、AI 调防/撤退、AI 造兵优先级调整（沿用 B3 的纯经济决策）、扩省份。

## Phase 0（C3 收尾验证 + 起点）

### 0.1 C3 P1/P3 收尾验证（OpenClaw 在分支创建前自检）
- 确认 `Assets/Tests/EditMode/IronCrown.Application.Tests/ReadModelBuilderTests.cs` 含 C3 用例 4 个（OccupiedProvince color / hasActiveBattle / isInBattle / activeBattles sorted）。
- 确认 `artifacts/c3-editmode.xml` + `artifacts/c3-playmode.xml` 已归档（与 c1/c2a/c2b 三对权威 artifact 并列保留）。
- 确认 `Design/screenshots/c3-battle-initiated.png` + `c3-battle-tick.png` + `c3-after-occupy.png` 已存在。
- 确认 P3 三项：同省多攻方 InitiateAttack 拒绝（"目标省已有战斗"）/ MainHudController 循环选驻军按 ownerCountry+controllerCountry 双重过滤 / 真多 tick 累积伤害测试至少 1 个用例。

### 0.2 起点 & 卫生
- 从 main（C3 合入后）切 `feature/c4-warfare-victory`；起点 EditMode + PlayMode 全绿。
- **改 Resolver/Service 构造签名时必须 grep 全工程 `new <ClassName>(` 同步所有调用点**（含测试，C2b 曾踩 5 处遗漏）；提交 PR 前 Unity Editor Console **0 error**（USS 已知 2 条 `text-align` warning 可豁免）。
- 不直接编辑 `CHANGELOG.md / PROJECT_STATE.md / PROJECT_RULES.md / ARCHITECTURE.md`；改动摘要写 PR 描述。
- UTF-8；artifacts 命名 `artifacts/c4-editmode.xml` + `artifacts/c4-playmode.xml`。

## 1. 数据（Claude 代拟，规则 14 人类可调）

| 项 | 值 | 备注 |
|---|---|---|
| AI 进攻系数阈值 | `攻方 baseAttack × org_ratio ≥ 守方 baseDefense × org_ratio × 1.2` 才发动 | 留 20% 优势安全边际；写入 `economy.json` 新字段 `aiAttackPowerRatio=120`（百分比整数） |
| AI 一回合进攻上限 | **1 次** | 写入 `economy.json` 新字段 `aiMaxAttacksPerTurn=1`；防 AI 一回合多线开战导致世界爆炸 |
| AI 进攻目标范围 | **所有非己方控制邻省**（含玩家国 + 其他 AI 国） | 增加"威胁感"+"乱局感" |
| AI 部队选择策略 | 该国所有 `movesLeft >= 1` 且**不在战斗中**的部队按 id 升序遍历，找到第一对（attacker_unit, target_neighbor）满足阈值即发起 | 确定性，无随机 |
| AI 进攻不打"空城" | **不做特例**——空城进驻是 C3 的自然结果（BattleResolver.InitiateAttack 已处理）| AI 也通过 InitiateAttack 走 |
| 战争关系存储 | 双边 `WarRelation { countryA, countryB, startTurn }`；`countryA < countryB` Ordinal 严格升序（去重唯一键）| 不存"是谁挑起的"，C5 再加 |
| 战争触发 | `BattleResolver.InitiateAttack` 成功（含空城占领）后**自动** AtPeace→AtWar；已 AtWar 则幂等 | 不要在 GameSessionService 重复实现 |
| 胜利条件 | 所有 `country.id != playerCountryId` 的国家其 `capitalProvinceId` 对应省份 `controllerCountry == playerCountryId` | "占领所有敌方首都" |
| 失败条件 | 玩家国 `capitalProvinceId` 对应省份 `controllerCountry != playerCountryId` | "首都失守" |
| 判胜负位置 | `Simulation/VictoryConditionResolver.CheckVictory(world, clock)`，在 `TurnResolver.ExecuteSettlement` 尾段、TickBattles 之后 | 战斗结束当回合立即判 |
| GameOver 后行为 | `GameClock.CurrentPhase = GameOver` → `GameSessionService.IssueCommand` 全拒（reason "游戏已结束"）；`AdvancePhase` 立即返回不推 | 锁死游戏状态 |
| 无新增 json 数值 | 仅 `economy.json` 加 2 字段（aiAttackPowerRatio / aiMaxAttacksPerTurn）+ `EconomyConfig.cs` 对应 2 字段 | 复用 C3 的 BattleResolver 公式 |

## 2. 架构决策（写死）

1. **新增 Domain 类型** `Domain/State/WarRelation.cs`：
   ```csharp
   public sealed class WarRelation {
       public string countryA;   // Ordinal 升序：countryA < countryB
       public string countryB;
       public int startTurn;
   }
   ```
   双边唯一：同一对 (A,B) 只一条记录，与 (B,A) 等价。

2. **`WorldState` 加** `public List<WarRelation> warRelations = new();`（按 countryA, countryB 双字段升序操作）。

3. **新增 `Domain/WarRegistry.cs` 静态工具**（在 Domain 层而非 Simulation，因纯数据操作无副作用）：
   ```csharp
   public static class WarRegistry {
       public static bool AreAtWar(WorldState world, string a, string b) { /* 标准化 + Linq Any */ }
       public static bool TryDeclareWar(WorldState world, string a, string b, int currentTurn, out WarRelation declared) {
           // 标准化 (a,b) Ordinal 升序；若已存在返回 false；否则新建 + Add + Sort + 输出
       }
       private static (string lo, string hi) Normalize(string a, string b) { /* Ordinal compare */ }
   }
   ```
   Domain 不发事件（无 IEventPublisher），发事件由调用方负责（BattleResolver/Application）。

4. **`BattleResolver.InitiateAttack` 加自动宣战**（在校验全过、确认要发起战斗或空城占领时）：
   ```csharp
   if (WarRegistry.TryDeclareWar(world, attacker.ownerCountry, target.ownerCountry, world.turnNumber, out var newWar))
       _events.Publish(new WarDeclaredEvent { ... });
   ```
   注意：宣战基于 `target.ownerCountry`（法理归属），不是 `controllerCountry`（因占领可能让 X 控制 Y 的首都；和约定中"对法理主权方宣战"一致；C5 再细化）。**写死按 ownerCountry**。

5. **`AIResolver` 填桩**（不动现有经济 `MakeDecisions` 主流程）：
   - 现有 line 95-98 `ExecuteTacticalOrders` 空方法 → 填军事决策实参；
   - 现有 `FindWeakNeighborProvinces` stub → 填实参（返回 `(attackerUnit, targetProvince)` 候选列表，按 attacker.id 升序、target.id 升序）；
   - 新增 `private void TryAttack(CountryState country, WorldState world)`：调上面 stub 拿候选 → 按阈值过滤 → 取第一对调 `BattleResolver.InitiateAttack` → 命中 1 次即跳出（aiMaxAttacksPerTurn=1）。
   - 注入 `BattleResolver` 到 `AIResolver`（构造函数加参）。
   - 玩家国仍跳过（line 25 现有保护不动）。

6. **新增 `Simulation/VictoryConditionResolver.cs`**（独立 Resolver，单一职责）：
   ```csharp
   public sealed class VictoryConditionResolver {
       private readonly IEventPublisher _events;
       public VictoryConditionResolver(IEventPublisher events) { _events = events; }

       public VictoryOutcome CheckVictory(WorldState world, ITurnClock clock) {
           if (clock.CurrentPhase == GamePhase.GameOver)
               return VictoryOutcome.None; // 幂等
           if (string.IsNullOrEmpty(world.playerCountryId))
               return VictoryOutcome.None;

           // 失败：玩家首都被占
           if (world.countries.TryGetValue(world.playerCountryId, out var player) &&
               world.provinces.TryGetValue(player.capitalProvinceId, out var playerCapital) &&
               playerCapital.controllerCountry != world.playerCountryId)
               return TriggerGameOver(world, "Defeat", null);

           // 胜利：所有非玩家国首都被玩家控制
           bool allCaptured = true;
           foreach (var c in world.countries.Values.OrderBy(c => c.id, StringComparer.Ordinal)) {
               if (c.id == world.playerCountryId) continue;
               if (!world.provinces.TryGetValue(c.capitalProvinceId, out var cap)) { allCaptured = false; break; }
               if (cap.controllerCountry != world.playerCountryId) { allCaptured = false; break; }
           }
           if (allCaptured && world.countries.Count > 1)
               return TriggerGameOver(world, "Victory", world.playerCountryId);

           return VictoryOutcome.None;
       }

       private VictoryOutcome TriggerGameOver(WorldState world, string result, string winnerCountryId) {
           world.gameOverResult = result;
           world.gameOverWinnerCountryId = winnerCountryId;
           _events.Publish(new GameOverEvent { result = result, winnerCountryId = winnerCountryId });
           return new VictoryOutcome { result = result, winnerCountryId = winnerCountryId };
       }
   }
   public struct VictoryOutcome { public string result; public string winnerCountryId; public static VictoryOutcome None => default; }
   ```
   `GameClock` 切 GameOver 由 `GameSessionService` 在订阅 `GameOverEvent` 时做（避免 Resolver 直接动 Clock）。

7. **`TurnResolver.ExecuteSettlement` 尾段加** `_victory.CheckVictory(world, _clock)`，**严格位置**：TickBattles 之后（占领即时反映）。注入 `VictoryConditionResolver _victory`。

8. **`GameSessionService` 订阅 `GameOverEvent`**：在构造函数中 `_events.Subscribe<GameOverEvent>(e => _clock.SetGameOver())`（GameClock 加 SetGameOver 方法，直接写 `CurrentPhase = GamePhase.GameOver`）；`IssueCommand` 入口最前面加 `if (_clock.CurrentPhase == GamePhase.GameOver) return Reject("游戏已结束");`；`AdvancePhase` 入口已有 `if (_clock.CurrentPhase == GamePhase.GameOver) return;` 沿用。

9. **存档** `WorldState` 加 `public string gameOverResult; public string gameOverWinnerCountryId;`，进 `GameState` 双向；`warRelations` 也进存档双向。`HashWorld` 扩展含 warRelations + gameOverResult（按 countryA/countryB 升序写）。

10. 规则 4：UI 只读 ReadModel；规则 3：胜负/AI 逻辑全在 Simulation；规则 8：不新建平行战斗管线（AI 进攻必须复用 BattleResolver.InitiateAttack）；规则 9：不重构既有 C3 战斗代码。

## 3. 实现规格

### 3.1 Contracts
- `Contracts/Events/WarDeclaredEvent.cs`（新）：`string countryA; string countryB; int startTurn;`（与 WarRelation 一一对应）
- `Contracts/Events/GameOverEvent.cs`（新）：`string result; // "Victory" | "Defeat" string winnerCountryId; // 玩家胜则=playerCountryId；失败则=null`
- `Contracts/ReadModels/WarRelationView.cs`（新）：与 WarRelation 同字段
- `Contracts/ReadModels/WorldView.cs`：加 `List<WarRelationView> warRelations; string gameOverResult; string gameOverWinnerCountryId;`

### 3.2 Domain
- `Domain/State/WarRelation.cs`（新）：见 §2.1
- `Domain/State/WorldState.cs`：加 `warRelations / gameOverResult / gameOverWinnerCountryId`
- `Domain/WarRegistry.cs`（新）：纯静态工具，见 §2.3

### 3.3 Simulation
- `Simulation/BattleResolver.cs`：在 `InitiateAttack` 校验全过后（空城分支 line 110 前 + 战斗分支 line 127 前）**统一**加一处 `WarRegistry.TryDeclareWar` + `WarDeclaredEvent` 发布；位置写死在"扣 movesLeft 之后、改省份/创建战斗之前"。**不动 ResolveBattle / TickBattles / DestroyUnit / float 公式**。
- `Simulation/VictoryConditionResolver.cs`（新）：见 §2.6
- `Simulation/AIResolver.cs`：
  - 注入 `BattleResolver _battle`（构造函数加参）
  - 在 `MakeDecisions` 末尾（line 42 之后，桩之前）调 `TryAttack(country, world)`
  - 填实 `FindWeakNeighborProvinces`：返回 `List<(string attackerUnitId, string targetProvinceId)>`，按 attackerUnitId Ordinal 升序、targetProvinceId Ordinal 升序
  - 新增 `private void TryAttack(CountryState country, WorldState world)`：
    ```csharp
    int attacksLeft = eco.aiMaxAttacksPerTurn;
    foreach (var unit in world.units.Values
              .Where(u => u.ownerCountry == country.id && u.movesLeft >= 1)
              .OrderBy(u => u.id, StringComparer.Ordinal))
    {
        if (attacksLeft <= 0) break;
        // 跳过战斗中部队
        if (world.activeBattles.Any(b => b.attackerUnitId == unit.id || b.defenderUnitId == unit.id)) continue;
        if (!world.provinces.TryGetValue(unit.currentProvinceId, out var cur)) continue;
        if (cur.neighbors == null) continue;
        foreach (var nId in cur.neighbors.OrderBy(s => s, StringComparer.Ordinal)) {
            if (!world.provinces.TryGetValue(nId, out var nProv)) continue;
            if (nProv.controllerCountry == country.id) continue; // 己方
            // 目标省已有战斗 → 跳过（C3 P3 决策一致）
            if (world.activeBattles.Any(b => b.provinceId == nId)) continue;
            if (!IsAttackerStrongEnough(unit, nProv, world, eco)) continue;
            var result = _battle.InitiateAttack(world, unit.id, nId, country.id);
            if (result.accepted) {
                attacksLeft--;
                break; // 该 unit 已发动，本轮换下一支
            }
        }
    }
    ```
  - 新增私有 `IsAttackerStrongEnough(unit, targetProvince, world, eco)`：
    - 取该省所有非己方控制者部队（按 id 升序）；空 → true（空城）
    - 取守方 [0] 为代表：`atkPower = unit.baseAttack * unit.organization * 100 / Math.Max(1, unit.maxOrganization)` 整数运算；`defPower = def.baseDefense * def.organization * 100 / Math.Max(1, def.maxOrganization)` 整数
    - 返回 `atkPower * 100 >= defPower * eco.aiAttackPowerRatio`（避免除法、保留整数确定性）
- `Simulation/TurnResolver.cs`：注入 `VictoryConditionResolver _victory`；`ExecuteSettlement` 在 `_battle.TickBattles(world)` 之后调 `_victory.CheckVictory(world, _clock)`。

### 3.4 Application
- `Application/Services/GameSessionService.cs`：
  - `IssueCommand` 最前面加 `if (_clock.CurrentPhase == GamePhase.GameOver) return CommandResult.Reject("游戏已结束");`
  - 构造函数内（最后一行 `_logger = logger` 前）订阅 `_events.Subscribe<GameOverEvent>(_ => _clock.SetGameOver());`
- `Application/Queries/ReadModelBuilder.cs`：`BuildWorldView` 拼装 `warRelations` 列表（按 countryA, countryB Ordinal 双键升序）+ 透传 `gameOverResult / gameOverWinnerCountryId`。
- `Application/Persistence/SaveModels.cs`：
  - `GameState` 加 `string gameOverResult; string gameOverWinnerCountryId; WarRelationSaveData[] warRelations;`
  - 新增 `[Serializable] class WarRelationSaveData { string countryA; string countryB; int startTurn; }`
- `Application/Mapping/SaveMapper.cs`：双向 warRelations + gameOver 双字段；HashWorld 扩展。

### 3.5 Domain — `GameClock.cs`
- 加 `public void SetGameOver() { CurrentPhase = GamePhase.GameOver; }`（一行级，确定性）。
- `GameOver` 已在枚举中（line 80）。

### 3.6 Bootstrap
- `Bootstrap/GameLifetimeScope.cs`：注册 `VictoryConditionResolver`；`BattleResolver` 已注入 `AIResolver`（构造签名变了——**grep 全工程 `new AIResolver(` 同步**，含测试）。

### 3.7 Presentation — `MainHudController`
- **国家列表行**：若该国与玩家国 AtWar → 在 `FormatCountryRow` 末尾追加 `  |  ⚔ 交战中`（按 vm.warRelations 查）。
- **战争状态详情栏**（可选最小化）：选中省的 ownerCountry 若与玩家国 AtWar → 详情栏追加 `战争状态: AtWar (自 T{startTurn})`。
- **GameOver 反馈**：订阅 `GameOverEvent`：
  - result == "Victory" → 状态栏大字红色 `🎉 胜利！占领所有敌方首都`
  - result == "Defeat" → 状态栏大字红色 `💀 失败！首都已失守`
  - 同时给 `_advanceBtn.SetEnabled(false)` 防点击。
  - 加 USS class `.status-game-over { color: ...; font-size: 22px; ...; }`。

### 3.8 UXML/USS
- `Assets/UI/MainHud.uss`：加 `.status-game-over`（红色大字）。其余沿用。
- UXML 本单**不改**。

## 4. 测试

### EditMode — `WarRegistryTests`（新）
- `TryDeclareWar_NewPair_AddsRelationOrdinalNormalized`：传 ("B","A") → 内部 countryA="A", countryB="B"。
- `TryDeclareWar_Existing_ReturnsFalseIdempotent`：第二次同对 → false，list 长度不变。
- `AreAtWar_BidirectionalSymmetry`：`AreAtWar("A","B") == AreAtWar("B","A")`。

### EditMode — `VictoryConditionResolverTests`（新）
- `CheckVictory_PlayerCapitalLost_TriggersDefeat`：玩家首都 controllerCountry 改为他国 → 返回 Defeat、发 GameOverEvent、world.gameOverResult=="Defeat"。
- `CheckVictory_AllEnemyCapitalsCaptured_TriggersVictory`：玩家国 + 1 敌国，敌国首都 controllerCountry 改为玩家 → Victory。
- `CheckVictory_NeitherWinNorLose_ReturnsNone`：初始局 → None。
- `CheckVictory_AlreadyGameOver_Idempotent`：第一次 Defeat 后再调 → 不重复发事件。
- `CheckVictory_NoPlayerCountry_ReturnsNone`：playerCountryId 为空 → None。

### EditMode — `AIResolverMilitaryTests`（新）
- `TryAttack_StrongAttacker_Attacks`：构造 AI 部队 baseAttack=20、邻省守军 baseDefense=5 → 阈值过 → 调 BattleResolver 创建 ActiveBattle。
- `TryAttack_WeakAttacker_DoesNotAttack`：baseAttack=5 vs baseDefense=20 → 阈值不过 → world.activeBattles 不变。
- `TryAttack_MaxOneAttackPerTurn`：构造 AI 2 支强部队 + 2 个弱邻省 → 调 MakeDecisions 一次 → 仅 1 个 ActiveBattle 创建。
- `TryAttack_SkipFriendlyNeighbor`：所有邻省都己方控制 → 不进攻。
- `TryAttack_SkipProvinceAlreadyInBattle`：邻省已有 ActiveBattle → 跳过。
- `TryAttack_SkipUnitInBattle`：本国 unit_a 已在 ActiveBattle、unit_b 强 → unit_a 被跳过、unit_b 发起。
- `TryAttack_PlayerCountry_NotProcessed`：country.id == playerCountryId → MakeDecisions 早 return，零进攻发起。
- `TryAttack_DeterministicOrderById`：3 支 AI 部队 id=z/a/m → 第一发起的是 a。

### EditMode — `BattleResolverWarDeclareTests`（新）
- `InitiateAttack_PeaceState_AutoDeclaresWar`：A 攻 B 首次 → world.warRelations.Count==1 + WarDeclaredEvent 发布 1 次。
- `InitiateAttack_AlreadyAtWar_NoDuplicateDeclare`：第二次攻 → list 长度不变，无 WarDeclaredEvent。
- `InitiateAttack_EmptyProvince_StillDeclaresWar`：空城进驻也宣战（基于 ownerCountry）。

### EditMode — `GameSessionServiceTests`（追加）
- `IssueCommand_GameOver_AllRejected`：手动设 GameOver phase → IssueCommand BuildCivilianFactory → rejected "游戏已结束"。
- `GameOverEvent_SubscribedFlipsClock`：手动 publish GameOverEvent → clock.CurrentPhase == GameOver。

### EditMode — `ReadModelBuilderTests`（追加）
- `BuildWorldView_WarRelations_PopulatedAndSortedByCountryAB`。
- `BuildWorldView_GameOverFields_PassedThrough`。

### EditMode — `SaveLoadEquivalenceTests`（追加）
- `SaveLoad_WarRelations_Preserved`：双方宣战 → 存 → 读 → hash 等价 + warRelations 长度+字段一致。
- `SaveLoad_GameOver_Preserved`：触发 Victory → 存 → 读 → gameOverResult/winnerCountryId 一致。
- `HashWorld` 扩展含 warRelations + gameOverResult + gameOverWinnerCountryId。

### EditMode — `ConfigValidationTests`（追加）
- `Economy_HasAiAttackPowerRatio`：economy.json 含字段且 > 0。
- `Economy_HasAiMaxAttacksPerTurn`：同上 ≥ 1。

### PlayMode — `MvpSmokeTests`（追加 1 用例）
- `AI_AttacksOverTurns_GameStateAdvances`：玩家选 alliance_east → 推 N 回合（不操作）→ 验证 world.activeBattles 至少出现过 1 次（AI 互打）OR warRelations 至少 1 条 OR 任一非玩家省 controllerCountry 改变。**flaky 风险**：阈值过严 AI 不攻；可临时构造测试用强 attacker（如 WorldInitializer 测试分支 +baseAttack 100）。**或更稳的方法**：直接 reflection 调 AIResolver.TryAttack 验证不依赖 PlayMode 顺序。

## 5. 文件清单
| 动作 | 路径 |
|---|---|
| 新增 | `Domain/State/WarRelation.cs`、`Domain/WarRegistry.cs` |
| 改 | `Domain/State/WorldState.cs`（+warRelations / +gameOverResult / +gameOverWinnerCountryId）、`Domain/Time/GameClock.cs`（+SetGameOver） |
| 新增 | `Simulation/VictoryConditionResolver.cs` |
| 改 | `Simulation/BattleResolver.cs`（InitiateAttack 加自动宣战；其余不动）、`Simulation/AIResolver.cs`（注入 BattleResolver + TryAttack + 填 FindWeakNeighborProvinces stub） |
| 改 | `Simulation/TurnResolver.cs`（注入 VictoryConditionResolver + ExecuteSettlement 尾段调 CheckVictory） |
| 新增 | `Contracts/Events/WarDeclaredEvent.cs`、`Contracts/Events/GameOverEvent.cs`、`Contracts/ReadModels/WarRelationView.cs` |
| 改 | `Contracts/ReadModels/WorldView.cs`（+warRelations / +gameOverResult / +gameOverWinnerCountryId） |
| 改 | `Application/Services/GameSessionService.cs`（IssueCommand 入口 GameOver 短路 + 订阅 GameOverEvent → SetGameOver） |
| 改 | `Application/Queries/ReadModelBuilder.cs`（warRelations 拼装 + 透传 gameOver 字段） |
| 改 | `Application/Persistence/SaveModels.cs`（+WarRelationSaveData + GameState 三字段）、`Application/Mapping/SaveMapper.cs`（双向 + HashWorld 扩展） |
| 改 | `Domain/Config/EconomyConfig.cs`（+aiAttackPowerRatio / +aiMaxAttacksPerTurn）、`Assets/StreamingAssets/Configs/Json/economy.json`（+这 2 字段，值 120 / 1） |
| 改 | `Bootstrap/GameLifetimeScope.cs`（注册 VictoryConditionResolver + AIResolver 构造签名变更同步） |
| 改 | `Presentation/MainHudController.cs`（国家行 ⚔ 交战中 + 战争状态详情 + GameOver 状态栏大字 + 禁推进按钮） |
| 改 | `Assets/UI/MainHud.uss`（+ `.status-game-over`） |
| 新增/改 | 上述测试文件 |
| 清理 | 无（C3 残留 artifact 已在 C3 Phase 0 清理） |

## 6. 验收门禁（DoD）
- [ ] Phase 0 起点全绿；C3 P1/P3 收尾已存在；分支干净。
- [ ] EditMode 全绿：C3 终态 + 本单新增（约 25 个：WarRegistry 3 + Victory 5 + AI 8 + BattleWar 3 + Session 2 + ReadModel 2 + SaveLoad 2）。
- [ ] PlayMode 全绿：C3 终态 + 本单 +1。
- [ ] Play `Main`：玩家选小国（如 federation_central，high_peak 4 邻枢纽，最容易被围殴）→ **不操作**推 ~10 回合 → AI 互打可见（地图省色变化、⚔战 标记、`⚔ 交战中` 国家行）→ 若最终首都被占 → 状态栏大红字"失败"+推进按钮禁用（**截图为证**：`Design/screenshots/c4-ai-attacking.png` AI 进攻中、`c4-war-state.png` 多场战争中、`c4-game-over-defeat.png` 失败）。
- [ ] 续跑等价：宣战后 → 存 → 读 → hash 等价 + warRelations / gameOver 字段持久。
- [ ] GameOver 触发后 IssueCommand 全拒、AdvancePhase 立即返回。
- [ ] 规则 4 守住；新增数值仅 economy.json 2 字段；未做停战/和谈/AI 调防/AI 撤退/AI 造兵改动/扩省；未改 BattleResolver 既有 `ResolveBattle / CalculateAttack / CalculateDefense / ApplyRandom` 公式；未改 EconomyResolver / PoliticsResolver / ConstructionResolver / UnitProductionResolver / MovementResolver / SupplyResolver。
- [ ] batchmode 0 error（Unity Editor Console 也 0 error）；`artifacts/c4-editmode.xml` + `artifacts/c4-playmode.xml`；PR 在 `feature/c4-warfare-victory`；CHANGELOG 写 PR 描述。
- [ ] commit diff 不含 `PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md / units.json / countries.json / provinces.json`。

## 7. 歧义处理
- AI 进攻"启发式阈值"调参 → 数值已在 economy.json，人类可调，**勿在工作单期间擅自调**。
- AI 造兵优先级（C5+ 可加"威胁高时优先 military factory"）→ **本单不做**。
- AI 部队调防/撤退 → **本单不做**。
- 停战谈判 / warSupport / stability 战争影响 / 占领后 resistance → **本单不做**。
- 同盟 / 多边战争状态 → **本单不做**，双边即可。
- AI 进攻"空城进驻"也宣战是否合理 → **按 §2.4 写死宣战**（基于 ownerCountry 法理主权）。
- 胜利后是否给玩家"继续征服"选项 → **本单不做**，直接 GameOver 锁死。
- 改 `GameSessionService / TurnResolver / AIResolver` 构造签名时**务必** grep 全工程同步；提交前 Unity Editor Console 必须 0 error。
- reason 字符串："游戏已结束"（GameOver 期 IssueCommand 拒）保持简短中文一致。
- 严禁在本单"顺手"做 BattleResolver float→int 重构 / 修改既有 C3 战斗代码 / 给 AI 加"撤退/调防"功能 / 给玩家加"宣战命令"按钮（玩家进攻自动宣战已够）；如发现既有 bug 单独 `[需 Claude 决策]`。

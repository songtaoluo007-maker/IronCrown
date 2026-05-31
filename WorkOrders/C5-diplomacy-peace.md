# 工作单 C5 — 停战 + 战争代价（外交扩展第一步）

| 项 | 值 |
|---|---|
| 工作单号 | C5（停战谈判 + warExhaustion + 战争对 stability/warSupport 的影响） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数据 | Claude（规则 13 + 经授权代拟战争代价数值/AI 接受阈值；规则 14 人类终审） |
| 分支 | `feature/c5-diplomacy-peace`（从 main 切，main 须先合入 C4 + 收尾） |
| 前置 | C4 已审查通过 + 收尾合并：①C4 PROJECT_STATE/CHANGELOG 违规改动已还原（governance 文件干净）②4 对 artifacts (c2a/c2b/c3/c4) 归档 ③ 5 单 Play 截图归档 |
| 角色边界 | 规则 12：只实现本单（停战 + 战争代价）。**勿做**双边关系等级（中立/敌对/同盟）、割地赔款条款、中立化承诺、AI 调防/撤退、占领后 resistance（C5b/C6+ 单独签发）；遇未覆盖点停 `[需 Claude 决策]` |

## 0. 目标与范围

让战争"能停得下来 + 有代价"：① `CountryState.warExhaustion` 字段+每回合处于 AtWar 累积；② 每回合 AtWar 国家 stability -N（持久战拖累内政）；③ 战斗胜负影响双方 warSupport；④ 玩家可发 `OfferPeace` 命令；⑤ AI 接受停战的启发式（综合实力对比 + warExhaustion）；⑥ 接受 → 移除 WarRelation、双方 warExhaustion 减半，**被占省保持 controllerCountry 不归还**（简化，C6 再加割地谈判）。**不做**：双边关系等级、同盟、中立化承诺、AI 主动提议停战、停战后再宣战冷却。

## Phase 0（C4 收尾验证 + 起点）

### 0.1 C4 收尾验证（OpenClaw 在分支创建前自检）
- 确认 `PROJECT_STATE.md` / `CHANGELOG.md` 在 main 上是 C3 末尾状态（C4 违规改动已还原）。
- 确认 `artifacts/` 至少含 `c1-editmode5.xml + c1-playmode-final.xml + c2a-editmode.xml + c2a-playmode.xml + c3-editmode.xml + c3-playmode.xml + c4-editmode.xml + c4-playmode.xml`（c2b 若 Phase 0 漏归档也补上）。
- 确认 `Design/screenshots/` 至少含 `c1-garrison-badge.png / c2a-build-completed.png / c2b-after-move.png / c3-after-occupy.png / c4-ai-attacking.png` 5 张关键证据。
- 缺任一 → 不开分支，先回 OpenClaw 补齐 C4 收尾。

### 0.2 起点 & 卫生（**本单 PR 提交 check list**，缺一项不可合并）
| Check | 通过条件 |
|---|---|
| 起点全绿 | EditMode + PlayMode 跑一遍与 C4 终态等价 |
| Unity Console 0 error | 提交 PR 前必须确认（USS 已知 2 条 `text-align` warning 可豁免） |
| 构造签名同步 | 改 Resolver/Service 构造签名时**全工程 grep `new <ClassName>(`**（含测试），C2b/C4 都踩过 |
| governance 文件干净 | diff 不含 `PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md`——**这是连续 4 单的失败模式，本单 OpenClaw 一票否决**。改动摘要写 PR 描述由 Claude 合入 |
| ProjectSettings/Packages 干净 | diff 不含 `ProjectSettings/ProjectVersion.txt / Packages/packages-lock.json`（C4 越界，本单杜绝）|
| artifacts 归档 | `artifacts/c5-editmode.xml` + `artifacts/c5-playmode.xml` 必出，命名严格无后缀 |
| Play 截图 | `Design/screenshots/c5-war-exhaustion.png` + `c5-peace-offered.png` + `c5-peace-concluded.png` 至少 3 张 |
| 文件编码 | UTF-8（无 BOM）—— C4 曾出现非 ASCII 文件编码问题 |

### 0.3 PR 必填段落
PR 描述必须含 `## DoD Check List`，逐项打 `[x]` 或写明豁免理由——本单不接受"看着对就交"。

## 1. 数据（Claude 代拟，规则 14 人类可调）

新增 `economy.json` 6 字段（全部整数，按 100 倍百分比避免 float）：

| 字段 | 值 | 含义 |
|---|---|---|
| `warStabilityPenaltyPerTurn` | `1` | AtWar 中每回合 stability -1 |
| `warExhaustionPerTurn` | `1` | AtWar 中每回合 warExhaustion +1 |
| `warSupportPenaltyPerLoss` | `5` | 一场战败 warSupport -5 |
| `warSupportBonusPerVictory` | `5` | 一场战胜 warSupport +5 |
| `warSupportPenaltyPerCapitalLoss` | `15` | 被占己方首都 warSupport -15（一次性） |
| `aiPeaceAcceptExhaustionThreshold` | `30` | AI warExhaustion ≥ 30 进入"愿意停战"基础线 |
| `aiPeaceAcceptPowerRatioPct` | `80` | AI 实力 ≤ 对方 80%（int 整数计算）时下调 warExhaustion 阈值减半 |

**EconomyConfig.cs 加对应 7 字段**（注：上面共 7 个字段 + 名字一一对应）。

### AI 停战接受启发式（写死）
```
if (myExhaustion >= aiPeaceAcceptExhaustionThreshold && myPower <= theirPower * aiPeaceAcceptPowerRatioPct / 100)
    return Accept;
if (myExhaustion >= aiPeaceAcceptExhaustionThreshold * 2)  // 极度疲惫无条件接受
    return Accept;
return Reject;
```
玩家提议时按此判断；AI 不主动提议（C6+）。

## 2. 架构决策（写死）

1. **新增 `Domain/State` 字段**：`CountryState.warExhaustion`（int，默认 0；正常和平回合不变化）。
2. **新增 `Simulation/WarTollResolver.cs`**：每回合 Settlement 在 `TickBattles` **之后**调用 `ApplyTurnToll(world, eco)`，遍历所有有 WarRelation 的国家：
   - `country.stability -= eco.warStabilityPenaltyPerTurn`
   - `country.warExhaustion += eco.warExhaustionPerTurn`
   - Clamp stability 到 [0, 100]、warExhaustion 到 [0, 100]
3. **`BattleResolver` 订阅自身的 `BattleConcludedEvent` 不可行**（怕循环订阅）→ 改在 `TickBattles` 内部、`BattleConcludedEvent` 发布**之前**直接调本类私有 `ApplyBattleToll(world, eco, winnerKind, attacker, defender)` 改两国 warSupport。**写死位置**：[BattleResolver.cs](Assets/Scripts/Simulation/BattleResolver.cs) `TickBattles` 的 3 个收尾分支（attackerWon / defenderWon / draw）各加一次调用。draw 时双方都按 loss 算。
4. **`ProvinceOccupiedEvent` 副作用**：占领后若 `previousControllerCountry == playerCountryId || previousControllerCountry == ownerCountry`，且 `previousControllerCountry == thatCountry.capitalProvinceId 所在地`（即丢首都）→ 该国 warSupport -=  warSupportPenaltyPerCapitalLoss。**放在 BattleResolver.cs**（既然占领在 BattleResolver 触发）。
5. **新增 `Simulation/PeaceResolver.cs`**：
   ```
   public CommandResult OfferPeace(WorldState world, string fromCountry, string toCountry, EconomyConfig eco) {
       // 校验：from/to 存在 / 双方 AtWar / fromCountry 是当前调用上下文允许的
       // 计算 AI 接受决策（按 §1 启发式，from=玩家方时 to=AI 方）
       // Accept → 双向 WarRelation 移除 + 双方 warExhaustion /= 2 + 发 PeaceConcludedEvent
       // Reject → 发 PeaceOfferedEvent (accepted=false) + 返回 reason
   }
   ```
   **不动 controllerCountry**（被占省永久划归占领方）。
6. **`GameCommand` 加** `targetCountryId`；`CommandType` 加 `OfferPeace`。
7. **`GameSessionService.IssueCommand`** 加 `case CommandType.OfferPeace:` → 调 `_peace.OfferPeace(...)`。注入 `PeaceResolver _peace`。
8. **PoliticsResolver 不改公式**（B1.5 既有 stability 由税率/民生影响保留）—— C5 的 stability/warSupport 变化**全在 WarTollResolver + BattleResolver**，不污染 PoliticsResolver。规则 9 严守。
9. **存档**：`CountrySaveData.warExhaustion` 进存档；`HashWorld` 扩含 warExhaustion（按 country.id 升序写）。
10. **规则 4/3/5/8/9 全守**：UI 只读 ReadModel；战争代价逻辑全在 Simulation；新数值入 economy.json；不新建平行战争管线；不重构 PoliticsResolver / BattleResolver 既有公式。

## 3. 实现规格

### 3.1 Contracts
- `Contracts/Commands/CommandType.cs`：加 `OfferPeace`。
- `Contracts/Commands/GameCommand.cs`：加 `public string targetCountryId;`（仅 OfferPeace 用）。
- `Contracts/Events/PeaceOfferedEvent.cs`（新）：`string fromCountry; string toCountry; bool accepted; string reason;`
- `Contracts/Events/PeaceConcludedEvent.cs`（新）：`string countryA; string countryB; int atTurn;`（countryA Ordinal < countryB）
- `Contracts/ReadModels/CountryView.cs`：加 `int warExhaustion;`
- `Contracts/ReadModels/WorldView.cs`：无新增（warRelations C4 已加）。

### 3.2 Domain
- `Domain/Country.cs`：加 `public int warExhaustion;`（默认 0）。
- `Domain/Config/EconomyConfig.cs`：加 7 字段（见 §1）。
- `Domain/WarRegistry.cs`：加 `public static bool TryEndWar(WorldState world, string a, string b, out WarRelation removed)`（移除一对，Ordinal normalized）。

### 3.3 Simulation
- `Simulation/WarTollResolver.cs`（新）：
  ```csharp
  public sealed class WarTollResolver {
      public void ApplyTurnToll(WorldState world, EconomyConfig eco) {
          // 收集所有正在战争中的国家 id
          var atWarCountries = new HashSet<string>();
          foreach (var w in world.warRelations) {
              atWarCountries.Add(w.countryA);
              atWarCountries.Add(w.countryB);
          }
          foreach (var id in atWarCountries.OrderBy(s => s, StringComparer.Ordinal)) {
              if (!world.countries.TryGetValue(id, out var c)) continue;
              c.stability = Math.Clamp(c.stability - eco.warStabilityPenaltyPerTurn, 0, 100);
              c.warExhaustion = Math.Clamp(c.warExhaustion + eco.warExhaustionPerTurn, 0, 100);
          }
      }
  }
  ```
- `Simulation/PeaceResolver.cs`（新）：
  ```csharp
  public sealed class PeaceResolver {
      private readonly IEventPublisher _events;
      public PeaceResolver(IEventPublisher events) { _events = events; }

      public CommandResult OfferPeace(WorldState world, string fromCountry, string toCountry, EconomyConfig eco) {
          if (!world.countries.TryGetValue(fromCountry, out var from)) return CommandResult.Reject("发起国不存在");
          if (!world.countries.TryGetValue(toCountry, out var to)) return CommandResult.Reject("目标国不存在");
          if (fromCountry == toCountry) return CommandResult.Reject("不能与自己停战");
          if (!WarRegistry.AreAtWar(world, fromCountry, toCountry)) return CommandResult.Reject("双方未处于战争状态");

          bool accept = ShouldAcceptPeace(from, to, world, eco);

          if (accept) {
              WarRegistry.TryEndWar(world, fromCountry, toCountry, out var removed);
              from.warExhaustion = Math.Max(0, from.warExhaustion / 2);
              to.warExhaustion = Math.Max(0, to.warExhaustion / 2);
              // 标准化 (lo, hi) Ordinal
              var (lo, hi) = fromCountry.CompareTo(toCountry) < 0 ? (fromCountry, toCountry) : (toCountry, fromCountry);
              _events.Publish(new PeaceOfferedEvent { fromCountry = fromCountry, toCountry = toCountry, accepted = true, reason = "" });
              _events.Publish(new PeaceConcludedEvent { countryA = lo, countryB = hi, atTurn = world.turnNumber });
              return CommandResult.Accept();
          } else {
              _events.Publish(new PeaceOfferedEvent { fromCountry = fromCountry, toCountry = toCountry, accepted = false, reason = "对方拒绝（实力优势/疲惫不足）" });
              return CommandResult.Reject("对方拒绝停战");
          }
      }

      private bool ShouldAcceptPeace(CountryState me /* 收到提议方 */, CountryState requester, WorldState world, EconomyConfig eco) {
          // 注意：这里 me = toCountry（被提议方），requester = fromCountry（提议方）
          // 用 me.warExhaustion 评估愿意度
          int myPower = ComputeNationalPower(me, world);
          int theirPower = ComputeNationalPower(requester, world);

          if (me.warExhaustion >= eco.aiPeaceAcceptExhaustionThreshold * 2) return true; // 极度疲惫
          if (me.warExhaustion >= eco.aiPeaceAcceptExhaustionThreshold
              && myPower * 100 <= theirPower * eco.aiPeaceAcceptPowerRatioPct) return true; // 疲惫 + 实力弱势
          return false;
      }

      private int ComputeNationalPower(CountryState c, WorldState world) {
          // 简化国力 = 工厂数*10 + 部队数*20 + 资本/10
          int factories = c.civilianFactories + c.militaryFactories + c.dockyards;
          int units = world.units.Values.Count(u => u.ownerCountry == c.id);
          int capital = c.GetResource("capital") / 10;
          return factories * 10 + units * 20 + capital;
      }
  }
  ```
  **注意签名**：被提议方决定接受与否；玩家提议→AI 评估时 me=AI；AI 不会主动提议（C6+）。

- `Simulation/BattleResolver.cs`：
  - 在 `TickBattles` 的 3 个收尾分支末尾、`BattleConcludedEvent` 发布**之前**插入：
    ```csharp
    ApplyBattleToll(world, attacker, defender, "Attacker"/"Defender"/"Draw");
    ```
  - 新增私有方法：
    ```csharp
    private void ApplyBattleToll(WorldState world, UnitState attacker, UnitState defender, string winnerKind) {
        var eco = _config?.Get<EconomyConfig>("global"); // 注入 _config（构造函数加 IConfigRegistry）
        if (eco == null) return;
        if (!world.countries.TryGetValue(attacker.ownerCountry, out var atkCountry)) return;
        if (!world.countries.TryGetValue(defender.ownerCountry, out var defCountry)) return;
        switch (winnerKind) {
            case "Attacker":
                atkCountry.warSupport = Math.Clamp(atkCountry.warSupport + eco.warSupportBonusPerVictory, 0, 100);
                defCountry.warSupport = Math.Clamp(defCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                break;
            case "Defender":
                defCountry.warSupport = Math.Clamp(defCountry.warSupport + eco.warSupportBonusPerVictory, 0, 100);
                atkCountry.warSupport = Math.Clamp(atkCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                break;
            case "Draw":
                atkCountry.warSupport = Math.Clamp(atkCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                defCountry.warSupport = Math.Clamp(defCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                break;
        }
    }
    ```
  - **`BattleResolver` 构造函数加 `IConfigRegistry config = null`**（默认 null 兼容 C3 测试），用于 ApplyBattleToll/CapitalLoss 取 eco。grep 全工程 `new BattleResolver(` 同步。
  - 在占领分支（`InitiateAttack` 空城占领 + `TickBattles` 攻方胜占领）追加：若 `previousControllerCountry` 国家的首都被占（即 `world.countries[prev].capitalProvinceId == provinceId`）→ 该国 warSupport -= eco.warSupportPenaltyPerCapitalLoss。

- `Simulation/TurnResolver.cs`：注入 `WarTollResolver`；`ExecuteSettlement` 在 `_battle.TickBattles(world)` 之后、`_victory.CheckVictory(world, _clock)` 之前调 `_warToll.ApplyTurnToll(world, eco)`。

### 3.4 Application
- `Application/Services/GameSessionService.cs`：
  - 注入 `PeaceResolver _peace`（构造函数加参，全 grep 同步）。
  - `IssueCommand` 加：
    ```csharp
    case CommandType.OfferPeace:
        return _peace.OfferPeace(_world, cmd.countryId, cmd.targetCountryId, eco);
    ```
- `Application/Queries/ReadModelBuilder.cs`：`BuildCountryView` 加 `warExhaustion = c.warExhaustion`。
- `Application/Persistence/SaveModels.cs`：`CountrySaveData` 加 `int warExhaustion;`。
- `Application/Mapping/SaveMapper.cs`：双向 warExhaustion + HashWorld 扩展。

### 3.5 Bootstrap
- `Bootstrap/GameLifetimeScope.cs`：注册 `WarTollResolver` + `PeaceResolver`；BattleResolver / TurnResolver / GameSessionService 构造签名变了——**grep 全工程同步**（C2b/C4 两次教训）。

### 3.6 Presentation
- `MainHudController`：
  - 国家行（FormatCountryRow）加：玩家国与该国 AtWar 时追加 `战疲: N/100`（仅敌方且 AtWar 显）。
  - 选中国家不是玩家自己 + AtWar → 显"📜 提议停战"按钮：发 `OfferPeace { countryId=playerCountryId, targetCountryId=该国 id }`。
  - 订阅 `PeaceOfferedEvent`：accepted → 状态栏 `🕊 与 {to} 停战达成`；rejected → `📜 {to} 拒绝停战（{reason}）`。
  - 订阅 `PeaceConcludedEvent`：忽略（已由 PeaceOfferedEvent 覆盖文案）。
  - 详情栏选中己方国家时（如有 UI 入口；当前没有则跳过）显示 `战疲: {warExhaustion}/100`。**最小化实现**：国家行右侧追加文字即可，不必新增 UI 区。
  - HUD 玩家行也显 `战疲: N/100`（与税率/民生平级）。
- `Assets/UI/MainHud.uss`：加 `.peace-button { background-color: rgba(100,180,100,0.8); ... }`（小绿按钮，与攻击红、移动绿区分）。

### 3.7 Test Stubs / Helpers
- 测试中 `BattleResolver` 构造改用 `new BattleResolver(rng, events, config)`（旧测试 config 传 null 即可，因 ApplyBattleToll 已 null-safe）。注意 grep 全工程 `new BattleResolver(`。

## 4. 测试

### EditMode — `WarTollResolverTests`（新）
- `ApplyTurnToll_AtWarCountries_StabilityAndExhaustionUpdate`：A vs B AtWar → 一次 tick → 双方 stability -1、warExhaustion +1。
- `ApplyTurnToll_PeaceCountries_Unchanged`：无 WarRelation → tick → 数值不变。
- `ApplyTurnToll_StabilityClampedAtZero`：stability=0 → tick → 仍 0（不变负）。
- `ApplyTurnToll_ExhaustionClampedAtHundred`：warExhaustion=100 → tick → 仍 100。
- `ApplyTurnToll_DeterministicOrder`：3 国 AtWar id=z/a/m → tick 内部按 a/m/z 顺序处理（用事件/日志验证）。

### EditMode — `PeaceResolverTests`（新）
- `OfferPeace_BothExhaustedWeakerAccepts`：me.warExhaustion=30 + myPower=80% theirPower → Accept、WarRelation 移除、warExhaustion 减半。
- `OfferPeace_ExtremelyExhaustedAccepts`：me.warExhaustion=60（≥ threshold×2）→ Accept 无视实力。
- `OfferPeace_StrongAndFreshRejects`：me.warExhaustion=10 + myPower=2×theirPower → Reject。
- `OfferPeace_NotAtWar_Rejects`：双方 Peace 状态 → rejected "双方未处于战争状态"。
- `OfferPeace_SameCountry_Rejects`：from==to → rejected "不能与自己停战"。
- `OfferPeace_NonExistentCountry_Rejects`：toCountry 不存在 → rejected "目标国不存在"。
- `OfferPeace_Success_PublishesBothEvents`：成功时 PeaceOfferedEvent(accepted=true) + PeaceConcludedEvent 都发。
- `OfferPeace_AcceptedAndOccupiedProvinceRemainsControlled`：A 占了 B 的省 → 停战 → B 的省 controllerCountry 仍 = A（**永久占领，本单核心写死**）。

### EditMode — `BattleResolverWarTollTests`（新或追加 BattleResolverC4Tests）
- `TickBattles_AttackerWins_WarSupportBonusAndPenalty`：A 攻 B 胜 → A.warSupport +5、B.warSupport -5。
- `TickBattles_DefenderWins_WarSupportBonusAndPenalty`：A 攻 B 败 → B.warSupport +5、A.warSupport -5。
- `TickBattles_Draw_BothLoseSupport`：双方齐崩 → 双方 warSupport -5。
- `InitiateAttack_AttackerCapturesCapital_CapitalLossPenalty`：占领 B 的首都 → B.warSupport -15。
- `InitiateAttack_AttackerCapturesNonCapital_NoCapitalLossPenalty`：占领非首都 → 不扣 capital loss penalty。

### EditMode — `GameSessionServiceTests`（追加）
- `IssueCommand_OfferPeace_Valid_Accepts`：玩家发提议 → AI 接受 → WarRelation 移除。
- `IssueCommand_OfferPeace_NonPlayer_Rejects`：countryId != playerCountryId → rejected "非玩家国"。
- `IssueCommand_OfferPeace_NotAtWar_Rejects`。

### EditMode — `ReadModelBuilderTests`（追加）
- `BuildCountryView_WarExhaustion_PassedThrough`。

### EditMode — `SaveLoadEquivalenceTests`（追加）
- `SaveLoad_WarExhaustion_Preserved`：A.warExhaustion=15 → 存 → 读 → hash 等价 + 字段持久。
- `SaveLoad_PeaceConcluded_Preserved`：宣战 → 停战 → 存 → 读 → warRelations 空 + 双方 warExhaustion 减半状态持久。
- HashWorld 扩展含 warExhaustion（已为 country 字段，按 id 升序写）。

### EditMode — `ConfigValidationTests`（追加）
- `Economy_HasWarFields`：检查 7 个新字段都 >= 0（warStabilityPenaltyPerTurn ≥ 0、warExhaustionPerTurn ≥ 0、warSupportPenaltyPerLoss ≥ 0、warSupportBonusPerVictory ≥ 0、warSupportPenaltyPerCapitalLoss ≥ 0、aiPeaceAcceptExhaustionThreshold ≥ 1、aiPeaceAcceptPowerRatioPct ≥ 1）。

### PlayMode — `MvpSmokeTests`（追加 1 用例）
- `WarToll_AtWarStabilityDecreases`：玩家攻邻省发起战争 → 推 N 回合 → 玩家国 stability 单调下降（容忍税率/民生影响，可断言 stability ≤ 初始）。或更稳：直接 reflection 调 WarTollResolver 验证（避免 PlayMode 时序 flaky）。

## 5. 文件清单
| 动作 | 路径 |
|---|---|
| 改 | `Domain/Country.cs`（+warExhaustion）、`Domain/Config/EconomyConfig.cs`（+7 字段）、`Domain/WarRegistry.cs`（+TryEndWar） |
| 新增 | `Simulation/WarTollResolver.cs`、`Simulation/PeaceResolver.cs` |
| 改 | `Simulation/BattleResolver.cs`（+ApplyBattleToll + 首都丢失扣 warSupport + 构造加 IConfigRegistry）、`Simulation/TurnResolver.cs`（注入 WarTollResolver + Settlement 调用顺序） |
| 新增 | `Contracts/Events/PeaceOfferedEvent.cs`、`Contracts/Events/PeaceConcludedEvent.cs` |
| 改 | `Contracts/Commands/CommandType.cs`（+OfferPeace）、`Contracts/Commands/GameCommand.cs`（+targetCountryId）、`Contracts/ReadModels/CountryView.cs`（+warExhaustion） |
| 改 | `Application/Services/GameSessionService.cs`（注入 PeaceResolver + IssueCommand 分支）、`Application/Queries/ReadModelBuilder.cs`（warExhaustion 透传） |
| 改 | `Application/Persistence/SaveModels.cs`（CountrySaveData +warExhaustion）、`Application/Mapping/SaveMapper.cs`（双向 + HashWorld 扩展） |
| 改 | `Bootstrap/GameLifetimeScope.cs`（注册新 Resolver + 构造签名同步） |
| 改 | `Presentation/MainHudController.cs`（战疲显示 + 提议停战按钮 + 事件订阅状态栏）、`Assets/UI/MainHud.uss`（+ `.peace-button`） |
| 改 | `Assets/StreamingAssets/Configs/Json/economy.json`（+7 字段，值见 §1） |
| 新增/改 | 上述测试文件 |

## 6. 验收门禁（DoD Check List）

**OpenClaw 必须在 PR 描述中逐项打 `[x]` 或写明豁免理由**。缺一项不可合并。

- [ ] **Phase 0 C4 收尾验证通过**（PROJECT_STATE/CHANGELOG main 上是 C3 末尾、5 单 artifact + 5 张关键截图齐全）
- [ ] **起点全绿**（EditMode + PlayMode 跑通 C4 终态）
- [ ] **EditMode 全绿**：C4 终态 + 本单新增（约 23 个：WarToll 5 + Peace 8 + BattleWarToll 5 + Session 3 + ReadModel 1 + SaveLoad 2 + ConfigValidation 1 = 25 实际约 23-25 区间）
- [ ] **PlayMode 全绿**：C4 终态 + 本单 +1
- [ ] **Play 截图归档**：`Design/screenshots/c5-war-exhaustion.png`（推几回合后看战疲数字增长）+ `c5-peace-offered.png`（点提议按钮 + 状态栏反馈）+ `c5-peace-concluded.png`（停战后地图 ⚔ 标记消失）三张
- [ ] **续跑等价**：宣战 → 推 5 回合（warExhaustion 累积）→ 存 → 读 → hash 等价 + 字段持久
- [ ] **artifacts 归档**：`artifacts/c5-editmode.xml` + `artifacts/c5-playmode.xml`
- [ ] **Unity Console 0 error**（USS 2 条 text-align warning 豁免）
- [ ] **batchmode 0 error**
- [ ] **diff 干净**：`PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md / ProjectSettings/* / Packages/packages-lock.json` 全部 0 改动
- [ ] **既有公式不动**：未改 BattleResolver float 战斗公式 / PoliticsResolver 既有 stability 修改公式（仅新增 WarTollResolver 不污染）/ EconomyResolver / AIResolver / MovementResolver / ConstructionResolver / UnitProductionResolver / VictoryConditionResolver
- [ ] **构造签名 grep 同步**：BattleResolver / TurnResolver / GameSessionService 三个构造改了，必须 grep `new BattleResolver(` + `new TurnResolver(` + `new GameSessionService(` 全工程匹配数，含测试每处都改
- [ ] **数值仅 economy.json 7 字段**：未改 units.json / countries.json / provinces.json / resources.json
- [ ] **PR 描述含 `## DoD Check List`**：本清单逐项打勾

## 7. 歧义处理

- **被占省是否归还**：本单写死**永久划归占领方**（停战后 controllerCountry 不变）。割地谈判 / 归还省份留 C6+。
- **停战后立即再宣战**：本单**允许**（无冷却）。中立化承诺留 C6+。
- **AI 主动提议停战**：本单**不做**（只玩家可发 OfferPeace）。C6+ 加 AI 周期检查。
- **三方混战的停战**：本单 OfferPeace 是双边的（A↔B 停战不影响 A 与 C 的战争）。
- **停战后双方 warExhaustion 减半**是为了"刚停战就再开打也不会瞬间过载"——若觉得过宽人类可在 PR 期间调（如改"清零"或"减 70%"）。
- **首都丢失 warSupport 扣减**是**一次性**（仅占领瞬间触发）——后续多次易主不重复扣。本单写死。
- **AI 决定接受/拒绝时玩家不知道实力对比**：UI 不显示 AI 国国力（信息透明度是 C6+ 决策点）。状态栏 reason 写"对方拒绝（实力优势/疲惫不足）"模糊提示。
- **同省多方部队战斗收尾的 warSupport** 仅按主战双方算（C3 同省多守方"清场"消灭的不算独立战败）。**写死**：ApplyBattleToll 只对主战双方扣。
- **OpenClaw 严禁**：① 改 governance 文件 ② 改 ProjectSettings/Packages ③ 改 PoliticsResolver 既有公式 ④ 改 BattleResolver 既有 float 战斗公式 ⑤ 改 EconomyResolver 维护费/产出公式 ⑥ 加 AI 调防/撤退/造兵优化（C5b/C6+）⑦ 加双边关系等级（C6+）⑧ 实现割地赔款（C6+）⑨ 跳过明确指派的测试用例（"功能等价就不写"）⑩ 用 commit 列表/文字描述替代 Play 截图。
- **若发现既有 bug**：单独写 `[需 Claude 决策]` 报告，不擅自修。
- **reason 字符串保持简短中文一致**："双方未处于战争状态" / "不能与自己停战" / "目标国不存在" / "发起国不存在" / "对方拒绝停战"。测试用 `Assert.AreEqual` 严格匹配。

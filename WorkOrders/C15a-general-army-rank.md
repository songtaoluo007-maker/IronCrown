# C15a — 将军卡 + 集团军 + 同省容量 + 军衔晋升

## 背景
Phase 1 战斗系统至 C14 已完整（师级 + 双条 + 经验 + 补给 + 切断/包围/解围）。C15a 引入**将军卡机制**——单机抽卡养成的核心载体（C16 才做抽卡 UI）。

每张将军卡 = 一个英雄单位，统帅麾下 1-5 师组成集团军；同省最多 5 师容量上限。军衔（少→中→上→大→帅 5 阶）由战役胜场累积晋升。

**与 C13 战役等级双层独立**：战役经验在师上、军衔在将军上。

时间尺度：1 回合 = 1 月。军衔晋升节奏按月战役胜场。

## 范围

### 五件齐做
| 件 | 描述 |
|---|---|
| **GeneralState 数据结构** | id / ownerCountry / name / rank / victoryCount / commandedDivisionIds 等 |
| **招募命令 + 任命命令** | RecruitGeneral（消耗 capital+manpower）+ AssignDivisionToGeneral（绑定师到将军麾下） |
| **同省 5 师容量** | MovementResolver/BattleResolver 移动到目标省时校验 + 拒绝超过 |
| **军衔晋升 + 战斗 buff** | 战役胜累积 victoryCount → 阈值晋升 → 麾下师上限 +1 + 战斗 attack/defense buff +5% |
| **测试卡 + HUD** | C15a 只内置 1 张测试将军卡 `general_test_basic`（C15b 才做完整 12 张原创卡）+ HUD 将军行显示 |

### 配置（economy.json）
```json
{
  "maxDivisionsPerProvince": 5,
  "generalRecruitCostCapital": 100,
  "generalRecruitCostManpower": 500,
  "rankPromotionThresholds": [5, 15, 35, 75],
  "rankPromotionEncirclementBonus": 3,
  "rankBattleBonusPerLevel": 5
}
```

### 军衔晋升数学
```
victoryCount 累积:
  普通战役胜 → +1
  包围歼敌 (TickBattles 攻方胜利且守方 isCutoff=true) → +rankPromotionEncirclementBonus (=3)

晋升阶梯 (rank 0=少将 → 4=元帅):
  少将  (rank 0): victoryCount < 5
  中将  (rank 1): victoryCount >= 5
  上将  (rank 2): victoryCount >= 15
  大将  (rank 3): victoryCount >= 35
  元帅  (rank 4): victoryCount >= 75

每 rank +1 = 麾下师上限 +1（少将=1 → 元帅=5）
每 rank +1 = 战斗 attack +5% + defense +5%
```

### 战斗 buff 集成（BattleResolver C12 整数公式 + 单师战力扩展）
```csharp
int singleUnitAttackPower(UnitState unit, EconomyConfig eco, WorldState world) {
    int orgPct = unit.organization * 100 / Math.Max(1, unit.maxOrganization);
    int level = unit.tacticalExp / eco.tacticalExpLevelStep;
    int levelBonus = 100 + level * eco.tacticalExpAttackBonusPerLevel;  // C13
    int rankBonus = 100;  // C15a 新增
    if (unit.commandingGeneralId != null && world.generals.TryGetValue(unit.commandingGeneralId, out var general)) {
        rankBonus = 100 + general.rank * eco.rankBattleBonusPerLevel;  // 100, 105, 110, 115, 120
    }
    return unit.baseAttack * orgPct * levelBonus * rankBonus / 1_000_000;  // 缩放回原量级
}
```
**关键**：单师同时受两层 buff——战役等级（师独立）+ 军衔（来自统帅将军）。

### 测试将军卡 `general_test_basic`（C15a 内置）
```json
{
  "id": "general_test_basic",
  "name": "测试将军",
  "rarity": "R",
  "description": "C15a 测试用，C15b 替换为完整 12 卡",
  "skills": []
}
```

C15a 不开放抽卡 UI——玩家通过命令直接招募该测试卡（即固定模板招募）。C15b 才开放多卡 + 历史差异化技能。

### 招募流程
```csharp
RecruitGeneral 命令处理:
  if (cmd.commandType == CommandType.RecruitGeneral):
    if (玩家国 capital < generalRecruitCostCapital) → reject "资本不足"
    if (玩家国 manpower < generalRecruitCostManpower) → reject "人力不足"
    扣 capital + manpower
    新建 GeneralState:
      id = "{country}_general_{seq}"
      ownerCountry = playerCountryId
      name = "测试将军 {seq}"
      rank = 0  // 少将
      victoryCount = 0
      commandedDivisionIds = []
      generalCardId = "general_test_basic"
    world.generals[id] = newGeneral
    country.generalIds.Add(id)
    发 GeneralRecruitedEvent
```

### 任命流程
```csharp
AssignDivisionToGeneral 命令处理:
  if (cmd.commandType == CommandType.AssignDivisionToGeneral):
    var general = world.generals[cmd.generalId]
    var unit = world.units[cmd.unitId]
    if (general.ownerCountry != unit.ownerCountry) → reject "异国部队"
    int maxAllowed = general.rank + 1  // 少将=1，元帅=5
    if (general.commandedDivisionIds.Count >= maxAllowed) → reject "麾下已满"
    // 解绑旧统帅（如有）
    if (unit.commandingGeneralId != null):
      world.generals[unit.commandingGeneralId].commandedDivisionIds.Remove(unit.id)
    general.commandedDivisionIds.Add(unit.id)
    unit.commandingGeneralId = general.id
    发 DivisionAssignedEvent
```

### 同省 5 师容量校验
```csharp
// MovementResolver.TryMove 加：
int divisionsInTarget = world.units.Values.Count(u => 
    u.currentProvinceId == targetProvinceId && u.ownerCountry == attacker.ownerCountry);
if (divisionsInTarget >= eco.maxDivisionsPerProvince) 
    return CommandResult.Reject("目标省驻军容量已满（5 师上限）");

// BattleResolver.InitiateAttack 同样加
// 例外：被占领的瞬间空城进驻不受限（攻方进入即视为新驻军、应该允许）—— 写死跳过校验
```

### 军衔晋升自动触发（BattleResolver.TickBattles 收尾扩展）
```csharp
// C12 已有 TickBattles 收尾胜负判定
// C13 加了 tacticalExp 累积
// C15a 加：
if (result.attackerWon):
  foreach (var unitId in attackerSurvivors):
    var unit = world.units[unitId];
    if (unit.commandingGeneralId == null) continue;
    var general = world.generals[unit.commandingGeneralId];
    int gain = 1;
    bool wasEncirclement = defenderUnitsInBattle.Any(d => world.units.ContainsKey(d) && world.units[d].isCutoff);
    if (wasEncirclement) gain = eco.rankPromotionEncirclementBonus;  // +3
    general.victoryCount += gain;
    int oldRank = general.rank;
    general.rank = ComputeRank(general.victoryCount, eco.rankPromotionThresholds);
    if (general.rank > oldRank) _events.Publish(new GeneralPromotedEvent { generalId = general.id, newRank = general.rank });
    
// 注意：同一将军麾下多支师都活下来时只算 1 次晋升（不是 N 次）—— 用 distinct general 集合
```

## 文件变更清单

### Domain
- 新建 `Domain/State/GeneralState.cs`：
  ```csharp
  public sealed class GeneralState {
      public string id;
      public string ownerCountry;
      public string name;
      public string generalCardId;          // 引用 generalCards.json (C15b 用)
      public int rank;                       // 0=少将 ... 4=元帅
      public int victoryCount;
      public List<string> commandedDivisionIds = new();
  }
  ```
- `Domain/Unit.cs` (`UnitState`) — 加 `string commandingGeneralId;`（默认 null）
- `Domain/Country.cs` (`CountryState`) — 加 `List<string> generalIds = new();`
- `Domain/State/WorldState.cs` — 加 `Dictionary<string, GeneralState> generals = new();`
- `Domain/Config/EconomyConfig.cs` — 加 6 个 C15a 字段
- 新建 `Domain/Config/GeneralCardConfig.cs`（简化版，C15b 才真正用）：
  ```csharp
  public sealed class GeneralCardConfig {
      public string id;
      public string name;
      public string rarity;          // "N" / "R" / "SR" / "SSR"
      public string description;
      public GeneralSkillEntry[] skills;
  }
  public sealed class GeneralSkillEntry {
      public string type;   // "defenseBonus" / "attackBonus" / "supplyConsumptionReduction" etc
      public int value;
  }
  ```

### Simulation
- `Simulation/BattleResolver.cs`:
  - `singleUnitAttackPower / singleUnitDefensePower` 加 rankBonus（公式见 §战斗 buff 集成）
  - `TickBattles` 收尾加 victoryCount 累积 + 军衔晋升判定
- `Simulation/MovementResolver.cs` — TryMove 加同省 5 师容量校验
- `Simulation/BattleResolver.InitiateAttack` — 加同省 5 师校验（攻方进入算驻军）

### Application
- `Application/Services/GameSessionService.cs` — IssueCommand 加 RecruitGeneral / AssignDivisionToGeneral 分支
- `Application/Queries/ReadModelBuilder.cs`:
  - BuildWorldView 加 generals 列表
  - BuildCountryView 加 generalCount
  - BuildUnitView 加 commandingGeneralId / commandingGeneralName / commandingGeneralRank
- `Application/Persistence/SaveModels.cs`:
  - 加 `GeneralSaveData { id, ownerCountry, name, generalCardId, rank, victoryCount, commandedDivisionIds[] }`
  - GameState 加 `GeneralSaveData[] generals`
  - UnitSaveData 加 `commandingGeneralId`
  - CountrySaveData 加 `generalIds[]`
- `Application/Mapping/SaveMapper.cs`:
  - 双向 generals + commandingGeneralId + generalIds
  - HashWorld 扩 generals（按 id 升序写 id/rank/victoryCount/commandedDivisionIds.Count）+ commandingGeneralId

### Contracts
- `Contracts/Commands/CommandType.cs` — 加 `RecruitGeneral` / `AssignDivisionToGeneral`
- `Contracts/Commands/GameCommand.cs` — 加 `string generalId;`（共用 cmd.unitId 作为师 id）
- 新建 `Contracts/Events/GeneralRecruitedEvent.cs` — `{ generalId, ownerCountry, atTurn }`
- 新建 `Contracts/Events/GeneralPromotedEvent.cs` — `{ generalId, newRank, atTurn }`
- 新建 `Contracts/Events/DivisionAssignedEvent.cs` — `{ generalId, divisionId }`
- 新建 `Contracts/ReadModels/GeneralView.cs` — 同 GeneralState 字段
- `Contracts/ReadModels/WorldView.cs` — 加 `List<GeneralView> generals;`
- `Contracts/ReadModels/UnitView.cs` — 加 `string commandingGeneralId / commandingGeneralName / int commandingGeneralRank`
- `Contracts/ReadModels/CountryView.cs` — 加 `int generalCount;`

### Bootstrap
- `Bootstrap/GameLifetimeScope.cs` — IConfigRepository 加载 GeneralCardConfig (`generalCards.json`)
- 数据驱动 ConfigRegistry.LoadAll 自动 LoadTable<GeneralCardConfig>

### Data
- `economy.json` — 加 6 个 C15a 字段
- 新建 `Assets/StreamingAssets/Configs/Json/generalCards.json`：
  ```json
  {
    "schemaVersion": 1,
    "items": [
      {
        "id": "general_test_basic",
        "name": "测试将军",
        "rarity": "R",
        "description": "C15a 测试用，C15b 替换为完整 12 卡",
        "skills": []
      }
    ]
  }
  ```

### Presentation
- `MainHudController`:
  - 玩家行追加 `将领: {generalCount}` 数量
  - HUD 详情栏（选中省含部队）追加 `统帅: {commandingGeneralName}（{rankName}）` 如 "测试将军 1（中将）"
  - 加按钮 "招募将军"（消耗 capital+manpower）→ 发 RecruitGeneral 命令
  - 状态栏订阅 GeneralRecruitedEvent / GeneralPromotedEvent
- `MainHud.uxml` — 加 `recruit-general-btn`
- `MainHud.uss` — 沿用现有按钮样式

### Tests
- 新建 `GeneralStateTests.cs`：
  - `ComputeRank_LessThan5Victories_StaysMajor`（rank 0）
  - `ComputeRank_5Victories_PromotesToLtGen`（rank 1）
  - `ComputeRank_75Victories_PromotesToMarshal`（rank 4）
- 新建 `RecruitGeneralTests.cs`：
  - `RecruitGeneral_CapitalSufficient_CreatesGeneralAndDeductsCost`
  - `RecruitGeneral_CapitalInsufficient_Rejects`
  - `RecruitGeneral_ManpowerInsufficient_Rejects`
- 新建 `AssignDivisionTests.cs`：
  - `AssignDivision_DivisionAndGeneralSameCountry_Succeeds`
  - `AssignDivision_DifferentCountries_Rejects`
  - `AssignDivision_GeneralAtRankLimit_Rejects`（少将麾下已 1 师再加 → 拒）
  - `AssignDivision_RebindToNewGeneral_RemovesFromOldGeneral`
- 新建 `ProvinceCapacityTests.cs`：
  - `MoveUnit_TargetProvinceFull_Rejects`（5 师上限触发）
  - `MoveUnit_TargetProvinceHas4Divisions_Succeeds`
- 新建 `BattleRankBonusTests.cs`：
  - `BattleResolver_GeneralRank3_Adds15PercentAttackBuff`
  - `BattleResolver_NoGeneral_NoRankBonus`
- 新建 `RankPromotionTests.cs`：
  - `Victory_NormalBattle_VictoryCountIncrementsBy1`
  - `Victory_EncirclementBattle_VictoryCountIncrementsBy3`
  - `Victory_CrossesThreshold_AutoPromotesAndPublishesEvent`
- `SaveLoadEquivalenceTests.cs` 追加：
  - `SaveLoad_GeneralWithCommandedDivisions_Preserved`
  - HashWorld 扩 generals 字段
- `ConfigValidationTests.cs` 追加：
  - `Economy_HasC15aFields`
  - `GeneralCards_AllReferencedSkillTypesValid`（C15a 仅 1 卡无 skill，主要检 schema 解析）

## DoD Check List
- [ ] GeneralState + GeneralCardConfig + GeneralView + 3 Events 齐全
- [ ] WorldState.generals + CountryState.generalIds + UnitState.commandingGeneralId 字段加入
- [ ] RecruitGeneral + AssignDivisionToGeneral 命令分发实现
- [ ] BattleResolver 战力公式集成 rankBonus（+0/5/10/15/20%）
- [ ] TickBattles 收尾累积 victoryCount + 自动晋升 + 包围奖励 ×3
- [ ] MovementResolver + BattleResolver.InitiateAttack 同省 5 师容量校验
- [ ] economy.json + generalCards.json (1 卡测试用) 齐全
- [ ] HUD 加招募按钮 + 玩家行将领数 + 详情栏统帅显示
- [ ] SaveMapper 双向 + HashWorld 扩 generals + commandingGeneralId
- [ ] 既有 269+ EditMode + 7 PlayMode 全绿 + 本单新增（约 18 个）全绿
- [ ] **★ commit 完成后立即 `git push origin feature/c5-diplomacy-peace`**（PR #1 自动追加，C13/C14 一度好转、本单保持）
- [ ] artifacts/c15a-editmode.xml + c15a-playmode.xml 归档（**只一对**）
- [ ] **Play 截图 3 张强制**（14 单累积 0 张，C15a 必破纪录）：
  - `c15a-recruit-general.png`（招募后 HUD 玩家行显示"将领: 1"）
  - `c15a-assign-division.png`（任命某师到将军麾下，详情栏显"统帅: 测试将军 1（少将）"）
  - `c15a-rank-promotion.png`（多次战役胜后将军军衔从少将升中将）
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 0 failed
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] 不动 EconomyResolver / SupplyResolver / WarTollResolver / PeaceResolver / AiPeaceOfferResolver / OccupationResolver / VictoryConditionResolver / AIResolver / ConstructionResolver / UnitProductionResolver / AiRedeploymentResolver / WarRegistry / PoliticsResolver 公式
- [ ] BattleResolver C12 整数化 + C13 经验加成不退化
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 不做（C15b / C16+）
- 完整 12 张原创将军卡 + 历史差异化技能（armor_specialist / cavalry_master 等）→ C15b
- 抽卡 + N/R/SR/SSR + 升星 → C16
- gachaTickets 货币 → C16
- 商城 → C17
- 将军阵亡 / 退役机制 → C18+
- AI 自动招募/任命将军 → C18+
- 将军个性化外貌 / 卡面美术 → D 美术
- 集团军移动作为整体调度（C15a 玩家仍单师下令、绑定后将军跟随首师位置）→ C19+

## 严禁
- 改 BattleResolver C12 整数公式（仅在公式末尾扩 rankBonus 乘数）
- 改 EconomyResolver / WarTollResolver / PeaceResolver / SupplyResolver 既有公式
- 加 AI 招募/任命（玩家专属，AI 当前无将军体验）
- 多张测试将军卡（C15a 仅 1 张，C15b 才加多张）
- 加抽卡 UI / 升星 / 稀有度概率（C16 才做）
- 用 commit log / txt 替代 Play 截图
- 不 push

## 歧义处理
- **统帅死亡（师消灭）的将军处理**：本单**不死亡机制**——将军即使麾下师全消灭仍存在，麾下列表空。可重新 AssignDivision 任命新师。**写死**
- **同省 5 师容量是否含敌方部队**：**仅同阵营**（攻方占用与守方占用独立计数）。规则：`unit.ownerCountry == attacker.ownerCountry` 才计入容量
- **同省占领瞬间**：空城进驻不受 5 师上限（避免攻方进省被拒）。多对多团战胜利后清场→进省同样不限。**写死**
- **将军 buff 是否叠加多个 trait**：C15a 测试卡 skills=[] 无技能，C15b 多技能时叠加 sum。但 buff 影响每师独立计算（不互相叠加）
- **将军跟随哪个师移动**：本单**将军不显式位置**——通过 commandedDivisionIds 各师位置间接表达。详情栏选中省时若该省有该将军麾下任何师 → 显示"驻有 {将军名}"
- **解除任命 (UnassignDivision)**：本单**不做命令**——通过 AssignDivision 任命到新将军时自动从旧将军摘除即可。"放归个人指挥"留 C18+
- **军衔 buff 与战役经验 buff 是否相乘**：**相乘**（公式 attack × levelBonus × rankBonus / 1_000_000）。同一师在元帅 + 战役 4 级麾下 = +20% × +20% ≈ +44% 战力。这是 SR/SSR 卡的核心价值

## 完工后人类 Play 验证清单
1. 玩家国 capital > 100、manpower > 500 → 点"招募将军"按钮 → 状态栏 "✓ 招募新将军 {id}"，玩家行 "将领: 1"
2. 选某省含玩家师 → 点"任命该师统帅"（或某种 UI）→ 详情栏 "统帅: 测试将军 1（少将）"
3. 该师攻击邻省 → 胜利 → 将军 victoryCount +1
4. 累积 5 场胜 → GeneralPromotedEvent + 状态栏"测试将军 1 晋升中将"
5. 中将麾下上限 2 师 → 再任命第 2 师成功；再加第 3 师拒"麾下已满"
6. 玩家把 5 师都聚到高峰（4 邻枢纽）→ 第 6 师移动到高峰被拒"目标省驻军容量已满（5 师上限）"
7. 元帅麾下 5 师战斗 → 攻防比无将军时高 20%
8. 存档 → 读 → 将军 + 麾下绑定 + victoryCount 全部持久

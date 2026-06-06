# C15b — 12 张原创将军卡（数据驱动 + 技能系统）

## 背景
C15a 已建立 GeneralCardConfig + skills 数据结构，但只内置 1 张测试卡 `general_test_basic`（无技能）。C15b 填实 12 张原创卡（4 SSR / 4 SR / 3 R / 1 N），每张差异化技能——为 C16 抽卡提供卡池。

**避免版权**：不照搬历史人物名，全部原创虚构。

## 范围

### 12 张原创卡 + skills（写入 generalCards.json）
| ID | 名 | 稀有度 | 技能 |
|---|---|---|---|
| general_ironwall | 铁壁元帅 | SSR | defenseBonus +20、moraleBonus +15 |
| general_blitz | 突击先锋 | SSR | attackBonus +20、breakthroughBonus +15、defenseBonus -5 |
| general_armor_pioneer | 装甲先驱 | SSR | brigadeBonus(light_tank, attack) +25 |
| general_lightning | 闪电将军 | SSR | speedBonus +1、breakthroughBonus +20 |
| general_mountain_hunter | 山地猎手 | SR | terrainBonus(Mountain) +30、terrainBonus(Hills) +30 |
| general_fireman | 救火队员 | SR | cutoffDecayMultiplier 50（切断衰减 ×0.5）|
| general_logistics | 后勤大师 | SR | supplyConsumptionReduction +25、reinforceRateBonus +20 |
| general_plains_hound | 平原猎犬 | SR | terrainBonus(Plain) +15、terrainBonus(Coastline) +15 |
| general_veteran | 老将 | R | tacticalExpRateBonus +50（战役经验累积 +50%）|
| general_engineer | 防御工兵 | R | defenseBonus +10 |
| general_infantry_drill | 步兵教官 | R | brigadeBonus(infantry, attackDefense) +10 |
| general_basic_officer | 普通军官 | N | 无技能（占池子稀释概率）|

### Skill Schema (extension of C15a)
```json
{
  "skills": [
    { "type": "attackBonus",                "value": 20 },
    { "type": "defenseBonus",               "value": -5 },
    { "type": "moraleBonus",                "value": 15 },
    { "type": "breakthroughBonus",          "value": 15 },
    { "type": "speedBonus",                 "value": 1 },
    { "type": "brigadeBonus",               "brigadeType": "light_tank", "stat": "attack",        "value": 25 },
    { "type": "brigadeBonus",               "brigadeType": "infantry",   "stat": "attackDefense", "value": 10 },
    { "type": "terrainBonus",               "terrain": "Mountain",       "value": 30 },
    { "type": "cutoffDecayMultiplier",      "value": 50 },
    { "type": "supplyConsumptionReduction", "value": 25 },
    { "type": "reinforceRateBonus",         "value": 20 },
    { "type": "tacticalExpRateBonus",       "value": 50 }
  ]
}
```

**12 个 type 全部需在 Domain/Config 解析**，未实现的 type 不在本单生效但 schema 必须解析无错。

## 文件变更清单

### Domain
- `Config/GeneralCardConfig.cs` — `GeneralSkillEntry` 加可选字段 `brigadeType / stat / terrain`
- 新建 `Simulation/CommanderSkillEvaluator.cs`（静态工具）：根据 commander.generalCardId 查模板、按 skills 列表对师战力 / morale / supply / reinforce / terrain 影响做相应 buff 计算

### Simulation
- `BattleResolver.cs` — 单师战力公式扩展（在 C13 levelBonus + C15a rankBonus 之后）加 `cardSkillBonus`：
  ```csharp
  int cardAtkPct = CommanderSkillEvaluator.EvalAttack(commander, unit, province, world);
  int cardDefPct = CommanderSkillEvaluator.EvalDefense(commander, unit, province, world);
  // 最终战力 = base × orgPct × levelBonus × rankBonus × cardAtkPct / 100^4 缩放
  ```
- `SupplyResolver.cs` — 在 ReplenishUnits 时用 `CommanderSkillEvaluator.EvalReinforceRate(commander)` 调整 reinforceRatePct；切断衰减按 `EvalCutoffDecayMultiplier` 调整（救火队员让 -30 变 -15）

### Data
- `Assets/StreamingAssets/Configs/Json/generalCards.json` — 12 张卡（**替换** C15a 的 1 张测试卡，保留 `general_test_basic` 也可）

### Tests
- 新建 `CommanderSkillEvaluatorTests.cs`：12 个核心技能各 1 测试（如 `Ironwall_DefensePlus20` / `Blitz_AttackPlus20DefenseMinus5` / `ArmorPioneer_OnlyLightTankBrigade` / `MountainHunter_OnlyMountainTerrain` / `Fireman_CutoffDecayHalved` 等）
- `ConfigValidationTests.cs` 追加：
  - `GeneralCards_TwelveCardsLoaded`
  - `GeneralCards_RarityDistribution_4SSR_4SR_3R_1N`
  - `GeneralCards_AllSkillTypesParseable`

## DoD Check List
- [ ] generalCards.json 含 12 张卡 + general_test_basic（13 项）
- [ ] CommanderSkillEvaluator 解析 12 个 type 无错（即使部分 type 暂不影响战力 = 占位但 schema OK）
- [ ] BattleResolver 单师战力公式加 cardSkillBonus 乘数（在 C12/C13/C15a 之后、最终缩放前）
- [ ] SupplyResolver 用 EvalReinforceRate + EvalCutoffDecayMultiplier
- [ ] 既有 269+ EditMode + 7 PlayMode 全绿 + 本单新增（约 15 个）全绿
- [ ] **★ commit 完成立即 push**
- [ ] artifacts/c15b-editmode.xml 归档（只一对）
- [ ] **Play 截图 1 张** `c15b-twelve-cards.png`（开发模式逐个切换将军卡观察战斗 buff 变化）
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] PR 描述含 DoD Check List

## 不做
- 抽卡逻辑（C16）
- 升星（C16）
- gachaTickets 货币（C16）
- 商城（C17）
- 卡牌美术（D 阶段）
- AI 用将军卡技能（玩家专属，AI 仍按 C15a 测试卡机制）

## 严禁
- 改 C12/C13/C15a 既有公式（仅在公式末尾扩 cardSkillBonus）
- 改军衔名（C15a-fix 已定 少→帅）
- 卡牌名不能照搬历史人物（巴顿/隆美尔/朱可夫等）—— 严守原创
- 加 SSR 卡的 attackBonus > 30（避免数值膨胀，最强 SSR = +25）

# C10 — 货币清理：treasury 激活 + 装备库存激活

## 背景
人类真玩反馈："国库 -8854 仍能造东西"——治死字段。Claude 设计审计：
- `treasury` 累积 netIncome（taxIncome - 维护费）但**建造扣 capital 不扣 treasury** → treasury 数字飞涨/暴跌都不影响游戏
- `country.equipmentStockpile` 累积军工产装备但**训练步兵不消耗** → 装备库存增长无意义
- `UnitState.equipment` 字段满编 100 但**战损不补员**（既有 Reinforce 方法 0 调用）

C10 = Phase 1 第一单，**治这两个死字段**。维护费 + 人力恢复 + 补员留 C13。

## ⚠ 附带 C9 收尾（人类玩第二局发现，本单顺手做）

人类 27 回合通关验证 C9 系列后两个小尾巴：
1. **钢铁仍卡 1**——C9d militaryFactoryEquipmentOutput=2 节流不够。算式：N 军工 × 2 装备 × 2 steel = 4N steel/回合消耗，N≥3 净亏。**本单顺手再降一档**：`militaryFactoryEquipmentOutput: 2 → 1`（每军工/回合产 1 装备消耗 2 steel，N=5 军工 = 10 steel 消耗 vs iron_city+5 省基础 ≈ 11 产出 → 净 +1 长期可累积）
2. **抵抗度详情 UI 缺失**——C9b §3.3 要求 `RenderProvinceDetail` 在 `isOccupied=true` 时显 `抵抗: N/100`，OpenClaw 漏做。**本单顺手补一行 Append**。

## 范围

### 双激活
| 改动 | 描述 | 数值 |
|------|------|------|
| **treasury → capital 自动转化** | 每回合 ResolveEconomy 尾段 `capital += treasury × ratePct / 100; treasury -= 转化量`；treasury < 0 时不转化 | treasuryToCapitalRatePct=10（10%/回合）|
| **装备库存加入训练 cost** | UnitProductionResolver.TryEnqueue 校验 `country.equipmentStockpile >= unit.equipmentTrainingCost`；扣减 | infantry 训练 cost.equipment=50 |
| **训练消耗装备类型** | 仅消耗 unit.equipmentType 对应库存（当前 equipmentStockpile 是单一池子，本单不细分；C13 再细分多兵种装备类型）| — |

### 配置 (economy.json + units.json)
```json
// economy.json 加
{
  "treasuryToCapitalRatePct": 10
}

// units.json infantry 加字段
{
  "id": "infantry",
  ...既有...,
  "equipmentTrainingCost": 50  // 训练 1 师/团步兵消耗 50 装备
}
```

### 设计哲学
- **treasury 真有用了**：国库越大 → 每回合越多 capital 注入 → 越能建造扩张；玩家要重视税收
- **装备库存激活**：军工厂产装备从"显示数字"变成"训练前提"；玩家必须军工厂 + 训练同步规划
- **货币流闭环**：
  ```
  税收 → treasury → (10%/回合) → capital → 建造工厂 + 训练部队（消耗装备库存）
                                              ↑
                                       军工厂 → 装备库存
  ```

## 文件变更清单

### Domain
- `EconomyConfig.cs` — 加 `int treasuryToCapitalRatePct;`
- `UnitConfig.cs` — 加 `int equipmentTrainingCost;`

### Simulation
- `EconomyResolver.cs` — `ResolveEconomy` 尾段（line 95 `country.treasury += result.netIncome` 之后）追加：
  ```csharp
  if (country.treasury > 0 && eco != null)
  {
      int conversion = country.treasury * eco.treasuryToCapitalRatePct / 100;
      if (conversion > 0)
      {
          country.treasury -= conversion;
          country.ModifyResource("capital", conversion);
      }
  }
  ```
- `UnitProductionResolver.cs` — `TryEnqueue` 校验链加（在 manpower 校验之后、扣减之前）：
  ```csharp
  if (template.equipmentTrainingCost > 0 && country.equipmentStockpile < template.equipmentTrainingCost)
      return CommandResult.Reject("装备库存不足");
  ```
  扣减块加：
  ```csharp
  if (template.equipmentTrainingCost > 0)
      country.equipmentStockpile -= template.equipmentTrainingCost;
  ```

### Data
- `economy.json` — 加 `treasuryToCapitalRatePct: 10` + **改 `militaryFactoryEquipmentOutput: 2 → 1`**（C9 附带收尾）
- `units.json` — infantry 加 `equipmentTrainingCost: 50`（其他兵种暂不加，仅 infantry 当前可造）

### Application
- `ReadModelBuilder.cs` — `CountryView` 已含 treasury + equipmentStockpile（B1 起），无变
- `SaveModels.cs` / `SaveMapper.cs` — equipmentStockpile 已在 CountrySaveData（T5 起），无变
- `HashWorld` — 已含 treasury（T5）+ equipmentStockpile（C5），无变

### Presentation
- `MainHudController.cs` — `FormatCountryRow` 既有 `国库:{treasury}` 和 `装备:{equipmentStockpile}` 已显示，**确认无遮挡** + 玩家行追加 `资本投资率: {treasuryToCapitalRatePct}%`（让玩家直观看到 treasury→capital 转化每回合多少）
- 详情：玩家国 HUD 顶栏可选追加 `📈 投资: +N capital/回合`（计算 = `treasury * rate / 100`，让玩家看到本回合 treasury 会进多少 capital）—— **可选**，最小实现只在 country row 显示数值
- **★ C9 附带收尾**：`MainHudController.RenderProvinceDetail` 在 `pv.isOccupied == true` 分支末尾追加：
  ```csharp
  sb.Append($"  |  抵抗: {pv.resistance}/100");
  ```
  位置：在既有 `法理: X / 控制: Y` 之后。仅占领省显示（沿用 C9b 工作单 §3.3 原设计）。

### Tests
- `EconomyResolverTests.cs` 追加：
  - `ResolveEconomy_PositiveTreasury_ConvertsToCapital`：treasury=1000、rate=10 → 转 100 → treasury=900 / capital +100
  - `ResolveEconomy_ZeroTreasury_NoConversion`：treasury=0 → 不变
  - `ResolveEconomy_NegativeTreasury_NoConversion`：treasury=-500 → 不转化（防止负 capital）
  - `ResolveEconomy_ConversionFloors_NotFractional`：treasury=5、rate=10 → 转 0（整数除法）
- `UnitProductionResolverTests.cs` 追加：
  - `TryEnqueue_InsufficientEquipment_Rejects`：equipmentStockpile=49、cost=50 → rejected "装备库存不足"
  - `TryEnqueue_SufficientEquipment_DeductsAndAccepts`：equipmentStockpile=100 → accept、扣 50 → stockpile=50
  - `TryEnqueue_ZeroEquipmentCost_NoCheck`：equipmentTrainingCost=0（其他兵种 / 老 fixture）→ 不校验装备
- `ConfigValidationTests.cs` 追加：
  - `Economy_HasTreasuryToCapitalRate`：字段存在 ∈ [0, 100]
  - `Units_InfantryHasEquipmentTrainingCost`：infantry.equipmentTrainingCost > 0

## 不做（留 C11+）
- 维护费扩展（部队 unitUpkeep）→ C13
- 人力恢复 / 部队补员 → C13
- 装备类型细分（infantry_gear vs artillery_gear）→ C11/C13
- treasury 投资率玩家可调档（0%/10%/30%）→ C14 决议系统
- 破产事件（treasury < -N 触发 stability 扣）→ C14
- 自动 capital → treasury 反向转化（增加灵活性但太多概念）

## DoD Check List
- [ ] EconomyResolver 改动仅 1 段（治 1 死字段）
- [ ] UnitProductionResolver 改动 2 处（校验 + 扣减）
- [ ] economy.json + units.json 数值变更与 §配置完全一致
- [ ] 既有公式 0 改动：BattleResolver / PoliticsResolver / WarTollResolver / PeaceResolver / AiPeaceOfferResolver / OccupationResolver / VictoryConditionResolver / AIResolver / MovementResolver / ConstructionResolver / AiRedeploymentResolver / SupplyResolver
- [ ] EconomyResolver 既有"省份产出 / 军工产装备 / 民厂产 capital / 税收 netIncome" 0 改动
- [ ] UnitProductionResolver 既有"steel/food/capital 资源校验" 0 改动
- [ ] 测试覆盖：见 §Tests 全部存在且全绿
- [ ] artifacts/c10-editmode.xml + c10-playmode.xml 归档
- [ ] Play 截图 2 张：
  - `c10-treasury-flow.png`（HUD 显示玩家国 treasury 数值 + capital 数值，推 5 回合后两者数字变化清晰）
  - `c10-training-needs-equipment.png`（装备库存 < 50 时点训步兵 → 状态栏被拒"装备库存不足"）
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 0 failed
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] PR 描述含 `## DoD Check List` 逐项打勾
- [ ] **C9 附带收尾**：`militaryFactoryEquipmentOutput: 2 → 1` 写入 economy.json
- [ ] **C9 附带收尾**：`RenderProvinceDetail` 占领省显抵抗度（截图为证 `c10-occupation-resistance-shown.png`，验证人类截图过的高峰[首都] 现在显 `抵抗: N/100`）

## 严禁
- 改 capital 概念（仍是建造主货币）
- 改既有维护费公式（C13 才做扩展）
- 加 treasury 投资率玩家档位（C14）
- 加破产事件（C14）
- 删除 equipmentStockpile 字段或改名（既有存档兼容性）
- 给其他兵种（artillery/tank/aircraft）加 equipmentTrainingCost（C11 师级改造后再加）
- 跳过指派测试

## 歧义处理
- **treasury 转化向下取整 vs 四舍五入**：写死**向下取整**（整数除法），与既有所有数值处理一致
- **conversion = 0 时不修改 treasury**：保持 treasury 数值不变（即 treasury=5、rate=10 → conversion=0 → treasury 仍 5），下回合 treasury 增长后或可触发转化
- **equipmentStockpile 检查时机**：在 TryEnqueue 校验链最末（unitType / template / resources / manpower / **equipment**）—— 这样 reason 字符串与最常见失败原因对齐（先报缺资源、最后报缺装备）
- **现有 infantry 训练已运行的存档**：infantry.cost 不变 + 加 equipmentTrainingCost 字段 → 老存档读入后 UnitConfig 反序列化拿不到该字段默认 0 → 不校验 → 兼容
- **AI 国造兵也受装备库存约束**：是设计意图——AI 国必须建军工厂产装备才能训兵。AI 既有 TryBuild 已建军工，不需额外改动 B3 AI 决策

## 完工后人类 Play 验证清单
1. 选 federation_central → 推 10 回合不操作 → treasury 应大于 0（净收入累积）+ capital 比纯民厂产 capital 多（treasury 转化注入）
2. 民厂产 capital + 治理转化 capital → 两者明显双轨道增长
3. 训步兵前若装备库存 < 50 → 状态栏被拒"装备库存不足"
4. 军工厂产装备 → 装备库存增长 → 训练触发扣 50 装备 → 装备库存减少
5. 总体经济流：**税收→国库→投资→capital→建造**+**钢铁→军工→装备→训练**两个独立循环都跑通

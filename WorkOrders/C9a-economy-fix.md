# C9a — 经济修复：民用工厂产 capital + 平衡调参

## 背景
人类真玩 37 回合反馈：**建造系统第 1 回合后全部"资金不足"被拒**。

根因（Claude 已诊断）：`EconomyResolver.ResolveProduction` 唯一对 capital 的操作是**消耗**（军工产装备扣 capital）。`provinces.json` 的 `resourceOutput` 数组**从不含 capital**。`ResolveEconomy` 的 netIncome 进 treasury 不进 capital。

**`capital` 资源只有初始值 + 消耗，0 产出**——T5 MVP 时代漏写的民用工厂产出逻辑。玩家 60 capital 起步，建 1 民厂(30)+训 1 兵(2)+军工每回合消耗 → 第 2 回合断流。

## 范围

### 核心修复
| 改动 | 描述 | 数值 |
|------|------|------|
| **民用工厂每回合产 capital** | `country.ModifyResource("capital", civilianFactories * civilianFactoryCapitalOutput)` 在 ResolveProduction 加入 | civilianFactoryCapitalOutput=5 |
| 省份资源产出微调 | `provinceBaseOutputPerResource: 2 → 3` | 战略储备稍宽裕 |
| 工厂建造成本微降 | `civilianFactoryBuildCost: 30 → 25`, `militaryFactoryBuildCost: 40 → 30` | 让玩家初期能多建 1-2 个 |
| 步兵 capital 成本 | `infantry.cost.capital: 2 → 3`（units.json）| 反向稍涨防经济过热 |
| 初始 capital 不动 | 仍 60-150 由 countries.json 定 | — |

### 设计哲学
- **treasury**（国库现金）目前仍然只通过税收+税率档增长，**用途暂无**（C10+ 决定要不要让 treasury 也能转化为 capital）
- **capital**（工业资本）= 建造工厂+训练部队的唯一货币，**由民用工厂产**
- 这与战略游戏直觉一致：民用工厂 = 民用产能 = 工业资本积累；军工 = 装备产线

### 验收数值预期（推 10 回合，玩家 federation_central 初始 60 capital + 2 民厂）
| 回合 | capital | 备注 |
|---|---|---|
| 1 | 60+10-30 = 40 | 建第 1 民厂 |
| 2 | 40+10 = 50 | 等成本恢复（factoryBuildTurns=3 工厂还在建造） |
| 3 | 50+15 = 65 | 民厂建成 → 3 个民厂、每回合产 15 |
| 4 | 65+15-25 = 55 | 建第 2 民厂 |
| 5-10 | 单调上升 + 偶尔建造 | 应该稳定循环 |

如果 10 回合内 capital 跌到 0 一次以上 → 数值还需上调（civilianFactoryCapitalOutput → 8）。

## Phase 1: 诊断验证（实施修复前必跑）

OpenClaw 跑一次 Play（federation_central 玩家、不操作、推 10 回合），在每回合 Settlement 之后 log 玩家国 capital：

```csharp
// 临时加到 EconomyResolver.ResolveEconomy 末尾（验证完删除）
if (country.id == world.playerCountryId)
    _logger.Info($"[C9a验证] Turn {world.turnNumber} {country.id} capital={country.GetResource("capital")} treasury={country.treasury}");
```

**预期结果**：capital 单调递减 / 卡 0。**若不符合**（capital 上涨）= 我的诊断错了，停 `[需 Claude 决策]`。

## Phase 2: 实施修复

### Domain
- `EconomyConfig.cs` — 加 `int civilianFactoryCapitalOutput;` 字段

### Simulation
- `EconomyResolver.cs` — `ResolveProduction` 末尾加 `country.ModifyResource("capital", country.civilianFactories * eco.civilianFactoryCapitalOutput);`（在 (2) 军工产出之后，避免被消耗）

### Data
- `economy.json`：
  - 加 `civilianFactoryCapitalOutput: 5`
  - 改 `provinceBaseOutputPerResource: 2 → 3`
  - 改 `civilianFactoryBuildCost: 30 → 25`
  - 改 `militaryFactoryBuildCost: 40 → 30`
- `units.json`：
  - infantry `cost.capital: 2 → 3`

### Tests
- `EconomyResolverTests.cs` 追加：
  - `ResolveProduction_CivilianFactoriesProduceCapital`：2 民厂 + civilianFactoryCapitalOutput=5 → 1 回合 capital +10
  - `ResolveProduction_NoCivilianFactories_NoCapitalGain`：0 民厂 → 不产 capital
  - `ResolveProduction_MilitaryConsumesCapital`：军工产装备消耗 capital（既有逻辑回归）
- `ConfigValidationTests.cs` 追加 `Economy_HasCivilianCapitalOutput`

## Phase 3: 验证修复

跑 Play 推 10 回合，记录 capital 流向。**验收条件**：
- [ ] 玩家不操作 + 推 10 回合 → capital 至少**两次回到 30+** 状态（玩家能至少建 2 个工厂）
- [ ] AI 国（C4 TryAttack 也需要 capital）能稳定造军 → 推 10 回合至少出现 1 次 AI 进攻
- [ ] 不出现 capital 负数 / 溢出
- [ ] 截图：`Design/screenshots/c9a-capital-balance.png` 显示推到第 10 回合 capital 数字仍 > 0

## 不做（留 C9b/C9c/C10+）
- treasury 用途扩展（C10+ 再考虑）
- 资源面板 UI（C9b 做）
- AI 经济决策权重调整（B3 数值阈值不动）
- 维护费扩展（unit 维护费 TODO 注释保留）

## DoD Check List
- [ ] Phase 1 诊断 log 已跑、结果证实 capital 0 产出（或证伪让 Claude 重诊）
- [ ] EconomyResolver 修改仅 1 行（追加民厂产 capital）
- [ ] economy.json + units.json 数值变更与 §"核心修复"表完全一致，无擅自调整
- [ ] EconomyResolver 既有军工消耗 capital / 税收 treasury / 维护费公式 0 改动
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] 不动 BattleResolver / WarTollResolver / PeaceResolver / AiPeaceOfferResolver / OccupationResolver / VictoryConditionResolver / AIResolver / MovementResolver / ConstructionResolver / UnitProductionResolver / PoliticsResolver / SupplyResolver / AiRedeploymentResolver 任何公式
- [ ] 测试覆盖：3 个新增 EconomyResolverTests + 1 个 ConfigValidation
- [ ] artifacts/c9a-editmode.xml + c9a-playmode.xml 归档
- [ ] Phase 3 验收截图归档 + capital 流向数据贴 PR 描述
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 **0 failed**（C7/C8 累积 4 失败必须本单顺手修：BattleToll fixture warSupport 改 50 起步 / WithoutGarrison fixture controllerCountry != ownerCountry）
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 严禁
- 改 capital 之外的资源平衡（steel/food/oil/rareMetal/equipment）
- 加新经济概念（如银行/借贷/通胀深化）
- 改 treasury 流向（仍由 ResolveEconomy 算）
- 改税率档/民生档公式
- 跳过 Phase 1 诊断直接调数值
- 用"测试全绿"话术——4 个累积 fixture 失败必须显示在 PR 描述的失败列表里（修了或保留都要列）

## 歧义处理
- **Phase 1 验证结果与 Claude 诊断不符** → 立刻停 `[需 Claude 决策]`，不擅自试错调数值
- **修了之后 capital 增长太快**（玩家 10 回合内建 5 个工厂以上）→ civilianFactoryCapitalOutput 5 → 3 微调
- **AI 国的 capital 也受益**（AI 民厂数 ≥ 玩家时 AI 经济过强）→ 这是设计预期，B3 AI 阈值已经控制 aiMaxCivilianFactories=10 上限
- **修了之后 4 个累积失败仍在** → DoD 强制顺修，但本单不签发 fixture 改动细节（fixture bug 是 OpenClaw 自己写的，自己改回去）

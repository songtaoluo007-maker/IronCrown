# C9a-hotfix — 资源链热修：占领即获产 + 基础生计兜底

## 背景
C9a 修了 capital，但人类 Play 反馈"部队仍以资源不足被拒"。

根因（Claude 已诊断）：
1. **EconomyResolver.ResolveProduction line 34** 用 `p.ownerCountry == country.id` 过滤——**占领别国省份不获其产出**。配合 C5"永久占领"决策极不协调（打了一圈但收入不变）。
2. **每省 `resourceOutput` 单一化**（如 high_peak 只产 rareMetal）→ 单产出国天生缺其他资源 → 造兵需 food 10 + steel 5、玩家选高峰首都的国就断粮。

## 范围

### 双修
| 修法 | 改动 | 数值 |
|------|------|------|
| **占领即获产** | EconomyResolver.ResolveProduction Where 改 `controllerCountry == country.id` | 一行 |
| **基础生计兜底** | 每省每回合额外产 1 个 steel/food/oil/rareMetal（不论 resourceOutput），保证单产出国不卡死 | 4 个新字段 economy.json |

### 配置（economy.json 加 4 字段）
```json
{
  "provinceBaseSteelOutput": 1,
  "provinceBaseFoodOutput": 1,
  "provinceBaseOilOutput": 1,
  "provinceBaseRareMetalOutput": 1
}
```

### 设计哲学
- **占领即获产**：与 C5 永久占领闭环——打下越多省份经济越强，玩家有真实的"征服收益"
- **基础生计**：保护初期玩家不被单产出国搞死，仍保留 resourceOutput 主导差异（如 high_peak 产 rareMetal +5、其他省只产 +1）

## 文件变更清单

### Domain
- `EconomyConfig.cs` — 加 4 个 C9a-hotfix 字段

### Simulation
- `EconomyResolver.cs` —
  - `ResolveProduction` line 34: `p.ownerCountry == country.id` → `p.controllerCountry == country.id`
  - foreach 省份循环内追加：
    ```csharp
    country.ModifyResource("steel", eco.provinceBaseSteelOutput);
    country.ModifyResource("food", eco.provinceBaseFoodOutput);
    country.ModifyResource("oil", eco.provinceBaseOilOutput);
    country.ModifyResource("rareMetal", eco.provinceBaseRareMetalOutput);
    ```
    （在 resourceOutput 循环之后、军工产装备之前）

### Data
- `economy.json` — 4 个新字段（值见 §配置）

### Tests
- `EconomyResolverTests.cs` 追加：
  - `ResolveProduction_OccupiedProvinceContributes`：构造 A 国占领 B 国的 high_peak（owner=B, controller=A）→ A 国获得 high_peak 产出
  - `ResolveProduction_BaseGenericOutput`：1 省份（无 resourceOutput 或不含 food） → 每回合仍 food +1 steel +1 oil +1 rareMetal +1
  - `ResolveProduction_OriginalOwnerLosesOccupiedProvinceOutput`：被占省的 ownerCountry 不再获该省产出（防御性）
- `ConfigValidationTests.cs` 追加 `Economy_HasBaseResourceOutputs`

### 不动
- BattleResolver / WarTollResolver / PeaceResolver / AiPeaceOfferResolver / OccupationResolver / VictoryConditionResolver / AIResolver / MovementResolver / ConstructionResolver / UnitProductionResolver / AiRedeploymentResolver / PoliticsResolver / SupplyResolver
- ResolveEconomy（税收 treasury 流向）
- C9a 引入的 civilianFactoryCapitalOutput=5 数值
- units.json / countries.json / provinces.json
- governance 文件 / ProjectSettings / Packages

## DoD Check List
- [ ] EconomyResolver 只改 1 行 Where + 加 1 段 ModifyResource × 4
- [ ] economy.json 仅新增 4 字段，无删改既有字段
- [ ] 既有 EconomyResolverTests 全绿（占领即获产改动若让既有测试失败，确认是测试 fixture 误用 ownerCountry，OpenClaw 修 fixture 而非回退产品代码）
- [ ] 新增 4 个测试全绿
- [ ] artifacts/c9a-hotfix-editmode.xml + c9a-hotfix-playmode.xml 归档
- [ ] Play 截图：`c9a-hotfix-resource-balance.png`——federation_central 玩家推 10 回合后所有 4 种基础资源 > 0
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 0 failed（C9a 已收 4 累积失败 fixture，本单不应再有）
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 严禁
- 改 provinces.json 给每省加多个 resourceOutput（架构上是数据问题，但 hotfix 走最小代码改动）
- 改 infantry.cost / artillery.cost / 其他兵种成本
- 删 ownerCountry 概念（仍用作"法理归属"语义，仅产出改用 controller）
- 改税收 treasury 流向（保持 ResolveEconomy 不动）
- 跳过测试覆盖中"被占省原主失去产出"用例（这是 controllerCountry 修改的反向验证）

## 歧义处理
- **基础生计是否受 infrastructure 加成**：本单写死**不加成**——基础生计是 1，与 resourceOutput 主产出加成（base 3 + infra × 1）形成对比。**勿擅自加 infrastructure 修正**。
- **被占省 resistance 反抗时是否扣产出**：本单**不做**——resistance 仅影响 OccupationResolver 反抗事件，不影响 EconomyResolver。C10+ 决定要不要"reseistance 越高产出越少"。
- **省份 resourceOutput 数组含多个资源时基础生计是否累加**：**不累加**——基础生计每省固定 +1×4 资源类型，与 resourceOutput 主产出分开计算。

## 完工后人类 Play 验证清单
1. 选 federation_central 推 10 回合不操作 → 4 种基础资源 + capital 都应增长
2. 占领一个邻省（如 red_plain）后 → 下回合开始 EconomyResolver 应给你算 red_plain 的产出
3. 训步兵命令应可在多回合多次执行（每 2 回合至少 1 次）
4. AI 国也应同样受益（AI 经济决策的可建造性提高）

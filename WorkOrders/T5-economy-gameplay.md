# 工作单 T5 — 玩法结算从配置（Economy MVP）

| 项 | 值 |
|---|---|
| 工作单号 | T5（MVP 倒数第三；让回合产生可见经济效果） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数值 | Claude（规则 13 架构 + 经人类授权代拟初版数值，规则 14 人类保留终审） |
| 分支 | `feature/t5-economy-gameplay`（独立分支） |
| 前置 | T4 已完成 |
| 角色边界 | 规则 12：只实现。**数值已由 Claude 写入 `StreamingAssets/Configs/Json/economy.json` 与 `provinces.json`——勿改动其中数值**；公式按本单规格实现。遇未覆盖点停 `[需 Claude 决策]` |

## 0. 目标
让"内政/结算"阶段产生**可见**经济变化：省份产出原料 → 入国库存；军工厂消耗钢铁+资本产出装备；税收−维护进国库。为 T6 的 UI 演示提供"每回合数字在动"。**纯整数运算（确定性）**。

## Phase 0 — 前置必办

1. 新建分支 `feature/t5-economy-gameplay`。
2. **UTF-8 编码守卫**（修复反复出现的 CHANGELOG 乱码）：新增 EditMode 测试 `IronCrown.Domain.Tests/EncodingGuardTests.cs`：用**严格 UTF-8**（`new UTF8Encoding(false, throwOnInvalidBytes: true)`）读取 `CHANGELOG.md`、`PROJECT_RULES.md`、`ARCHITECTURE.md` 及所有 `Assets/StreamingAssets/Configs/Json/*.json`，任一抛异常即测试失败。
3. **标准纪律**：本单起 **OpenClaw 不得直接编辑 `CHANGELOG.md`**（历史两次写成非 UTF-8）。把本单的 changelog 文本写在 **PR 描述**里，由 Claude 合入。
4. 跑全套测试确认 49+ 仍绿。

## 1. 数值（Claude 已写入，勿改）
- `economy.json`（`EconomyConfig`，id="global"）：`provinceBaseOutputPerResource=4`、`provinceInfraOutputBonus=2`、`militaryFactoryEquipmentOutput=4`、`equipmentSteelCost=2`、`equipmentCapitalCost=1`、`civilianFactoryUpkeep=2`、`militaryFactoryUpkeep=3`、`dockyardUpkeep=4`。
- `provinces.json`：6 省已填实 `resourceOutput`/`infrastructure` 等。
> 这些是**初版平衡**，人类后续可调；OpenClaw 不得改这些数字，也不得新增/编造其它数值（一切常量都从 Config 读，规则 5）。

## 2. 实现规格

### 2.1 新增 DTO `Domain/Config/EconomyConfig.cs`
```
[System.Serializable] public class EconomyConfig {
    public string id;
    public int provinceBaseOutputPerResource;
    public int provinceInfraOutputBonus;
    public int militaryFactoryEquipmentOutput;
    public int equipmentSteelCost;
    public int equipmentCapitalCost;
    public int civilianFactoryUpkeep;
    public int militaryFactoryUpkeep;
    public int dockyardUpkeep;
}
```
- `ConfigRegistry.LoadAll()` 增加 `LoadTable<EconomyConfig>("economy")`。

### 2.2 运行时状态
- `Domain/State/Country.cs`（`CountryState`）新增 `public int equipmentStockpile;`（装备库存）。

### 2.3 `EconomyResolver` 注入配置
- 构造改为 `EconomyResolver(IConfigRegistry config)`；`GameLifetimeScope` 注册时由容器注入（`IConfigRegistry` 已注册）。

### 2.4 `ResolveProduction(CountryState country, WorldState world)`（内政阶段，确定性、有序）
```
var eco = _config.Get<EconomyConfig>("global");

// (1) 省份原料产出 → 国库存
foreach (province in world.provinces.Values 中 ownerCountry==country.id, 按 id 升序)
    foreach (resId in province.resourceOutput)   // 数组原序
        int amt = eco.provinceBaseOutputPerResource + province.infrastructure * eco.provinceInfraOutputBonus;
        int oldV = country.GetResource(resId);
        country.ModifyResource(resId, amt);
        _events.Publish(new ResourceChangedEvent { CountryId=country.id, ResourceId=resId, OldValue=oldV, NewValue=oldV+amt });

// (2) 军工产出：steel(+capital) -> equipment（受输入门限）
int desired  = country.militaryFactories * eco.militaryFactoryEquipmentOutput;
int bySteel  = country.GetResource("steel")   / System.Math.Max(1, eco.equipmentSteelCost);
int byCap    = country.GetResource("capital") / System.Math.Max(1, eco.equipmentCapitalCost);
int actual   = System.Math.Min(desired, System.Math.Min(bySteel, byCap));
if (actual > 0) {
    country.ModifyResource("steel",   -actual * eco.equipmentSteelCost);
    country.ModifyResource("capital", -actual * eco.equipmentCapitalCost);
    country.equipmentStockpile += actual;
}
```
- `EconomyResolver` 需注入 `IEventPublisher`（发 ResourceChangedEvent）——加到构造（容器注入）。

### 2.5 `ResolveEconomy` 去硬编码（规则 5）
- `CalculateMilitaryExpense` 里 `*2 / *3 / *4` 改为 `eco.civilianFactoryUpkeep / militaryFactoryUpkeep / dockyardUpkeep`。其余税收/通胀公式**不动**（截断保持）。

### 2.6 配置校验测试
- `ConfigValidationTests` 增加：`economy.json` schemaVersion=1、存在 id="global" 行、各常量 ≥ 0。

## 3. 测试（规则 6）
- **经济产出确定性**：用真实配置构建 1 国（含其省份），调 `ResolveProduction` 一次，断言 `country.resources` 与 `equipmentStockpile` 等于**按 economy.json 手算的期望值**（写出算式）。
- **维护费来自配置**：断言 `CalculateMilitaryExpense` = civ×2+mil×3+dock×4（取自配置而非字面量——可改配置值验证联动）。
- **补 T4 遗留的"存档续跑等价"**（现回合有真实效果）：用真实配置经 `GameSessionService` 跑 2 回合 → `Save` → 新会话 `Load` → 再跑 2 回合，世界状态哈希 == 直接跑 4 回合。替换 T4 中只比对初始世界的弱用例。

## 4. 文件清单
| 动作 | 路径 |
|---|---|
| 已由 Claude 提供 | `StreamingAssets/Configs/Json/economy.json`、填实的 `provinces.json` |
| 新增 | `Domain/Config/EconomyConfig.cs`、`Tests/.../EncodingGuardTests.cs`、经济产出测试、续跑等价测试 |
| 改 | `Domain/State/Country.cs`(+equipmentStockpile)、`Application/Config/ConfigRegistry.cs`(+economy)、`Simulation/EconomyResolver.cs`(注入+产出+去硬编码)、`Bootstrap/GameLifetimeScope.cs`(EconomyResolver 依赖)、`ConfigValidationTests`(+economy) |

## 5. 验收门禁（DoD）
- [ ] Phase 0：独立分支、UTF-8 守卫测试存在且绿、未直接编辑 CHANGELOG。
- [ ] `EconomyConfig` 经 `IConfigRegistry` 加载；`EconomyResolver` 全部常量取自配置（**零硬编码数值**，规则 5）。
- [ ] `ResolveProduction` 按 §2.4 实现，确定性、有序、发 `ResourceChangedEvent`。
- [ ] 经济产出测试 + 续跑等价测试通过；全套 EditMode 绿（附 results）。
- [ ] **未改 economy.json/provinces.json 的数值**，未编造新数值（规则 9/14）。
- [ ] PR 在 `feature/t5-economy-gameplay`；changelog 文本写在 PR 描述（勿碰 CHANGELOG.md）。

## 6. 歧义处理
本单未覆盖、或需新数值/玩法 → 停下标 `[需 Claude 决策]`/`[需人类定值]`，写进 PR 描述。

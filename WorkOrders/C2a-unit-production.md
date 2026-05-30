# 工作单 C2a — 造兵管线（军事阶段第二步 · 部分一）

| 项 | 值 |
|---|---|
| 工作单号 | C2a（造兵：仅 infantry、仅首都、多回合队列） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数据 | Claude（规则 13 + 经授权代拟造兵回合数 / 成本扣减时机；规则 14 人类终审） |
| 分支 | `feature/c2a-unit-production`（从 main 切，main 须先合入 C1） |
| 前置 | C1 已审查通过（neighbors+初始驻军+地图标记达成；C1 的 Play 截图可与本单一起补） |
| 角色边界 | 规则 12：只实现本单（造兵）。**勿改既有经济/政治/AI 公式与数值**；**勿做移动**（C2b）、战斗（C3）、AI 造兵（C4）；遇未覆盖点停 `[需 Claude 决策]` |

## 0. 目标与范围
玩家可在**首都**下令造一支步兵：下单时**一次性**扣 `infantry.cost` 全部资源 + 扣 `manpower=hp`，**2 回合后**完工，生成 1 支满编 `infantry` 加入 `world.units` 驻首都。**不做**：移动、战斗、其他兵种、AI 造兵、取消退款。

## Phase 0（C1 收尾，强制项；不做则后续单累计风险）

### 0.1 ★ 扩 `SaveLoadEquivalenceTests.HashWorld` 覆盖部队 + 省份静态字段
当前 `HashWorld`（`Assets/Tests/EditMode/IronCrown.Application.Tests/SaveLoadEquivalenceTests.cs:70-96`）**只哈希 country 的 9 个数值 + province 的 3 个 string**——units 字段全丢都测不出来，C1 续跑等价"通过"实属侥幸。本单 Phase 0 必须改：
- 加 `world.units.Values.OrderBy(u => u.id, Ordinal)`：每支部队写入 `id / unitType / ownerCountry / currentProvinceId / manpower / equipment / organization / maxManpower / maxEquipment / maxOrganization / morale / experience / movesLeft`（13 字段，覆盖运行时可变项）。
- 省份循环补 `neighbors`（按原序 join `"|"`）、`gridX`、`gridY`、`terrain.ToString()`。
- `BuildWorldWithProvinces` 加 1 支 infantry 到 iron_city（满编），让续跑等价真正覆盖 units。
- 不改既有断言数（避免回退既有等价集合），新加 1 个测试 `SaveLoadEquivalence_Units_Preserved`：建 1 支已损耗（manpower=80/organization=30/movesLeft=1）的 infantry → 存 → 读 → hash 等价。

### 0.2 ★ 扩 `UnitSaveData` 全字段持久化（决策 A）
当前 `Assets/Scripts/Application/Persistence/SaveModels.cs:108-117` 只存 7 字段；C2 起 max/morale/movesLeft 将偏离模板，必须持久。决策已敲定 **A 方案**（全字段持久化，序列化简单，后续大规模再评估 B 方案"重查 config 补回"）。具体追加：
- `UnitSaveData` 新增字段：`maxManpower, maxEquipment, maxOrganization, morale, experience, baseAttack, baseDefense, baseBreakthrough, armor, piercing, speed, movesLeft, supplyConsumption`（13 个 int）。
- `SaveMapper.ToSave/ToRuntime` 双向，**全字段**写读，不再依赖模板补回。
- `SaveMapper.ToRuntime` 增加：用 `world.units.Values` 按 owner 分组、按 id 升序，**重建 `country.unitIds`**（不进存档，避免一致性 bug）。`WorldInitializer.CreateNewGame` 同样在 Add 部队时 `country.unitIds.Add(unitId)`——激活 `CountryState.unitIds` 字段（目前是死代码）。

### 0.3 `ReadModelBuilder.BuildProvinceView` 遍历 units 按 id 升序
`Assets/Scripts/Application/Queries/ReadModelBuilder.cs:82` 改为：先 `world.units.Values.OrderBy(u => u.id, Ordinal).ToList()` 一次，外层 BuildWorldView 传给每个 BuildProvinceView，避免 O(P*U) 中 P*U 都是无序。对 C2a 还是计数+1 无可见差异，但 C2b 要展示驻军列表时直接复用有序结果。

### 0.4 artifacts 命名规范
- 删除 C1 残留中间 artifact：`artifacts/c1-editmode.xml`、`c1-editmode3.xml`、`c1-editmode-final.xml`（早于 c1-editmode5 且含失败）、`c1-playmode.xml`、`c1-playmode2.xml`，**只保留** `c1-editmode5.xml`（97/97）+ `c1-playmode-final.xml`（5/5）作为 C1 权威证据。
- 本单产出严格命名为 `artifacts/c2a-editmode.xml` + `artifacts/c2a-playmode.xml`，**不再用 -final / 数字后缀**。如多次跑覆盖同名即可。

### 0.5 起点 & 分支卫生
- 从 main（合入 C1 后）切 `feature/c2a-unit-production`；起点跑 `c1-editmode5.xml` 等价的 97/97 + 5/5。
- 不直接编辑 `CHANGELOG.md`（OpenClaw 已多次写坏 UTF-8）—— 改动摘要写 PR 描述，由 Claude 合入。
- 不擅自改 `PROJECT_RULES.md` / `ARCHITECTURE.md` / `PROJECT_STATE.md`（这些 commit 出现在 C1 是 Claude 的 dirty 工作打包遗留，本单务必干净）。
- 文件全部 UTF-8（无 BOM）。

## 1. 数据（Claude 代拟，规则 14 人类可调）

| 项 | 值 | 备注 |
|---|---|---|
| C2a 仅开放兵种 | `infantry` | 其他兵种 C3 解锁；命令传 unitType 非 "infantry" 一律拒 |
| 造兵回合数 | **2 回合** | 比工厂(3) 短；写入 `economy.json` 新字段 `unitProductionTurns: 2` |
| 资源成本 | `UnitConfig.cost` 全量 | infantry: `{steel:5, food:10, capital:2}` 已在 units.json |
| manpower 成本 | `infantryTemplate.hp = 100` | 国家 `manpower` 池子直接扣 |
| 扣减时机 | **下单时一次性全扣** | 与 `ConstructionResolver.TryBuild` 一致 |
| 取消 | C2a 不做 | 退款逻辑后续视需要补 |
| 新部队属性 | 与 C1 `WorldInitializer` 初始步兵创建块**完全一致**（满编、morale=50、experience=0）| 抽工具方法 `UnitFactory.CreateFromTemplate(unitType, ownerCountry, provinceId, UnitConfig)` 供 WorldInitializer + UnitProductionResolver 共用，避免重复（规则 3） |
| 新部队 id 规则 | `{countryId}_{shortType}_{seq}` | `shortType` infantry→`inf`；seq = `world.units.Values.Count(u => u.ownerCountry==X && u.unitType==Y) + 1`。初始驻军是 inf_1，第二支即 inf_2 |

## 2. 架构决策（写死）

1. **新增独立 Resolver** `Simulation/UnitProductionResolver.cs`，**不**复用 `ConstructionResolver`。理由：职责分离，工厂与兵种产线未来独立演化（规则 3）。
2. **新增工具** `Domain/UnitFactory.cs`（静态方法 `CreateFromTemplate`）。`WorldInitializer.CreateNewGame` 初始步兵创建块**同步改为调它**（规则 3 共享逻辑、规则 11 删重复代码）。
3. 造兵队列挂 `CountryState.unitProductionQueue`（`List<UnitProductionOrder>`）。新部队完工时 owner=本国、currentProvinceId=本国 `capitalProvinceId`。
4. `BuildUnit` 命令通过 `GameCommand`（加 `unitType` 字段）走 `GameSessionService.IssueCommand` → `UnitProductionResolver.TryEnqueue`，校验：是玩家国 / unitType 允许 / 资源够 / manpower 够。校验失败返回 `CommandResult.Rejected("...")`。
5. **结算次序**（写死）：`TurnResolver.ExecuteTurn` 在 `EconomyResolver` 之后、`ConstructionResolver.ResolveConstruction` 之前调用 `UnitProductionResolver.ResolveProduction`。理由：避免新完工的部队/工厂在当回合再被对方阶段误处理。
6. AI 暂不造兵（`AIResolver` 不动）。
7. 规则 4：UI 只读 `CountryView.unitProductionQueueCount`；规则 3：无玩法逻辑泄漏 UI。
8. **存档稳定性**：`CountrySaveData.unitProductionQueue` 数组（可空），`UnitProductionOrderSaveData {unitType, turnsRemaining}`；`schemaVersion` **不**升（GameState.schemaVersion 仍为 1；C-2 技术债：存档迁移整套等真正破坏兼容时再加，本单不触发）。

## 3. 实现规格

### 3.1 Contracts
- `Contracts/Commands/CommandType.cs`：加枚举值 `BuildUnit`。
- `Contracts/Commands/GameCommand.cs`：加 `public string unitType;`（仅 BuildUnit 用）。
- `Contracts/ReadModels/CountryView.cs`：加 `public int unitProductionQueueCount;`。
- `Contracts/Events/UnitProducedEvent.cs`（新增）：`public string unitId; public string ownerCountry; public string provinceId; public string unitType;`。

### 3.2 Domain
- `Domain/UnitProductionOrder.cs`（新增，仿 `Domain/State/ConstructionOrder.cs` 风格）：`public sealed class UnitProductionOrder { public string unitType; public int turnsRemaining; }`。
- `Domain/Country.cs`：加 `public List<UnitProductionOrder> unitProductionQueue = new();`（放在 `constructionQueue` 旁）。
- `Domain/UnitFactory.cs`（新增）：`public static class UnitFactory { public static UnitState CreateFromTemplate(string id, string unitType, string ownerCountry, string provinceId, UnitConfig template) { ... } }`——内含 C1 WorldInitializer 89-117 行的字段映射。

### 3.3 Application
- `Application/Setup/WorldInitializer.cs`：初始部队循环改调 `UnitFactory.CreateFromTemplate`，并在 Add 后 `country.unitIds.Add(unit.id)`。
- `Application/Persistence/SaveModels.cs`：
  - `CountrySaveData` 加 `public UnitProductionOrderSaveData[] unitProductionQueue;`
  - 新增 `[Serializable] public class UnitProductionOrderSaveData { public string unitType; public int turnsRemaining; }`
  - **★ 0.2**：`UnitSaveData` 加 13 字段（清单见 Phase 0.2）。
- `Application/Mapping/SaveMapper.cs`：
  - `ToSave`：unit 全字段写、country.unitProductionQueue 写。
  - `ToRuntime`：unit 全字段读、country.unitProductionQueue 读；**最后一步**遍历 `world.units` 按 ownerCountry 分组、按 id 升序，重建每个 country.unitIds（不读存档）。
- `Application/Queries/ReadModelBuilder.cs`：
  - `BuildCountryView`：`unitProductionQueueCount = c.unitProductionQueue.Count`。
  - `BuildWorldView`：units 先 `OrderBy(u => u.id, Ordinal)` 一次，结果作为参数传入 `BuildProvinceView`（**★ 0.3**）。
- `Application/Services/GameSessionService.cs`：`IssueCommand` 加分支 `case CommandType.BuildUnit:` → 校验玩家国 → 调 `_unitProduction.TryEnqueue(country, cmd.unitType, _config)`。
- DI 注入 `UnitProductionResolver`。

### 3.4 Simulation — `UnitProductionResolver`（新文件）

```csharp
namespace IronCrown.Simulation {
  public sealed class UnitProductionResolver {
    private static readonly HashSet<string> AllowedTypes = new() { "infantry" }; // C2a 限定

    public CommandResult TryEnqueue(CountryState c, string unitType, IConfigRegistry config, EconomyConfig eco) {
      if (!AllowedTypes.Contains(unitType)) return Rejected($"unitType 不允许: {unitType}");
      var template = config.Get<UnitConfig>(unitType);
      if (template == null) return Rejected($"未找到 unitType 模板: {unitType}");
      if (!c.HasResources(template.cost)) return Rejected("资源不足");
      if (c.manpower < template.hp) return Rejected("人力不足");
      c.ConsumeResources(template.cost);
      c.manpower -= template.hp;
      c.unitProductionQueue.Add(new UnitProductionOrder { unitType = unitType, turnsRemaining = eco.unitProductionTurns });
      return Accepted();
    }

    public List<UnitState> ResolveProduction(WorldState world, IConfigRegistry config) {
      var produced = new List<UnitState>();
      foreach (var country in world.countries.Values.OrderBy(c => c.id, StringComparer.Ordinal)) {
        var completed = new List<UnitProductionOrder>();
        foreach (var order in country.unitProductionQueue) {
          order.turnsRemaining--;
          if (order.turnsRemaining <= 0) {
            var template = config.Get<UnitConfig>(order.unitType);
            int seq = world.units.Values.Count(u => u.ownerCountry == country.id && u.unitType == order.unitType) + 1;
            string shortType = order.unitType == "infantry" ? "inf" : order.unitType; // C2a 只 inf；其他兵种 C3 再扩
            string unitId = $"{country.id}_{shortType}_{seq}";
            var unit = UnitFactory.CreateFromTemplate(unitId, order.unitType, country.id, country.capitalProvinceId, template);
            world.units[unitId] = unit;
            country.unitIds.Add(unitId);
            produced.Add(unit);
            completed.Add(order);
          }
        }
        foreach (var d in completed) country.unitProductionQueue.Remove(d);
      }
      return produced;
    }
  }
}
```
- `CommandResult.Rejected(reason)` / `Accepted()` 用现有静态方法；如无则按现有命名沿用（看 `Contracts/Commands/CommandResult.cs`）。
- 事件发布在 `TurnResolver` 拿到 produced 列表后逐个 `_events.Publish(new UnitProducedEvent {...})`。

### 3.5 Simulation — `TurnResolver`
- 构造函数注入 `UnitProductionResolver unitProduction`。
- `ExecuteTurn` 在 `economy.ResolveTurn(world)` 之后、`construction.ResolveConstruction(country)` 之前调用 `var produced = unitProduction.ResolveProduction(world, config);` 并对每个发 `UnitProducedEvent`。
- **既有 EconomyResolver/ConstructionResolver 调用顺序不动**，仅插入新一步。

### 3.6 Bootstrap
- `Bootstrap/GameLifetimeScope.cs`：注册 `UnitProductionResolver`；传给 `TurnResolver` 和 `GameSessionService`。

### 3.7 Presentation
- `Presentation/MainHudController.cs`：
  - `Bind`：新增 `_buildInfantryBtn = root.Q<Button>("build-infantry-btn");` + 回调 `BuildInfantry()`。
  - `BuildInfantry()` 仿 `BuildCivilian`：发 `GameCommand { commandType = BuildUnit, countryId = _session.PlayerCountryId, unitType = "infantry" }`；accepted → "已下令训练步兵"，rejected → "被拒: {reason}"。
  - `FormatCountryRow`：在"在建"字段后追加 `在训: {c.unitProductionQueueCount}`（>0 时）。
- `Assets/UI/MainHud.uxml`：在现有"建造民用厂/军用厂"按钮区加 `<Button name="build-infantry-btn" text="训练步兵" />`。
- `Assets/UI/MainHud.uss`：复用现有按钮样式，无新 class。

## 4. 测试（规则 6）

### EditMode — UnitProductionResolverTests（新文件）
- `TryEnqueue_ResourcesEnough_AcceptsAndDeducts`：构造 country 含 cost + manpower 余量 → accepted、queue 长度 1、资源/manpower 精确扣减。
- `TryEnqueue_ResourcesShort_Rejects`：steel 差 1 → rejected, queue 长度 0, 资源不动。
- `TryEnqueue_ManpowerShort_Rejects`：manpower 差 1 → rejected。
- `TryEnqueue_UnknownType_Rejects`：unitType="artillery" → rejected（C2a 仅 infantry）。
- `ResolveProduction_TwoTurns_Completes`：下单 → ResolveProduction × 2 → world.units +1、id 符合规则、queue 清空、country.unitIds 含新 id。
- `ResolveProduction_NewUnitAttributes_FullStrength`：新部队 manpower==maxManpower==hp、organization==maxOrganization==orgTemplate、morale==50、experience==0。
- `ResolveProduction_CountryOrderDeterministic`：两国同回合完工 → produced 列表按 country.id 升序。

### EditMode — GameSessionServiceTests（追加）
- `IssueCommand_BuildUnit_Player_Accepts`。
- `IssueCommand_BuildUnit_NonPlayer_Rejects`（reason="非玩家国"）。
- `BuildUnit_TwoTurnAdvance_NewGarrisonAppears`：下单 → 跑 2 个完整回合 → `view.provinces[capital].garrisonCount == 2`（之前 C1 是 1）。

### EditMode — ReadModelBuilderTests（追加）
- `BuildCountryView_UnitProductionQueueCount`：country.unitProductionQueue 含 1 项 → view.unitProductionQueueCount == 1。
- `BuildWorldView_UnitsOrderedById`（**★ 0.3 验证**）：构造 3 支 unit id="b","a","c" 同省份 → BuildProvinceView 用的 units 顺序为 a,b,c（通过反射或暴露内部测试桩验证；如不便测，至少加 GarrisonCount 在两次调用间稳定的回归测试）。

### EditMode — SaveLoadEquivalenceTests（★ 0.1）
- 改造 `HashWorld` 含 units + province 静态字段。
- 改造 `BuildWorldWithProvinces` 加 1 支 infantry 到 iron_city。
- 新增 `SaveLoad_Units_PreservedAcrossSave`：1 支已损耗 infantry 存→读→hash 等价。
- 新增 `SaveLoad_UnitProductionQueue_Preserved`：玩家国下单 → 1 回合 → 存（turnsRemaining=1）→ 读 → 再跑 1 回合 → 新部队按期完工、id/属性与不存档直跑等价。
- 既有 `SaveLoadEquivalence_2TurnsSave2More_Equals4Turns` 必须不退化（hash 扩展不会改变跑 4 回合 vs 跑 2+2 的等价性）。

### EditMode — ConfigValidationTests（追加）
- `Economy_HasUnitProductionTurns`：`economy.json` 含 `unitProductionTurns` 且 > 0。

### PlayMode — MvpSmokeTests（追加 1 用例）
- `BuildInfantry_Click_AdvanceTwoTurns_GarrisonIncreases`：找按钮 → controller.Advance() × 推到下一回合 ×2 → mapTile 内 garrisonBadge 文本变 "⚔2"。

## 5. 文件清单
| 动作 | 路径 |
|---|---|
| 改 | `Contracts/Commands/CommandType.cs`（+BuildUnit）、`Contracts/Commands/GameCommand.cs`（+unitType）、`Contracts/ReadModels/CountryView.cs`（+unitProductionQueueCount） |
| 新增 | `Contracts/Events/UnitProducedEvent.cs` |
| 新增 | `Domain/UnitProductionOrder.cs`、`Domain/UnitFactory.cs` |
| 改 | `Domain/Country.cs`（+unitProductionQueue） |
| 改 | `Application/Persistence/SaveModels.cs`（UnitSaveData 全字段 / CountrySaveData+queue / +UnitProductionOrderSaveData） |
| 改 | `Application/Mapping/SaveMapper.cs`（unit 全字段双向 + queue 双向 + 重建 country.unitIds） |
| 改 | `Application/Queries/ReadModelBuilder.cs`（units 排序 + CountryView 字段） |
| 改 | `Application/Setup/WorldInitializer.cs`（调 UnitFactory + 维护 country.unitIds） |
| 改 | `Application/Services/GameSessionService.cs`（IssueCommand 分支） |
| 新增 | `Simulation/UnitProductionResolver.cs` |
| 改 | `Simulation/TurnResolver.cs`（注入 + 调用顺序） |
| 改 | `Bootstrap/GameLifetimeScope.cs`（DI 注册） |
| 改 | `Presentation/MainHudController.cs`、`Assets/UI/MainHud.uxml`（+按钮）、`Assets/UI/MainHud.uss`（如需小调） |
| 改 | `Domain/Config/EconomyConfig.cs`（+unitProductionTurns）、`Assets/StreamingAssets/Configs/Json/economy.json`（+unitProductionTurns: 2） |
| 新增/改 | 上述测试 + `SaveLoadEquivalenceTests` Phase 0.1 改造 |
| 清理 | 删 `artifacts/c1-editmode.xml`、`c1-editmode3.xml`、`c1-editmode-final.xml`、`c1-playmode.xml`、`c1-playmode2.xml`、`c1-editmode4.xml`、`b15-editmode.xml`、`b15-editmode3.xml`、`b15-editmode4.xml`、`b2-editmode.xml`、`b2-playmode.xml`、`b3-editmode*.xml`（之外仅保留 `c1-editmode5.xml`+`c1-playmode-final.xml`+本单产出） |

## 6. 验收门禁（DoD）
- [ ] Phase 0 五项全做：HashWorld 扩 / UnitSaveData 扩 / units 排序 / artifacts 清理 / 起点 97+5 全绿。
- [ ] EditMode 全绿：既有 97 + 本单新增（≈ 12 个）= 约 109+ 全过；列表含 UnitProductionResolverTests、SaveLoad_Units_Preserved、SaveLoad_UnitProductionQueue_Preserved。
- [ ] PlayMode 6/6 全绿（5 既有 + 1 BuildInfantry）。
- [ ] Play `Main`：选玩家国 → 点"训练步兵"→ 状态栏含"已下令训练步兵"+ 国家行含"在训: 1"→ 推 2 回合 → 地图首都徽章 `⚔1` 变 `⚔2`（**截图为证**，存 `Design/screenshots/c2a-build-step1.png`、`c2a-build-completed.png`）。**同时补 C1 截图**：`Design/screenshots/c1-garrison-badge.png`、`c1-province-detail.png`。
- [ ] 续跑等价：下单 → 第 1 回合后存 → 读 → 跑 1 回合 → 完工时机与不存档等价（hash 一致 + 部队 id 一致）。
- [ ] 规则 4 守住（Presentation 不引用 Domain/Simulation）；数据全取自 config（unitProductionTurns 在 economy.json）；未做移动/战斗/AI 造兵；未改既有经济/政治/AI 公式与数值（diff 不含 EconomyResolver/PoliticsResolver/AIResolver 玩法行）。
- [ ] batchmode 0 error；`artifacts/c2a-editmode.xml`（约 109+ pass）+ `artifacts/c2a-playmode.xml`（6/6）；PR 在 `feature/c2a-unit-production`；CHANGELOG 写 PR 描述（**不直接改 CHANGELOG.md**）。
- [ ] 干净 commit：diff 不含 `PROJECT_RULES.md` / `ARCHITECTURE.md` / `PROJECT_STATE.md` / `CHANGELOG.md`；不擅自改 economy.json 既有字段（仅追加 `unitProductionTurns`）；不改 `Ideology` 枚举（C1 的 ConfigValidationTests vs Ideology 枚举漂移留待技术债清单）。

## 7. 歧义处理
- "训练步兵"按钮文案/位置/快捷键 → 最小清晰实现，截图为准。
- 资源/manpower 不足提示文案 → 自定，参照"被拒: 资源不足/人力不足"。
- 推进次序（EconomyResolver → UnitProductionResolver → ConstructionResolver）按 §2.5 写死，**勿改**。
- 兵种扩充、AI 造兵、取消造兵、首都以外造兵 → 全部留 C2b/C3/后续，**勿擅自扩**，遇 `[需 Claude 决策]`。
- 若 EditMode 跑出失败：先看是不是 0.1/0.2 改造导致 `BuildWorldWithProvinces` 等老用例需要补字段；若是，按"扩 BuildWorldWithProvinces 默认值不影响既有断言"原则补；如断言本身要改，停 `[需 Claude 决策]`。
- 严禁给造兵/新部队编造未在 units.json 配置的数值（全部取 UnitConfig 模板）。
- 严禁在本单"顺手"改 B 阶段或 C1 的玩法代码（如 EconomyResolver/AIResolver/PoliticsResolver/BattleResolver/SupplyResolver）；如发现 bug 单独写 `[需 Claude 决策]` 报告。

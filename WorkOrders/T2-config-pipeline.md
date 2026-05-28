# 工作单 T2 — 配置管线（Config Pipeline）

| 项 | 值 |
|---|---|
| 工作单号 | T2（覆盖路线图 T2 配置/状态分离 + T3 配置管线） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查 | Claude（规则 13） |
| 分支 | `feature/t2-config-pipeline` |
| 前置 | **T1-FIX 已合入**：工程 batchmode 编译 0 error、EditMode 全绿、git 工作流就位 |
| 角色边界 | 规则 12：只实现，不做架构/数值/玩法决策。本单已写死全部结构决策 |

---

## 0. 内容边界（规则 14 / 12，最高优先，违者打回）

- **严禁发明或改动任何游戏平衡数值**。`countries.json` / `units.json` / `resources.json` 现有数值是既有内容，**原样保留**（移动文件 ≠ 改值）。
- 需新建 `provinces.json`：只建**结构** + 外键所需的 6 个首都省份行。`ownerCountry`/`isCapital`/`id` 这类**由现有数据推导的结构字段**照实填；其余**数值字段一律占位**并在该行加注 `"_note": "PLACEHOLDER 待人类定值"`，不得视为平衡决策。
- 任何"该填多少/该怎么平衡/要不要加系统"的判断 → **停下**标 `[需人类定值]` 或 `[需 Claude 决策]`，继续其它独立步骤。

## 1. 目标终态

1. 配置 DTO 与运行时 State **彻底分离**，集中于 `Domain/Config/`，每类型一文件，字段不可变（只读/`init`）。
2. 全部配置表迁至 `Assets/StreamingAssets/Configs/Json/`，统一外层包 `{ "schemaVersion": N, "items": [ ... ] }`，可被 `NewtonsoftConfigRepository` 正确加载。
3. `IConfigRegistry`（Domain）在启动时由 `ConfigRegistry`（Application）加载全部表为不可变 DTO，提供只读 `Get<T>(id)` / `All<T>()`（落地规则 5）。
4. `WorldInitializer`（Application）从配置构建初始 `WorldState`，取代 `GameEntryPoint.StartNewGame` 里的 TODO。
5. **配置校验测试**作为 CI 门禁：唯一 id、外键完整、枚举可解析、必填、数值范围、schemaVersion。
6. batchmode 编译 0 error；新增 + 现有测试全绿。

## 2. 背景（审查已知现状）

- **配置加载潜伏 bug（本单修复）**：`NewtonsoftConfigRepository.LoadList<T>` 用 `DeserializeObject<List<T>>` 期望顶层数组，但现有 JSON 是 `{"items":[...]}` 对象 → 一旦加载即抛异常。T1 未暴露（尚无人调用加载）。
- **位置不符**：现有表在 `Assets/Configs/Json/`，而 `NewtonsoftConfigRepository` 读 `Application.streamingAssetsPath/Configs/Json` → 现读不到。
- **DTO 现状**：`ResourceConfig`/`TechConfig`/`PolicyConfig`（`Domain/Economy.cs`）、`UnitConfig`（`Domain/Unit.cs`）、`EventConfig`/`CommanderConfig`（`Domain/Politics.cs`）——与运行时 State 混在同文件。
- **缺口**：无 `CountryConfig`（`countries.json` 无对应 DTO）；无 `provinces.json` 且无 `ProvinceConfig`，但 `countries.capitalProvinceId` 指向 `iron_city`/`liberty_port`/`red_plain`/`coral_bay`/`high_peak`/`wind_plain`（外键悬空）。

## 3. 架构决策（写死）

1. **配置文件外层格式**：`{ "schemaVersion": 1, "items": [ {row}, ... ] }`。新增泛型包 `Domain/Config/ConfigFile.cs`：`class ConfigFile<T>{ public int schemaVersion; public List<T> items; }`。
2. **`NewtonsoftConfigRepository.LoadList<T>` 改为**反序列化 `ConfigFile<T>` 并返回 `.items`（修复 §2 bug）；`Load<T>` 保留单对象语义。
3. **配置 DTO 归位**：把上列 6 个 `*Config` 从 `Domain/{Economy,Unit,Politics}.cs` 移到 `Domain/Config/`（每类一文件，命名空间不变 `IronCrown.Domain`），运行时 State 留在原文件。
4. **新增 DTO**：`CountryConfig`、`ProvinceConfig`（字段见 §4）。**不引入** `BuildingConfig`/建筑系统（超本单范围）。
5. **配置访问**：`Domain/Config/IConfigRegistry`（只读：`T Get<T>(string id)`、`IReadOnlyList<T> All<T>()`、`bool Has<T>(string id)`）。Simulation 今后取数只走它（规则 5）。
6. **加载实现**：`Application/Config/ConfigRegistry : IConfigRegistry`，构造注入 `IConfigRepository`，`LoadAll()` 加载 resources/units/countries/provinces 全部表并缓存为不可变。DI 注册单例。
7. **世界初始化**：`Application/Setup/WorldInitializer`，方法 `WorldState CreateNewGame(IConfigRegistry config)`：遍历 `CountryConfig`→`CountryState`、`ProvinceConfig`→`ProvinceState` 装入 `WorldState`。字段一一映射（同名直拷）。
8. **接线**：`GameEntryPoint`（T1-FIX 后已是 `IStartable`）构造注入 `IConfigRegistry` + `WorldInitializer`；`Start()` 内先 `config.LoadAll()` 再 `_world = initializer.CreateNewGame(config)`。

## 4. 配置 Schema（DTO 字段，照此定义）

> 现有 `ResourceConfig`/`UnitConfig` 字段已与 `resources.json`/`units.json` 对齐，仅迁移文件、勿改字段。

**CountryConfig（新增，对齐 `countries.json`）**
```
string id; string name; string capitalProvinceId; string ideology;   // ideology 用字符串，加载后解析为 Ideology 枚举
int stability, warSupport, legitimacy, corruption, bureaucracy;
int treasury, taxIncome, tradeIncome, militaryExpense, civilExpense;
int civilianFactories, militaryFactories, dockyards;
int manpower, totalManpower;
Dictionary<string,int> resources;
```

**ProvinceConfig（新增；`provinces.json` 暂缺，需新建——见 §5）**
```
string id; string name; string terrain;        // terrain 字符串 → TerrainType 枚举
string ownerCountry; bool isCapital;
int population; int manpower;
int infrastructure; int railwayLevel; int portLevel; int airBaseLevel;
int industrySlots; string[] resourceOutput;
int victoryPoint;
```

## 5. 新建 `provinces.json`（仅结构 + 占位）

在 `Assets/StreamingAssets/Configs/Json/provinces.json` 建 6 行首都省份，`id`/`ownerCountry`/`isCapital` 照下表（由 `countries.json` 推导，属结构非平衡），其余数值占位并标注：

| province id | ownerCountry | isCapital |
|---|---|---|
| `iron_city` | `empire_north` | true |
| `liberty_port` | `republic_west` | true |
| `red_plain` | `alliance_east` | true |
| `coral_bay` | `kingdom_south` | true |
| `high_peak` | `federation_central` | true |
| `wind_plain` | `steppe_junta` | true |

- 占位规则：`terrain:"Plain"`、`population:0`、`infrastructure:1`、其余数值字段 `0`、`resourceOutput:[]`、`victoryPoint:0`，每行加 `"_note":"PLACEHOLDER 待人类定值"`。
- 文件外层同样用 `{ "schemaVersion":1, "items":[...] }`。
- **不得**自行编造省份名册/地形/人口等真实数值（规则 14）。

## 6. Phases

### P1 — 配置文件迁移 + 格式统一
- 移 `Assets/Configs/Json/{resources,units,countries}.json` → `Assets/StreamingAssets/Configs/Json/`（含 `.meta`）。
- 三表外层改为 `{ "schemaVersion":1, "items":[...] }`（现有 `{"items":[...]}` → 加 `schemaVersion`）。**行内数值一字不改**。
- 新建 `provinces.json`（§5）。
- 删空的 `Assets/Configs/Json/`（若 `Assets/Configs/Tables/` 另有用途则保留）。

### P2 — 配置 DTO 归位 + 新增
- 建 `Domain/Config/`：迁入 `ResourceConfig`/`TechConfig`(+`TechEffect`)/`PolicyConfig`(+`PolicyEffect`)/`UnitConfig`/`EventConfig`(+相关)/`CommanderConfig`；从原 State 文件移除这些类型。
- 新增 `CountryConfig.cs`、`ProvinceConfig.cs`（§4）、`ConfigFile.cs`（§3.1）。
- 验收：编译通过；运行时 State 文件不再含 `*Config`。

### P3 — 加载器修复 + Registry
- 修 `NewtonsoftConfigRepository.LoadList<T>`（§3.2，走 `ConfigFile<T>`）。
- 建 `Domain/Config/IConfigRegistry.cs`、`Application/Config/ConfigRegistry.cs`（`LoadAll()` 加载 4 表；`ideology`/`terrain` 字符串→枚举在此或 WorldInitializer 解析，二选一并保持一致）。
- DI：`GameLifetimeScope` 注册 `ConfigRegistry` 单例 As `IConfigRegistry`。

### P4 — 世界初始化接线
- 建 `Application/Setup/WorldInitializer.cs`（§3.7）。DI 注册单例。
- `GameEntryPoint` 注入 `IConfigRegistry`+`WorldInitializer`；`Start()`：`config.LoadAll()` → `_world = initializer.CreateNewGame(config)` → 日志输出国家/省份数量。
- 验收：运行启动场景，日志显示 6 国 6 省装入 `WorldState`。

### P5 — 配置校验测试（CI 门禁，规则 6）
新增 `Assets/Tests/EditMode/IronCrown.Config.Validation.Tests`（asmdef 配置同 T1-FIX 后的测试规范：引用 Domain/Application + TestRunner，`nunit` + `UNITY_INCLUDE_TESTS`）。直接读 `StreamingAssets/Configs/Json` 实测：
- 每表 `schemaVersion == 1`、`items` 非空。
- **唯一 id**：每表内 id 不重复。
- **枚举可解析**：`country.ideology` ∈ `Ideology`；`province.terrain` ∈ `TerrainType`。
- **外键完整**：
  - `unit.cost` 键 ∈ `resources.id`；
  - `country.resources` 键 ∈ `resources.id`；
  - `country.capitalProvinceId` ∈ `provinces.id`；
  - `province.ownerCountry` ∈ `countries.id`；
  - `province.resourceOutput` ⊆ `resources.id`。
- **必填非空**：各表 `id`/`name` 非空。
- **数值范围**：`country` 的 `stability/warSupport/legitimacy/corruption/bureaucracy` ∈ [0,100]；各 `*Factories`/`treasury`/`manpower` ≥ 0。
- 验收：以上对**现有数据**全绿（若现有数据违规则停下标 `[需人类定值]`，不擅自改数）。

### P6 — 收尾
- `CHANGELOG.md` 追加 `[T2]` 条目（关联规则 5,6,7）。
- batchmode 编译 + 全套 EditMode，导出结果附 PR。开 PR 指派 Claude 审查。

## 7. 文件清单（新增/移动）

| 动作 | 路径 |
|---|---|
| 移动 | `Assets/Configs/Json/{resources,units,countries}.json` → `Assets/StreamingAssets/Configs/Json/` |
| 新增 | `Assets/StreamingAssets/Configs/Json/provinces.json` |
| 新增 | `Domain/Config/ConfigFile.cs`、`IConfigRegistry.cs`、`CountryConfig.cs`、`ProvinceConfig.cs` |
| 移动 | `Domain/{Economy,Unit,Politics}.cs` 中的 `*Config` → `Domain/Config/*.cs` |
| 新增 | `Application/Config/ConfigRegistry.cs`、`Application/Setup/WorldInitializer.cs` |
| 修改 | `Infrastructure/Config/NewtonsoftConfigRepository.cs`（LoadList 走 ConfigFile<T>） |
| 修改 | `Bootstrap/GameLifetimeScope.cs`、`Bootstrap/GameEntryPoint.cs`（接线） |
| 新增 | `Assets/Tests/EditMode/IronCrown.Config.Validation.Tests.asmdef` + 测试类 |

## 8. 验收门禁（Definition of Done）

- [ ] 4 张表在 `StreamingAssets/Configs/Json/`，`{schemaVersion,items}` 格式，**现有数值未改**。
- [ ] 配置 DTO 全在 `Domain/Config/`；运行时 State 文件无 `*Config`。
- [ ] `LoadList` 修复，启动能加载 6 国 6 省进 `WorldState`。
- [ ] `IConfigRegistry` 经 DI 注入；Simulation/初始化取数只走它（无硬编码数值，规则 5）。
- [ ] 配置校验测试存在且对现有数据全绿（规则 6）。
- [ ] batchmode 0 error；EditMode 全绿（附 results）。
- [ ] 未发明/改动任何平衡数值（规则 14）；未引入建筑等额外系统（规则 8/9）。
- [ ] 改动在 `feature/t2-config-pipeline`，PR 待审；`CHANGELOG.md` 已更新（规则 7,10）。

## 9. 歧义处理
遇本单未指定细节、或需定数值/玩法/新系统 → 停下标 `[需人类定值]`/`[需 Claude 决策]`，写进 PR 描述，继续其它独立步骤。

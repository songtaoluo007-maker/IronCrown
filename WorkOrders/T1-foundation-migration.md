# 工作单 T1 — Foundation Migration（铁冠计划 / Project Iron Crown）

| 项 | 值 |
|---|---|
| 工作单号 | T1 |
| 标题 | Core 拆分 + 运行时/存档模型分离 + DI(VContainer) + Newtonsoft |
| 执行者 | **OpenClaw（DeepSeek V4-Pro）** |
| 出单/审查 | Claude（主架构师，规则 13） |
| 上游依据 | `PROJECT_RULES.md`、`ARCHITECTURE.md`（尤其附录 A/B）、`CHANGELOG.md` |
| 分支 | `feature/t1-foundation-migration`（从主干切出；**禁止直推 main**，规则 10） |
| 目标终态 | Unity 6 LTS 下**零编译错误**；`IronCrown.Core` 程序集消失；核心层 Unity-free；每模块有 EditMode 测试且全绿；CHANGELOG 已更新 |

---

## 0. 执行者角色边界（务必先读）

- 规则 12：OpenClaw 只做**实现与流程自动化**，**不做架构 / 数值 / 玩法决策**。本单已把所有架构决策写死。
- **遇到本单未覆盖的设计选择 → 立即停止该步，在产出里标注 `[需 Claude 决策]` 并继续其它独立步骤，禁止自行发挥**（尤其禁止：自创新系统、改游戏数值/公式、改 PRNG 算法、把 float 改成整数、改 UI、删除看不懂的代码）。
- 本单只动**结构与接线**。所有现有**数值、公式、游戏逻辑一字不改**（float 也保留原样，整数化是后续独立任务）。
- 小步提交：每个 Phase 至少一次提交，使用 Conventional Commits（如 `refactor(core): move EventBus to Domain`）。每次提交前确认未破坏前一个 Phase 的验收。

---

## 1. 前置条件（Prerequisites）—— ⚠ 阻塞项

**当前 `E:\IronCrown` 不是已初始化的 Unity 工程**：缺少 `Packages/`、`ProjectSettings/`，且无任何 `.meta` 文件。现有 8 个 `.asmdef` 在 Unity 导入前不生效。

执行 Phase 1+ 之前**必须**满足：

1. 已安装 **Unity 6 LTS** 编辑器（与团队约定的具体小版本一致）。
2. 以该版本**打开一次** `E:\IronCrown`（Unity Hub → Add → 选此目录），让 Unity 生成 `ProjectSettings/`、`Packages/manifest.json`、`Library/`，并为所有现有文件（含 `.asmdef`、`.cs`、`.json`）生成 `.meta`。
3. 确认 `ProjectSettings/ProjectVersion.txt` 记录的版本是 Unity 6 LTS。
4. 把新生成的 `.meta` 一并纳入版本控制（提交：`chore: initialize Unity 6 LTS project (settings + meta)`）。

> 若 OpenClaw 所在机器**无 Unity 安装或无法初始化**，则在此停止并标注 `[需人类/环境初始化 Unity 工程]`——后续所有"编译/测试/装包"步骤都依赖它。本单其余 Phase 的**代码改动**可以先写（纯文本编辑），但**验收（编译/测试）必须在工程初始化后进行**。

---

## 2. 背景：为什么做这个（审查发现）

现有 `Assets/Scripts` 是早期 stub，存在以下结构问题与**既有编译错误**（与 asmdef 无关）：

1. `Core` 含 Unity 耦合（`ConfigLoader`/`SaveSystem` 用 `Application.dataPath` + `JsonUtility`），且被 `Simulation` 引用 → 核心层不纯净、无法纯 C# 单测。
2. 三个单例（`EventBus.Instance`/`ConfigLoader.Instance`/`SaveSystem.Instance`）= 全局可变状态，破坏可测性与确定性。
3. `JsonUtility` 不支持 `Dictionary`（`CountryState.resources`、`UnitConfig.cost`、`PolicyConfig.requirements` 等）→ 存档/配置静默丢数据。
4. **存档 DTO 与运行时模型混用（根因）**：5 个 resolver（`Economy`/`Politics`/`Supply`/`AI`/`Diplomacy`）签名把存档 DTO `GameState` 当"世界状态"参数，却没 `using IronCrown.Core` → **5 处"找不到 GameState"编译错误**；`TurnResolver` 又把 `CountrySaveData`（存档 DTO）传进期望 `CountryState`（运行时）的 resolver → **类型不匹配编译错误**。

**为何本单合并 T1 与 T2-最小集**：`GameState` 既是存档 DTO 又被 Simulation 当世界状态用，"拆 Core"会强制决定 `GameState` 归属，从而强制处理运行时/存档模型分离。二者无法在"保持可编译"的前提下拆开，故合并为一个能交付可编译终态的工作单。运行时世界根采用扩展后的 `WorldState`（见 Phase 4）。

---

## 3. 架构决策（已拍死，OpenClaw 直接照做）

1. **运行时世界根 = `WorldState`**（扩展为持有 country/province/unit/relation 集合）。Simulation **只认运行时 `WorldState`/`CountryState`**，永不接触存档 DTO。
2. **存档 DTO（`GameState`/`CountrySaveData`/`ProvinceSaveData`/`UnitSaveData`）归 `Application` 层**，仅由 `ISaveRepository` 实现与 `SaveMapper` 使用。
3. **命名空间 = 程序集**：文件移动后命名空间随层改（见 §10 映射表）。
4. **事件结构体与 `EventBus` 暂留 `Domain`**（随 EventBus 迁移）；"事件→Contracts"是后续任务，本单不做。
5. **`GamePhase` 枚举随 `GameClock` 迁入 `Domain`**；"枚举→Contracts"是后续任务。
6. **PRNG 仍用 `System.Random`**（`RandomService` 实现 `IRandom`，方法体不变）；自定义确定性 PRNG 是后续任务。
7. **`BattleResolver` 的 float 战斗公式保持不变**；本单只把它的 `EventBus.Instance` 换成注入、`RandomService` 换成 `IRandom`。
8. `turnNumber` 在 `WorldState`/`GameClock`/`GameState` 三处冗余 → **本单不去重**；以 `GameClock` 为回合/阶段权威。

---

## 4. 接口契约（Claude 定义，OpenClaw 照此创建文件并实现 impl）

> 这些是架构"接缝"，签名按下方原样创建；实现体由 OpenClaw 编写。

**`Assets/Scripts/Domain/Abstractions/`（命名空间 `IronCrown.Domain`）**

```csharp
public interface IEventPublisher {
    void Subscribe<T>(System.Action<T> handler);
    void Unsubscribe<T>(System.Action<T> handler);
    void Publish<T>(T evt);
    void Clear();
}

public interface IRandom {           // 整数优先；NextDouble 暂留供现有 float 公式使用（后续整数化时移除）
    int Seed { get; }
    int Next(int maxExclusive);
    int Range(int minInclusive, int maxExclusive);
    bool Roll(int percentChance);    // 0-100
    double NextDouble();
    double RangeDouble(double min, double max);
    void Reset();
    void Reset(int newSeed);
}

public interface ITurnClock {
    int CurrentTurn { get; }
    int MaxTurns { get; set; }
    GamePhase CurrentPhase { get; }
    bool IsPaused { get; set; }
    void AdvancePhase();
    void Reset(int maxTurns = 60);
}
```

**`Assets/Scripts/Application/Ports/`（命名空间 `IronCrown.Application`）**

```csharp
public interface IConfigRepository {
    T Load<T>(string configName) where T : class;
    System.Collections.Generic.List<T> LoadList<T>(string configName) where T : class;
    void ClearCache();
}

public interface ISaveRepository {                       // GameState 为 Application 层存档 DTO（见 Phase 4）
    bool Save(string slot, GameState state);
    GameState Load(string slot);
    bool Delete(string slot);
    string[] ListSaves();
}

public interface IAppLogger {                            // 取代散落的 UnityEngine.Debug
    void Info(string msg);
    void Warn(string msg);
    void Error(string msg);
}
```

---

## 5. Phase 1 — 装包（Packages）

编辑 `Packages/manifest.json`，合并以下内容（保留现有项；版本为已知可用下限，OpenClaw 取 OpenUPM/UPM 上**兼容 Unity 6 LTS 的最新稳定版**）：

```jsonc
{
  "scopedRegistries": [
    { "name": "package.openupm.com", "url": "https://package.openupm.com",
      "scopes": [ "jp.hadashikick.vcontainer" ] }
  ],
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    "jp.hadashikick.vcontainer": "1.16.6"
  }
}
```

**验收**：Unity 重新导入无包解析错误；能 `using Newtonsoft.Json;` 与 `using VContainer;`。

---

## 6. Phase 2 — 新增接口文件（纯加法，不破坏现有）

按 §4 创建 6 个接口文件于 `Domain/Abstractions/` 与 `Application/Ports/`。
**验收**：编译仍能进行到原有错误处（不新增错误；接口本身无依赖问题）。

---

## 7. Phase 3 — 迁移纯 Core → Domain（去单例 + 实现接口）

移动并改命名空间为 `IronCrown.Domain`：

| 源文件 | 目标 | 改动要点 |
|---|---|---|
| `Core/EventBus.cs` | `Domain/Messaging/EventBus.cs` | 删除 `static Instance`；`class EventBus : IEventPublisher`；**事件结构体随此文件迁入 `Domain`** |
| `Core/GameClock.cs` | `Domain/Time/GameClock.cs` | `class GameClock : ITurnClock`；构造改为 `GameClock(IEventPublisher events)`；内部 `EventBus.Instance.Publish` → `_events.Publish`；`GamePhase` 枚举随此文件迁入 `Domain` |
| `Core/RandomService.cs` | `Domain/Random/RandomService.cs` | `class RandomService : IRandom`；**方法体不变**（仍用 `System.Random`） |

同步更新调用方（仅删用不到的 `using IronCrown.Core`；这些类型现已在 `IronCrown.Domain`，调用方多数已 `using IronCrown.Domain`）：
- `Simulation/TurnResolver.cs`、`Simulation/BattleResolver.cs`：去掉 `using IronCrown.Core;`。

**验收**：`EventBus`/`GameClock`/`RandomService` 在 `IronCrown.Domain` 下编译通过；无 `*.Instance` 单例残留。

---

## 8. Phase 4 — 迁移 Unity Core → Infrastructure（换 Newtonsoft）+ 存档 DTO → Application + 扩展 WorldState

**8.1 ConfigLoader → Infrastructure（命名空间 `IronCrown.Infrastructure`）**
- 新文件 `Infrastructure/Config/NewtonsoftConfigRepository.cs`：`class NewtonsoftConfigRepository : IConfigRepository`。
- 用 `Newtonsoft.Json`（`JsonConvert.DeserializeObject<List<T>>` 直接支持顶层数组与 `Dictionary` 字段，**删除原 JsonListWrapper hack**）。
- 读取路径改为 `Application.streamingAssetsPath`（运行时配置目录），编辑器/独立平台用 `File.ReadAllText`。
- ⚠ **Android 注意**：StreamingAssets 在 APK 内需用 `UnityWebRequest` 读取——本单仅实现 Editor/Standalone 的 `File.IO` 版本，Android 适配标注为 `[TODO: Android StreamingAssets via UnityWebRequest]`（后续任务）。
- 统一 `JsonSerializerSettings`（驼峰、枚举转字符串）置于 Infrastructure，供配置与存档共用。

**8.2 SaveSystem → Infrastructure**
- 新文件 `Infrastructure/Persistence/FileSaveRepository.cs`：`class FileSaveRepository : ISaveRepository`，`Application.persistentDataPath`，Newtonsoft 序列化（Dictionary 现可正常存）。

**8.3 存档 DTO → Application（命名空间 `IronCrown.Application`）**
- 从原 `Core/SaveSystem.cs` 拆出 `GameState`/`CountrySaveData`/`ProvinceSaveData`/`UnitSaveData` → 新文件 `Application/Persistence/SaveModels.cs`。

**8.4 扩展运行时世界根 `WorldState`**
- 把 `WorldState` 从 `Domain/Diplomacy.cs` 拆到独立文件 `Domain/State/WorldState.cs`（命名空间不变 `IronCrown.Domain`）。
- 扩展字段（保留现有 `worldTension`/`turnNumber` 及方法 `AddTension`/`TensionAllows`）：
```csharp
public System.Collections.Generic.Dictionary<string, CountryState> countries = new();
public System.Collections.Generic.Dictionary<string, ProvinceState> provinces = new();
public System.Collections.Generic.Dictionary<string, UnitState>     units     = new();
public System.Collections.Generic.List<DiplomacyRelation>           relations = new();
```

**8.5 映射器 `SaveMapper`（`Application/Mapping/SaveMapper.cs`，命名空间 `IronCrown.Application`）**
```csharp
public static class SaveMapper {
    public static GameState  ToSave(WorldState world, int seed, GamePhase phase);
    public static WorldState ToRuntime(GameState save);
}
```
- 映射**仅覆盖存档 DTO 中已存在的字段**（DTO 是运行时 State 的子集快照），其余运行时字段由默认值/配置重建。**存档字段完整化是后续任务**，本单不扩展 DTO 字段。

**验收**：Infrastructure/Application 编译通过；`GameState` 不再被 `Simulation` 引用。

---

## 9. Phase 5 — 修正 Simulation 使用运行时模型（消除既有编译错误）

逐文件把"世界状态参数"类型 `GameState` → `WorldState`（其余签名与方法体不变）：

| 文件 | 签名变更 |
|---|---|
| `TurnResolver.cs` | `ExecuteTurn(WorldState world)`；各阶段方法 `(GameState)`→`(WorldState)`；遍历改为 `world.countries.Values` **按 `id` 升序**（`OrderBy(c => c.id, System.StringComparer.Ordinal)`，加 `using System.Linq;`，确定性）；构造注入 `ITurnClock`/`IEventPublisher`，去 `EventBus.Instance` |
| `EconomyResolver.cs` | `ResolveEconomy/ResolveProduction/CalculateMilitaryExpense` 第二参数 `GameState`→`WorldState` |
| `PoliticsResolver.cs` | `ResolvePolitics/ResolveWarSupport` 第二参数 `GameState`→`WorldState` |
| `SupplyResolver.cs` | `CheckSupply` 第二参数 `GameState`→`WorldState` |
| `AIResolver.cs` | `MakeDecisions` 及所有私有方法的 `GameState`→`WorldState` |
| `DiplomacyResolver.cs` | `ResolveDiplomacy(WorldState world)` |
| `BattleResolver.cs` | 构造 `BattleResolver(IRandom rng, IEventPublisher events)`；字段 `RandomService`→`IRandom`；`EventBus.Instance.Publish`→`_events.Publish`；**float 公式不动** |

**验收**：§2 所列 6 处编译错误全部消失。

---

## 10. Phase 6 — Bootstrap + VContainer 接线

- `Infrastructure/GameManager.cs` → `Bootstrap/GameEntryPoint.cs`（命名空间 `IronCrown.Bootstrap`）。改为实现 VContainer `IStartable`，依赖**构造注入**（不再 `new` 任何系统、不再用单例）。原 `Awake/Start` 流程迁到 `Start()`。
- 新文件 `Bootstrap/GameLifetimeScope.cs`：`class GameLifetimeScope : VContainer.Unity.LifetimeScope`，挂在启动场景的 Bootstrap GameObject 上。注册清单：

| 注册 | 生命周期 | 备注 |
|---|---|---|
| `IEventPublisher → EventBus` | Singleton | |
| `IRandom → RandomService` | Singleton | 用 `RegisterInstance<IRandom>(new RandomService(initialSeed))`；`initialSeed` 暂用 EntryPoint 上的 `[SerializeField] int`（默认 12345）。真正种子由新游戏/读档 `Reset(seed)` 设定（后续任务） |
| `ITurnClock → GameClock` | Singleton | 依赖 `IEventPublisher`（容器自动注入） |
| `IConfigRepository → NewtonsoftConfigRepository` | Singleton | |
| `ISaveRepository → FileSaveRepository` | Singleton | |
| `IAppLogger → UnityAppLogger` | Singleton | Infrastructure 新增 `Logging/UnityAppLogger.cs`，包 `UnityEngine.Debug` |
| `EconomyResolver`/`PoliticsResolver`/`BattleResolver`/`SupplyResolver`/`AIResolver`/`DiplomacyResolver` | Singleton | BattleResolver 依赖 `IRandom`+`IEventPublisher` |
| `TurnResolver` | Singleton | 依赖 6 个 resolver + `ITurnClock` + `IEventPublisher` |
| `RegisterEntryPoint<GameEntryPoint>()` | — | 容器注入依赖并调用 `Start()` |

- 存读档：EntryPoint 通过 `ISaveRepository` + `SaveMapper` 在运行时 `WorldState` 与存档 `GameState` 间转换。

**验收**：无手写 `new` 系统、无 `*.Instance`；启动场景含 `GameLifetimeScope`。

---

## 11. Phase 7 — 更新 asmdef 并删除 Core

| asmdef | references 终态 | noEngineReferences |
|---|---|---|
| `IronCrown.Domain` | `[]` | true |
| `IronCrown.Simulation` | `["IronCrown.Domain"]` | true |
| `IronCrown.Application` | `["IronCrown.Domain","IronCrown.Simulation"]` | true |
| `IronCrown.Infrastructure` | `["IronCrown.Application","IronCrown.Domain"]` | false |
| `IronCrown.Presentation` | `["IronCrown.Application"]` | false |
| `IronCrown.Bootstrap` | `["IronCrown.Domain","IronCrown.Simulation","IronCrown.Application","IronCrown.Infrastructure","IronCrown.Presentation","VContainer"]` | false |
| `IronCrown.Contracts` | `[]` | true |
| ~~`IronCrown.Core`~~ | **删除该 asmdef + 空 `Core/` 目录（连同 `.meta`）** | — |

- 注：`Newtonsoft.Json` 为 autoReferenced 预编译程序集，`overrideReferences=false` 时 Infrastructure 无需显式引用即可用。`VContainer` 需在 Bootstrap 显式引用（如上）。
- 注：Domain/Application/Contracts 在本单仍 `references[]` 或不含 Contracts（Contracts 仍为空；待后续任务填充枚举/事件/只读模型后再接线）。

**验收**：全仓 `grep "IronCrown.Core"` 无残留（命名空间与 asmdef 均无）。

---

## 12. Phase 8 — 测试（规则 6：每个核心模块必须有单元测试）

在 `Assets/Tests/EditMode/` 建测试程序集并实现用例（NUnit / Unity Test Framework）。测试 asmdef 需引用被测程序集 + `UnityEngine.TestRunner`、`UnityEditor.TestRunner`，并含 `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`。

| 测试程序集 | 必备用例（最小集） |
|---|---|
| `IronCrown.Domain.Tests` | ① `RandomService` 同种子同序列结果一致；`Reset(seed)` 可复现。② `EventBus` 订阅→发布→收到；`Unsubscribe` 后不再收到。③ `WorldState` 字典增删查正常 |
| `IronCrown.Simulation.Tests` | `EconomyResolver.ResolveEconomy`：给定固定 `CountryState`（税收/稳定度/通胀等已知值），断言 `EconomyResult.netIncome` 等于按**现有公式**手算的期望值（锁定当前行为，防回归） |
| `IronCrown.Application.Tests` | `SaveMapper` 往返：`ToSave` → `ToRuntime` 后，DTO 覆盖字段一致（无损） |

提供给执行者的 Unity 无头测试命令（路径/版本按实际替换）：
```
"<UnityPath>/Unity.exe" -batchmode -runTests -projectPath "E:\IronCrown" ^
  -testPlatform EditMode -testResults "E:\IronCrown\artifacts\editmode-results.xml" -quit
```

**验收**：EditMode 全绿；结果 XML 无 failed。

---

## 13. Phase 9 — 收尾

1. 更新 `CHANGELOG.md`：在 `[Unreleased]` 追加 `[T1]` 条目（日期 + 改动摘要 + 关联规则 3,4,5,6）。
2. 按 §14 自检门禁逐项核对。
3. 推送 `feature/t1-foundation-migration` 并开 PR，**指派 Claude 审查**（规则 13）。PR 描述列出：完成的 Phase、遗留 `[需 Claude 决策]`/`[TODO]` 项、测试结果。

---

## 14. 验收门禁 / Definition of Done（PR 合入前必须全绿）

- [ ] Unity 6 LTS 下**零编译错误、零警告新增**。
- [ ] `IronCrown.Core` 程序集与命名空间**彻底消失**；`Core/` 目录已删。
- [ ] 核心层 `Domain/Simulation/Application/Contracts` 无 `UnityEngine` 引用（`noEngineReferences=true` 未被关闭）。
- [ ] 无 `static * Instance` 单例（`EventBus`/`ConfigLoader`/`SaveSystem`）。
- [ ] 序列化全部走 `Newtonsoft.Json`，无 `JsonUtility` 残留；`Dictionary` 字段可正常序列化。
- [ ] `Simulation` 不引用任何存档 DTO（`GameState` 等）；resolver 仅用运行时 `WorldState`/`CountryState`。
- [ ] DI 经 `GameLifetimeScope` 装配；无手写跨层 `new`。
- [ ] 三个测试程序集存在且 EditMode 全绿（规则 6）。
- [ ] **未改任何游戏数值/公式**（float、概率、系数原样）；未做未授权的额外重构（规则 9）。
- [ ] `CHANGELOG.md` 已更新（规则 7）；来自 feature 分支而非直推 main（规则 10）。

---

## 15. 文件移动 / 命名空间映射总表

| 源 | 目标 | 命名空间 | 程序集 |
|---|---|---|---|
| `Core/EventBus.cs`(+事件结构体) | `Domain/Messaging/EventBus.cs` | `IronCrown.Core` → `IronCrown.Domain` | Domain |
| `Core/GameClock.cs`(+`GamePhase`) | `Domain/Time/GameClock.cs` | → `IronCrown.Domain` | Domain |
| `Core/RandomService.cs` | `Domain/Random/RandomService.cs` | → `IronCrown.Domain` | Domain |
| `Core/ConfigLoader.cs` | `Infrastructure/Config/NewtonsoftConfigRepository.cs` | → `IronCrown.Infrastructure` | Infrastructure |
| `Core/SaveSystem.cs`(类) | `Infrastructure/Persistence/FileSaveRepository.cs` | → `IronCrown.Infrastructure` | Infrastructure |
| `Core/SaveSystem.cs`(DTO) | `Application/Persistence/SaveModels.cs` | → `IronCrown.Application` | Application |
| `Domain/Diplomacy.cs`(`WorldState`) | `Domain/State/WorldState.cs`(扩展) | `IronCrown.Domain`(不变) | Domain |
| `Infrastructure/GameManager.cs` | `Bootstrap/GameEntryPoint.cs` | → `IronCrown.Bootstrap` | Bootstrap |
| （新）DI 装配 | `Bootstrap/GameLifetimeScope.cs` | `IronCrown.Bootstrap` | Bootstrap |
| （新）日志 | `Infrastructure/Logging/UnityAppLogger.cs` | `IronCrown.Infrastructure` | Infrastructure |

> 移动文件时务必同时移动/删除对应 `.meta`（Unity 会为新位置重新生成）。`Domain/Diplomacy.cs` 中除 `WorldState` 外的类型（`DiplomacyRelation`/`DiplomacyStatus`）**保留原处**。

---

## 16. 歧义处理协议

遇到下列情况**停止该步并标注**，不要自行决定：
- 本单未指定的命名空间/目录/签名细节，且有多种合理写法。
- 现有代码与本单描述不符（签名/字段与上游审查时不一致）。
- 需要改动游戏数值、公式、PRNG 算法、float→整数、UI、或新增系统。
- 包版本与 Unity 6 不兼容。
标注格式：在 PR 描述与对应代码处写 `[需 Claude 决策] <问题与你建议的选项>`。

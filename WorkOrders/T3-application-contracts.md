# 工作单 T3 — 应用用例层与 Contracts（Application Use-Cases & Contracts）

| 项 | 值 |
|---|---|
| 工作单号 | T3（路线图：应用层用例 + Contracts 落地） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查 | Claude（规则 13） |
| 分支 | `feature/t3-application-contracts` |
| 前置 | T2 已完成 + 本单 Phase 0 收尾通过 |
| 角色边界 | 规则 12：只实现、不做架构/数值/玩法决策；本单已写死结构。遇未覆盖点停下标 `[需 Claude 决策]` |

## 0. 目标与意义

让 UI（下一板块 T6）能在**不引用 Domain/Simulation**（规则 4）的前提下：① 读只读模型展示世界；② 订阅领域事件刷新；③ 调用 Application 门面驱动游戏。本单填充至今为空的 `Contracts` 程序集，并新增 Application 用例门面 `GameSessionService`。

**本单只做结构与编排，不碰任何游戏数值/公式（规则 9/14）。**

## Phase 0 — 前置必办（T2 收尾，未过不得开 T3）

> 以下为上一轮遗留 + Claude 已手修项的确认。

1. **[G1] 取消跟踪 `Library/`**：`git rm -r --cached Library/`，确认 `.gitignore` 生效；提交当前未提交的 `.meta` 变更。`git status` 应不再出现 `Library/`。
2. **[D5] 补测试证据**：在 Unity 6 LTS 跑 **全部 EditMode**（T1 的 Domain/Simulation/Application.Tests + T2 的 Config.Validation.Tests），导出 `artifacts/editmode-results.xml`，确认**全绿**；另跑 batchmode 编译确认 **0 error**。两者附 PR。
3. **确认 Claude 手修已编译通过**：`Bootstrap/GameEntryPoint.cs` 现为 `GameEntryPoint : VContainer.Unity.IStartable`（已移除多余 `using UnityEngine`）；`IronCrown.Bootstrap.asmdef` references 已含 `IronCrown.Contracts`、`IronCrown.Presentation`。**勿回退这些改动**。
   - 顺带验证 `RegisterEntryPoint<GameEntryPoint>()` 现在确会调用 `Start()`（运行启动场景见 `[EntryPoint] 游戏启动` / `配置加载完成` / 国家省份数量日志）。

## 1. 架构决策（已拍死）

1. **Contracts 内容**：领域事件结构体 + `IEventPublisher` 接口 + 只读模型（ReadModel）DTO。**Contracts 必须零依赖**（`references: []` 不变），因此其中类型**只用基元/字符串/字典**，不得引用 Domain 的枚举或 State。
2. **枚举暂不迁入 Contracts**：`Ideology`/`TerrainType`/`GamePhase` 等留在 Domain。ReadModel 中相关字段一律用 **string**（如 `ideology` 存 `cfg.ideology.ToString()`、`phase` 存 `GamePhase.ToString()`）。
3. **命令（Command DTO）本单不引入**：MVP 用 `GameSessionService` 显式方法即可满足规则 4；正式命令总线待有大量玩法操作时再做。
4. **`GameSessionService` 为 Application 门面**，持有运行时 `WorldState`，是 UI 唯一合法入口（规则 4）。`GameEntryPoint` 瘦身为只调用它。
5. **事件订阅**：UI 后续通过注入的 `IEventPublisher`（迁至 Contracts 后 Presentation 可引用）订阅；本单不写 UI。

## 2. Contracts 类型定义（照此创建，命名空间 `IronCrown.Contracts`）

**`Contracts/Events/`**（从 `Domain/Messaging/EventBus.cs` 迁出，逐个结构体，内容不变）
`TurnStartEvent`/`TurnEndEvent`/`ResourceChangedEvent`/`ProvinceOwnerChangedEvent`/`BattleResolvedEvent`/`PolicyChangedEvent`/`TechCompletedEvent`/`DiplomacyChangedEvent`。

**`Contracts/Abstractions/IEventPublisher.cs`**（从 `Domain/Abstractions/` 迁入，签名不变：`Subscribe/Unsubscribe/Publish/Clear`）。

**`Contracts/ReadModels/WorldView.cs`**
```
public sealed class WorldView {
    public int turn;
    public string phase;          // GamePhase.ToString()
    public int worldTension;
    public System.Collections.Generic.List<CountryView> countries;
}
```

**`Contracts/ReadModels/CountryView.cs`**
```
public sealed class CountryView {
    public string id;
    public string name;
    public string ideology;       // Ideology.ToString()
    public int treasury;
    public int stability;
    public int warSupport;
    public int legitimacy;
    public int civilianFactories;
    public int militaryFactories;
    public int dockyards;
    public int manpower;
    public System.Collections.Generic.Dictionary<string,int> resources;
}
```
> `ProvinceView` 等待地图 UI 需要时再加（本单不做）。

## 3. asmdef 引用更新

| asmdef | references 终态 | 说明 |
|---|---|---|
| `IronCrown.Contracts` | `[]` | **保持零依赖** |
| `IronCrown.Domain` | `["IronCrown.Contracts"]` | EventBus 实现 + GameClock 发事件需引用 Contracts |
| `IronCrown.Simulation` | `["IronCrown.Domain","IronCrown.Contracts"]` | resolver 发事件 |
| `IronCrown.Application` | `["IronCrown.Domain","IronCrown.Simulation","IronCrown.Contracts"]` | ReadModel/GameSessionService |
| `IronCrown.Infrastructure` | 不变 `["IronCrown.Application","IronCrown.Domain"]` | 不涉及事件/ReadModel |
| `IronCrown.Presentation` | 不变（空，T6 再加 Contracts） | — |
| `IronCrown.Bootstrap` | 不变（Claude 已含 Contracts/Presentation） | — |

## 4. Phases

### P1 — 事件 + IEventPublisher 迁入 Contracts
- 8 个事件结构体从 `Domain/Messaging/EventBus.cs` 移到 `Contracts/Events/`（命名空间改 `IronCrown.Contracts`）。`EventBus` 类**留在 Domain**。
- `IEventPublisher` 从 `Domain/Abstractions/` 移到 `Contracts/Abstractions/`（命名空间改 `IronCrown.Contracts`）。`EventBus : IEventPublisher` 保持。
- 更新引用：所有发/订事件处加 `using IronCrown.Contracts;`（`GameClock`/`TurnResolver`/`BattleResolver`/`EventBus`/测试）。
- 更新 Domain/Simulation/Application asmdef references（§3）。
- 验收：编译 0 error；`EventBusTests` 仍绿。

### P2 — ReadModel DTO
- 按 §2 建 `WorldView`、`CountryView` 于 `Contracts/ReadModels/`。

### P3 — ReadModelBuilder（Application/Queries/）
- `public sealed class ReadModelBuilder`：
  - `WorldView BuildWorldView(WorldState world, ITurnClock clock)` — `turn=clock.CurrentTurn`、`phase=clock.CurrentPhase.ToString()`、`worldTension=world.worldTension`、`countries` 按 `id` 升序映射。
  - `CountryView BuildCountryView(CountryState c)` — 字段一一映射，`ideology=c.ideology.ToString()`，`resources=new Dictionary<>(c.resources)`（拷贝，勿暴露内部引用）。
- 纯映射，无副作用。

### P4 — GameSessionService（Application/Session/）
- 持有 `private WorldState _world;`，构造注入 `ITurnClock`、`IConfigRegistry`、`WorldInitializer`、`TurnResolver`、`ISaveRepository`、`IRandom`、`ReadModelBuilder`、`IAppLogger`。
- 方法（把 `GameEntryPoint` 现有编排逻辑迁入，行为不变）：
  - `void NewGame(int? seed = null)`：`if(seed.HasValue) _rng.Reset(seed.Value);` → `config.LoadAll()` → `_clock.Reset(60)` → `_world = initializer.CreateNewGame(config)`。
  - `void AdvancePhase()`：复制 `GameEntryPoint.NextPhase` 逻辑（`TurnStart` 时 `turnResolver.ExecuteTurn(_world)`，再 `clock.AdvancePhase()`）。
  - `bool Save(string slot)` / `bool Load(string slot)`：用 `SaveMapper` + `ISaveRepository`（行为同现 EntryPoint；seed/phase 持久化仍属后续"存读档闭环"任务，本单不扩展 DTO）。
  - `WorldView GetWorldView()`：`return _builder.BuildWorldView(_world, _clock);`
- DI：`GameLifetimeScope` 注册 `ReadModelBuilder`、`GameSessionService`（Singleton）。

### P5 — GameEntryPoint 瘦身
- `GameEntryPoint` 改为只注入 `GameSessionService` + `IAppLogger`；`Start()` → `_logger.Info(...)` + `_session.NewGame()`。删除其重复的 clock/config/turnResolver 等字段与方法（编排逻辑已搬到 service）。
- 保持 `: IStartable`（Claude 已加，勿动）。

### P6 — 测试（Application.Tests，规则 6）
- `ReadModelBuilderTests`：给定 `WorldState`（2 国，已知字段）→ `BuildWorldView` 的 `turn/phase/countries` 数量与字段正确；`countries` 按 id 升序；`ideology`/`phase` 为字符串。
- `GameSessionServiceTests`：`NewGame()` 后 `GetWorldView().countries.Count == 6`（用真实配置，或注入测试用 IConfigRegistry stub）；`AdvancePhase()` 推进 `clock` 阶段。
  - 若需隔离配置，可注入轻量 fake `IConfigRegistry`/`IConfigRepository`（测试内 stub），避免依赖 StreamingAssets。

### P7 — 收尾
- `CHANGELOG.md` 追加 `[T3]` 条目（关联规则 4,6,7）。
- batchmode 编译 0 error + 全套 EditMode 全绿，导出结果附 PR。开 PR 指派 Claude 审查。

## 5. 文件清单

| 动作 | 路径 |
|---|---|
| 移动 | 事件结构体：`Domain/Messaging/EventBus.cs` → `Contracts/Events/*.cs` |
| 移动 | `Domain/Abstractions/IEventPublisher.cs` → `Contracts/Abstractions/IEventPublisher.cs` |
| 新增 | `Contracts/ReadModels/WorldView.cs`、`CountryView.cs` |
| 新增 | `Application/Queries/ReadModelBuilder.cs`、`Application/Session/GameSessionService.cs` |
| 修改 | `Bootstrap/GameEntryPoint.cs`（瘦身）、`Bootstrap/GameLifetimeScope.cs`（注册）；`Domain/Simulation/Application` 三个 asmdef（+Contracts 引用）；各发/订事件文件加 `using IronCrown.Contracts;` |
| 新增 | `Assets/Tests/EditMode/IronCrown.Application.Tests/ReadModelBuilderTests.cs`、`GameSessionServiceTests.cs` |

## 6. 验收门禁（DoD）

- [ ] Phase 0 全过（`Library/` 已 untrack；EditMode 全绿且附 results；手修项编译通过）。
- [ ] `Contracts` 仍 `references: []`，其中类型只含基元/字符串/字典（无 Domain 枚举/State 依赖）。
- [ ] 事件 + `IEventPublisher` 已在 Contracts；`EventBus` 实现仍在 Domain；编译 0 error。
- [ ] `GameSessionService` 为唯一持有 `WorldState` 的门面；`GameEntryPoint` 仅委托它。
- [ ] `ReadModelBuilder` 映射正确、`countries` 按 id 升序、枚举转字符串。
- [ ] 新增测试 + 现有测试全绿（规则 6）。
- [ ] **未改任何游戏数值/公式**（规则 9/14）；未引入命令总线/UI 等超范围系统。
- [ ] 改动在 `feature/t3-application-contracts`，PR 待审；`CHANGELOG.md` 已更新（规则 7,10）。

## 7. 歧义处理
遇本单未指定细节、或需定数值/玩法/新系统 → 停下标 `[需 Claude 决策]`/`[需人类定值]`，写进 PR 描述，继续其它独立步骤。

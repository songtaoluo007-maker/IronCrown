# 工作单 T6 — 表现层 MVP（UI Toolkit HUD）

| 项 | 值 |
|---|---|
| 工作单号 | T6（MVP 倒数第二；首个可见垂直切片） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查 | Claude（规则 13） |
| 分支 | `feature/t6-presentation-ui` |
| 前置 | T5 已完成（回合有可见经济效果） |
| 角色边界 | 规则 12：只实现功能骨架与数据绑定。**视觉/布局/美术/文案是人类取舍（规则 14 体验验收）**——本单只做"能看、能点、会刷新"的最小功能 HUD，**不做美化**。**严禁照搬任何现有游戏的 UI/布局/图标/文案（规则 2）**，用中性原创样式。 |

## 0. 目标
一个最小可玩 HUD：显示当前回合/阶段 + 各国关键状态（国库/稳定度/资源），一个"推进"按钮调用 `GameSessionService.AdvancePhase()`，并在推进后/收到事件时刷新。**全程仅经 `GameSessionService` 调用 + 读 `Contracts` 只读模型**——`Presentation` 在编译期就引用不到 `Domain`/`Simulation`（规则 4 硬约束）。

## Phase 0
- 新建分支 `feature/t6-presentation-ui`；全套测试先绿。
- 不直接编辑 `CHANGELOG.md`（changelog 写 PR 描述，Claude 合入）。

## 1. 架构决策（写死）
1. **UI Toolkit**：`UIDocument` + `PanelSettings` + UXML + USS（运行时 UI）。
2. **`Presentation` asmdef**：`references: ["IronCrown.Application","IronCrown.Contracts"]`，`noEngineReferences:false`。**不得引用 Domain/Simulation/Infrastructure**（编译期强制规则 4）。
3. **UI 控制器经 DI 注入**：通过 VContainer 注入 `GameSessionService` 与 `IEventPublisher`（`IEventPublisher` 在 Contracts，可引用）。UI 只读 `WorldView`/`CountryView`、只调 `GameSessionService` 方法。
4. **数据流**：点击"推进" → `GameSessionService.AdvancePhase()` → 重新 `GetWorldView()` 渲染；同时订阅 `TurnStartEvent`/`TurnEndEvent` 触发刷新。**UI 不持有也不修改任何运行时状态**。
5. **玩家国家**：MVP 不做"选国"，HUD 展示**全部国家**列表（`WorldView.countries` 已按 id 升序）。"玩家视角/选国"是后续 + 人类取舍。

## 2. 实现规格

### 2.1 场景与装配
- 新建场景 `Assets/Scenes/Main.unity`：含一个 `Bootstrap` GameObject 挂 `GameLifetimeScope`（T1 已建）+ 一个 `UIDocument`（引用下方 UXML + 一个 `PanelSettings` 资产 `Assets/UI/Settings/MainPanelSettings.asset`）。
- `GameLifetimeScope` 注册 UI 控制器（见 2.3），并确保 `GameEntryPoint.Start()`（已是 `IStartable`）先建好世界，UI 再渲染（顺序：EntryPoint 建世界 → UI 首帧 `GetWorldView()`；若时序不稳，UI 在自身首次 enable 时主动拉取一次）。

### 2.2 UXML/USS（`Assets/UI/`）
- `MainHud.uxml`：
  - 顶栏：`Label#turn-label`（"回合 N · 阶段 X"）+ `Button#advance-btn`（文案"推进"）。
  - 主体：`ScrollView#country-list`，运行时为每个 `CountryView` 动态生成一行：国名 + 国库 + 稳定度 + 资源摘要（`steel/oil/...` 拼接）。
- `MainHud.uss`：中性原创排版（间距/字号/分隔线即可，**不模仿任何既有游戏**）。

### 2.3 UI 控制器 `Presentation/Views/MainHudController.cs`
- 普通类（非 MonoBehaviour），构造注入 `GameSessionService`、`IEventPublisher`、（可选）`IAppLogger`。
- 提供 `void Bind(VisualElement root)`：缓存控件、绑定按钮回调、订阅事件、首次 `Render()`。
- `Render()`：`var vm = _session.GetWorldView(); if(vm==null) return;` 更新顶栏文案、清空并重建 `country-list` 行。
- 按钮回调：`_session.AdvancePhase(); Render();`
- 事件：`_events.Subscribe<TurnStartEvent>(_ => Render());`（及 TurnEnd）。
- 由一个轻量 MonoBehaviour（`Presentation/Views/MainHudBehaviour.cs`，持 `UIDocument`）在 `OnEnable` 时把 `rootVisualElement` 交给注入的 `MainHudController.Bind(...)`。MonoBehaviour 经 VContainer 的 `RegisterComponentInHierarchy<MainHudBehaviour>()` 或注入获取控制器。

> 绑定的具体接法（`RegisterComponentInHierarchy` vs `EntryPoint` 持有）若有多写法且本单未定，选其一并在 PR 说明；不引入额外框架。

## 3. 测试（规则 6）
- **可单测的展示逻辑**：把"WorldView → 行文案"的纯逻辑抽成 `MainHudController` 的纯方法（如 `static string FormatCountryRow(CountryView c)`、`static string FormatHeader(WorldView w)`），在 `Presentation` 无法纯测时退而在 **Application.Tests** 或一个 `Presentation.Tests`(EditMode) 覆盖这些纯方法。
- **PlayMode 冒烟**（最小）：加载 `Main` 场景 → 等一帧 → 断言 `country-list` 子元素数 == 6、`turn-label` 非空；模拟点击 `advance-btn` → 断言 `turn-label` 文案变化。（PlayMode 测试程序集放 `Assets/Tests/PlayMode/`。）

## 4. 文件清单
| 动作 | 路径 |
|---|---|
| 新增 | `Assets/Scenes/Main.unity`、`Assets/UI/MainHud.uxml`、`Assets/UI/MainHud.uss`、`Assets/UI/Settings/MainPanelSettings.asset` |
| 新增 | `Presentation/Views/MainHudController.cs`、`Presentation/Views/MainHudBehaviour.cs` |
| 改 | `Assets/Scripts/Presentation/IronCrown.Presentation.asmdef`（references = `["IronCrown.Application","IronCrown.Contracts"]`）、`Bootstrap/GameLifetimeScope.cs`（注册 UI 控制器/组件） |
| 新增 | `Assets/Tests/PlayMode/IronCrown.PlayMode.Tests.asmdef` + HUD 冒烟测试；（可选）Presentation 纯逻辑 EditMode 测试 |

## 5. 验收门禁（DoD）
- [ ] `Presentation` 仅引用 `Application`+`Contracts`，**不引用 Domain/Simulation/Infrastructure 仍能编译**（规则 4 硬验证）。
- [ ] 运行 `Main` 场景：HUD 显示 6 国及其国库/稳定度/资源；点"推进"回合/阶段前进且数字刷新（T5 的产出可见变化）。
- [ ] UI 不持有/不修改运行时状态，全程经 `GameSessionService` + `WorldView`。
- [ ] PlayMode 冒烟绿；编译 0 error；EditMode 仍全绿。
- [ ] UI 为原创、无照搬（规则 2）；视觉粗糙可接受（待人类验收/迭代，规则 14）。
- [ ] PR 在 `feature/t6-presentation-ui`；changelog 写 PR 描述（勿碰 CHANGELOG.md）。

## 6. 歧义处理
涉及视觉/布局/选国/玩家视角等体验取舍 → 停下标 `[需人类定值]`，做最小中性实现并在 PR 列出供人类验收。其它未覆盖技术细节 → `[需 Claude 决策]`。

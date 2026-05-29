# 工作单 T7-FIX — 修复 MVP 演示（UI 接线 + 真实验证）

| 项 | 值 |
|---|---|
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查 | Claude（规则 13） |
| 分支 | `feature/t7-fixes` |
| 前置 | T5/T6/T7 已提交 |
| 角色边界 | 规则 12：只实现本单所列；**勿改经济数值/公式**；CHANGELOG 写 PR 描述不直接编辑 |

## 背景（Claude 审查 T5/T6/T7）
- ✅ T5 经济：实现正确、数值未改、规则 4/9/14 守住。
- 🔴 **演示打不开有效画面**：`MainHudController` 注册进 DI，但**从未注入 `MainHudBehaviour`**（`SetController` 无 `[Inject]`、`GameLifetimeScope` 无 `RegisterComponentInHierarchy`）→ `_controller==null` → `Bind()` 不执行 → **HUD 空白**。
- 🔴 **全程未真实验证**：`artifacts/editmode-results.xml` 是 **T4 旧结果（49 个）**，T5/T6/T7 新测试从未重跑；**无 PlayMode 结果**。
- 🟡 `MvpSmokeTests` 纵容 `childCount==0`/缺 UIDocument 直接 skip → 空白 HUD 也假性通过。

## 状态更新（Claude，2026-05-28）
- **F1 接线 = 已完成（勿改）**：OpenClaw 已用 `GameEntryPoint.Start()` 里 `FindObjectOfType<MainHudBehaviour>().SetController(_hudController)` 接线（非本单原建议的 `[Inject]`，但等效）。**保留此方式，勿改回 `[Inject]`/`RegisterComponentInHierarchy`**（避免再次破坏）。
- **F2 时序 = Claude 已手修（勿回退）**：`MainHudBehaviour.SetController` 现在控制器到位时即触发 `Bind`（修复 `OnEnable` 早于注入而漏渲染）。`NewGame` 末尾发刷新事件这条可不做。
- ⚠ **OpenClaw 本单剩余范围 = 仅 F3 + F4**。F1/F2 已闭合，不要再动接线代码。

## 必办项（剩余）

### F1 — [已完成·勿改] 接线 UI 控制器
- `Presentation/MainHudBehaviour.cs`：改用 VContainer **方法注入**：
  ```csharp
  [Inject] public void Construct(MainHudController controller) { _controller = controller; }
  ```
  （`using VContainer;`；保留 `_controller` 字段；`SetController` 可删或保留供测试）
- `Bootstrap/GameLifetimeScope.cs` 的 `Configure` 末尾加：
  ```csharp
  builder.RegisterComponentInHierarchy<MainHudBehaviour>();
  ```
  保留 `builder.Register<MainHudController>(Lifetime.Singleton);`。

### F2 — [核心] 首屏渲染时序（否则首屏空白到第一次推进）
- 确保世界建好后 HUD 立即渲染一次。推荐：在 `GameSessionService.NewGame()` 末尾发一次刷新事件：
  ```csharp
  _events.Publish(new TurnStartEvent { TurnNumber = _clock.CurrentTurn });
  ```
  （`GameSessionService` 需注入 `IEventPublisher`；HUD 已订阅 `TurnStartEvent`→`Render()`）
- 绑定时机：在 `Construct` 注入后或 `Start()` 里执行 `Bind`，不要只依赖 `OnEnable`（注入可能晚于 OnEnable）。**务必实际 Play 验证首屏即显示 6 国**。

### F3 — [测试] 收紧冒烟（否则空白 HUD 仍假性通过）
- `MvpSmokeTests`：删除 `if (uiDoc == null) yield break;` 与 `if (childCount > 0)` 的纵容逻辑——UIDocument/`turn-label`/`advance-btn`/`country-list` 缺失或 `country-list.childCount != 6` 一律 `Assert` **失败**。
- 增一条：记录 `turn-label.text` → 模拟点击 `advance-btn`（`advanceBtn.SendEvent` 或直接触发回调）→ 等一帧 → 断言 `turn-label.text` 变化。

### F4 — [验证·本轮最重要] 真跑、附证据
- **重跑全部 EditMode**（含 T5/T6 新增 `EncodingGuardTests`/`SaveLoadEquivalenceTests`/`MainHudControllerTests`/经济用例），导出**新的** `artifacts/editmode-results.xml`（计数应 **> 49**、时间戳为本次）。
- **跑 PlayMode**（`MvpSmokeTests`），导出 `artifacts/playmode-results.xml`。
- **手动 Play `Main` 场景**：菜单 `IronCrown > Setup Main Scene` → 打开 `Main` → Play → 截 1 张图：HUD 显示 6 国 + 资源/国库；点"推进"后数字变化。三项证据全部附 PR。

## 第二轮缺陷（首次真跑 74 用例暴露：68 过 / 6 败）

### F5 — [真 bug·必办] 存档字段不完整 → 续跑等价测试失败（1 个 Application 失败）
诊断：`SaveLoadEquivalenceTests` 失败，因 `SaveMapper`/`CountrySaveData`/`ProvinceSaveData` 只存子集，**丢失 `resources`/`equipmentStockpile`/工厂数/人力 及省份 `infrastructure`/`resourceOutput`** → 读档后世界与直跑不一致。这是真 bug（测试正确抓出），也是长期延后的"存档字段完整化"。
修复（MVP 用**完整快照**，因测试用 `ToRuntime` 直接重建、不走配置）：
- `CountrySaveData` 增字段：`Dictionary<string,int> resources`、`int equipmentStockpile`、`manpower`、`totalManpower`、`civilianFactories`、`militaryFactories`、`dockyards`、`legitimacy`、`corruption`、`bureaucracy`、`taxIncome`、`tradeIncome`、`militaryExpense`、`civilExpense`、`inflation`。
- `ProvinceSaveData` 增字段：`string terrain`、`infrastructure`、`railwayLevel`、`portLevel`、`airBaseLevel`、`industrySlots`、`string[] resourceOutput`、`population`、`manpower`、`isCapital`、`victoryPoint`。
- `GameState` 增 `int worldTension`。
- `SaveMapper.ToSave`/`ToRuntime` 映射全部新字段（`terrain` 用 `Enum.Parse<TerrainType>`/`ToString`）；`ToRuntime` 设 `world.worldTension = save.worldTension`（不再恒 0）。
- 验收：`SaveLoadEquivalenceTests` 转绿；存读档为**无损完整快照**。

### F6 — [配置·Claude 已手修] PlayMode 测试 asmdef（5 个失败 + PlayMode 标签页空）
诊断：`IronCrown.PlayMode.Tests.asmdef` `includePlatforms:["Editor"]`（错）且缺 TestRunner 引用 → 混入 EditMode 跑且 5 连败、PlayMode 标签页 "No tests"。
**Claude 已手修**：`includePlatforms: []` + `references` 增 `UnityEngine.TestRunner`、`UnityEditor.TestRunner`。OpenClaw 只需：在 **PlayMode 标签页**重跑 `MvpSmokeTests`（先 `IronCrown > Setup Main Scene`，场景已在 Build Settings），确认 5 个测试出现在 PlayMode 标签页并通过；EditMode 不再混入这 5 个。

## 验收门禁（DoD）
- [ ] Play `Main` 场景：**首屏即见 6 国 HUD（非空白）**；点"推进"回合/阶段前进且资源数字变化（**截图为证**）。
- [ ] EditMode 全绿且为**本次新结果**（计数 > 49、时间戳新）；PlayMode（收紧后断言）全绿。
- [ ] `MainHudController` 经 `[Inject]` + `RegisterComponentInHierarchy` 真正注入 `MainHudBehaviour`。
- [ ] 未改经济数值/公式（规则 9/14）；分支 `feature/t7-fixes`；changelog 写 PR 描述（勿碰 CHANGELOG.md）。

## 歧义处理
VContainer 注入时序/组件查找若有多写法 → 选能让"首屏 Play 即显示 6 国"的那种，并在 PR 说明；以**实际 Play 截图**为最终判据，不要只靠测试绿。

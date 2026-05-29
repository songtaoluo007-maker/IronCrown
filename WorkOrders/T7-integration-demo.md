# 工作单 T7 — 集成冒烟 + MVP 实机演示

| 项 | 值 |
|---|---|
| 工作单号 | T7（MVP 收官；端到端最小闭环 + 可演示） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查 | Claude（规则 13）；**最终体验由人类验收（规则 14）** |
| 分支 | `feature/t7-integration-demo` |
| 前置 | T5、T6 已完成 |
| 角色边界 | 规则 12：只实现集成测试 + 演示脚手架，不加新玩法/数值。 |

## 0. 目标
把 T0–T6 串成一个**可运行、可断言**的最小闭环，验证 MVP 目标："配置加载 → 世界初始化 → 推进回合（经济产出可见）→ 存档 → 读档一致 → UI 展示"。并给人类一个**一键看实机**的入口。

## Phase 0
- 新建分支 `feature/t7-integration-demo`；全套测试先绿。
- 不直接编辑 `CHANGELOG.md`（写 PR 描述）。

## 1. 集成冒烟测试（PlayMode）`Assets/Tests/PlayMode/`
新增 `MvpSmokeTests`（用 `[UnityTest]` 协程，加载 `Main` 场景，等容器与 EntryPoint 就绪）：

1. **启动**：加载 `Assets/Scenes/Main.unity` → 等 1–2 帧 → 取到 `GameSessionService`（经场景内 LifetimeScope 解析；测试可用 `LifetimeScope.Find` 或暴露的入口）。断言 `GetWorldView().countries.Count == 6`。
2. **推进有效果**：记录某国（如 `empire_north`）的 `steel` 资源 → 连续 `AdvancePhase()` 直到完整推进 1 个回合（5 阶段）→ 断言该国 `steel` **已增加**（T5 省份产出生效）、`turn` 已 +1。
3. **存读档闭环**：`Save("smoke")` → 改动现状（再推进几阶段）→ `Load("smoke")` → 断言 `GetWorldView()` 与保存点一致（回合/阶段/某国国库或资源一致）。
4. **HUD 反映**：断言 `turn-label` 文案随推进变化、`country-list` 行数 == 6（接 T6）。

> 若 PlayMode 下 DI 时序导致取 `GameSessionService` 不稳，允许在 `GameEntryPoint` 或一个测试钩子上暴露**只读**访问入口；不得为测试在生产代码塞逻辑分支。

## 2. 确定性回归（EditMode，巩固）
- 复用/确认 T5 的"存档续跑等价"测试在 CI 中常驻（跑2→存→读→再跑2 == 直跑4 的世界哈希）。

## 3. 实机演示入口（给人类）
- 在 `Main` 场景确保：打开 → Play → 见 HUD（6 国 + 资源/国库/稳定度）→ 点"推进"数次，数字逐回合变化。
- 在 PR 描述附**演示说明**：要打开的场景路径、操作步骤、预期现象（"推进 1 回合后 empire_north 钢铁 +X"）；附 1–2 张 Play 模式截图或日志片段为证。
- 在仓库根新增 `RUNME.md`（UTF-8）：3 行说明如何在 Unity 6 打开本工程、打开 `Main` 场景、Play 看演示。

## 4. 文件清单
| 动作 | 路径 |
|---|---|
| 新增 | `Assets/Tests/PlayMode/MvpSmokeTests.cs`（+所需 asmdef，若 T6 已建 PlayMode asmdef 则复用） |
| 新增 | `RUNME.md`（演示运行说明，UTF-8） |
| 改（如需） | 暴露只读测试入口的最小改动（不污染生产逻辑） |

## 5. 验收门禁（DoD）
- [ ] PlayMode 冒烟绿：启动→6 国→推进 1 回合资源增加→存档→读档一致→HUD 刷新。
- [ ] EditMode 全套（含确定性续跑等价）绿；编译 0 error；附 `artifacts/` 结果。
- [ ] `RUNME.md` 存在，按其步骤可在 Unity 6 打开 `Main` 场景 Play 看到 MVP 演示（人类验收，规则 14）。
- [ ] 未新增玩法/数值；未照搬他游（规则 2/9/14）。
- [ ] PR 在 `feature/t7-integration-demo`；changelog 写 PR 描述。

## 6. MVP 完成定义
T7 通过 = **第一个可玩垂直切片达成**：配置驱动、回合推进有可见经济效果、存读档确定性一致、UI 可视可操作。此后进入 MVP 之后阶段（军事/外交/科技/AI 深度、美术、音频、关卡内容——按系统另拆工作单）。

## 7. 歧义处理
PlayMode/DI 时序、测试入口暴露方式若有多解且本单未定 → 选最小侵入实现并在 PR 说明；涉及体验取舍 → `[需人类定值]`。

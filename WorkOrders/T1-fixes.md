# 工作单 T1-FIX — T1 审查整改

| 项 | 值 |
|---|---|
| 工作单号 | T1-FIX |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查 | Claude（规则 13） |
| 分支 | `feature/t1-fixes` |
| 前置 | 已完成 T1（Foundation Migration） |
| 角色边界 | 同 T1：只实现，不做架构/数值决策；遇未覆盖点停下标 `[需 Claude 决策]` |

## 背景

Claude 审查 T1：结构与红线（分层纯净、无 Core、无单例、无 JsonUtility、确定性遍历、公式未改）**全部通过**，但发现以下整改项。

## 必办项

### F0 — [流程·高] 初始化 git（审查 D6）
当前仓库**不是 git 仓库**，规则 10（不得直推 main）与"feature 分支 + PR"无法执行。
- 仓库根 `git init`。
- 添加 Unity 标准 `.gitignore`（至少忽略 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`、`*.csproj`、`*.sln`、`Build/`）。
- 提交当前全部为基线：`chore: baseline (governance + T0 asmdef + T1 migration)`。
- 之后切 `feature/t1-fixes` 做下方修复。**今后所有任务一律分支 + PR**。

### F1 — [功能·高] GameEntryPoint 实现 IStartable（审查 D1）
`Bootstrap/GameEntryPoint.cs` 当前是普通类，只有 `public void Start()`，但 `GameLifetimeScope` 用 `RegisterEntryPoint<GameEntryPoint>()` 注册——VContainer **不会调用**非 `IStartable` 的 `Start()`，导致**游戏永不启动**。
- 改为 `public sealed class GameEntryPoint : VContainer.Unity.IStartable`，加 `using VContainer.Unity;`，确认 `Start()` 匹配接口。
- 验收：启动后 `Start()` 确被调用（运行启动场景见 `[EntryPoint] 游戏启动` 日志，或加一条 PlayMode 冒烟测试断言）。

### F2 — [验证·中] 确认测试可被 Test Runner 发现并实跑（审查 D3 + D5）
3 个测试 asmdef 当前 `noEngineReferences:true` 且未引用 TestRunner，可能不被 Unity Test Runner 识别。
- 在 Unity Test Runner 窗口确认 3 个测试程序集与 7 个测试类**被列出且可运行**。
- 若未被列出：在各测试 asmdef 的 `references` 加 `"UnityEngine.TestRunner"`、`"UnityEditor.TestRunner"`，并将该测试 asmdef 的 `noEngineReferences` 改为 `false`（保留 `nunit.framework.dll` 与 `UNITY_INCLUDE_TESTS`）。
- **跑通 EditMode 全套**，导出结果到 `artifacts/editmode-results.xml`，PR 附上。
- 另跑一次 batchmode 编译，确认 **0 error**，附日志。

### F3 — [清理·低] 删多余 using（审查 D4）
`Bootstrap/GameEntryPoint.cs` 的 `using UnityEngine;` 若清理后确无 Unity 用法则删除。

## 暂不处理（已知、按设计延后）
- **D2 存档种子/阶段未持久化**：并入后续"存读档闭环"任务（届时给 `GameState` 加 `seed`/`phase` 字段、`GameEntryPoint` 注入 `IRandom` 取真实种子、`SaveMapper` 写入读出）。当前 `SaveMapper.ToSave` 的 `seed`/`phase` 形参先保留。

## 验收门禁
- [ ] git 仓库 + `.gitignore` 就绪；改动在 `feature/t1-fixes`，未直推 main（规则 10）。
- [ ] batchmode 编译 0 error（附日志）。
- [ ] EditMode 全绿（附 `artifacts/editmode-results.xml`）。
- [ ] `GameEntryPoint : IStartable`，`Start()` 确被调用。
- [ ] `CHANGELOG.md` 追加 `[T1-FIX]` 条目（规则 7）。

# P2-review-fixes-2 执行报告（OpenClaw）

## 执行状态

| 项 | 状态 | 说明 |
|---|---|---|
| R1 | ✅ 完成 | SpatialIndexTests.Index_AfterMoveCommand_MatchesTraversal — 真经 session.IssueCommand(MoveUnit) |
| R2 | ✅ 完成 | ReplayTests 3 个测试全部通过，黄金 hash 已锁定 = -2128831035 |
| R3 | ✅ 完成 | ReplayPlayer.AdvanceUntilNextTurn — 按 turnNumber 递增切回合 |
| R4 | ✅ 守住 | 未编辑 CHANGELOG / PROJECT_STATE / Design/ |
| R5 | ✅ 产出 | 见下方 artifacts |

## R1: 真实命令路径测试

文件：`IronCrown.Simulation.Tests/SpatialIndexTests.cs`（从 IronCrown.Tests 移入，有 asmdef）

测试方法：
1. `TestSessionFactory.Create()` 创建真实 session
2. 手动注入 `CountryState` + 2 个 `ProvinceState`（互为邻居）+ 1 个 `UnitState`
3. 注册 `EconomyConfig("global")`（`IssueCommand` 统一校验）
4. `session.SetPlayerCountry()` + `world.RebuildProvinceUnitIndex()`
5. `session.IssueCommand(MoveUnit)` 驱动移动
6. 验证：源省移除、目标省添加、全局索引一致性

编译修复：
- `UnitState` 无 `isActive` 字段 → 改用 `organization > 0`
- `neighbors` 类型为 `string[]` → `new[] { ... }` 语法
- `IConfigRegistry` 无 `Register` → 反射调用 `TestConfigRegistry.Register`

## R2: 真实 hash 比对

文件：`IronCrown.Application.Tests/ReplayTests.cs`

三个测试：
1. `RecordReplay_SameWorld` — 录制→回放→HashWorld 一致 ✅
2. `GoldenReplay_MatchesBaseline` — 黄金脚本→hash = -2128831035 ✅（已锁定）
3. `Replay_SameSeed_TwiceIdentical` — 同种子→同 hash ✅

基础设施（跨程序集）：
- `TestSaveRepo` — 内存 ISaveRepository
- `ReplayTestFactory` — 创建带 TestSaveRepo 的 session
- `NoopLogger` / `TestConfigRegistry`

## R3: 回放边界修正

`ReplayPlayer.AdvanceUntilNextTurn()`：
- 读 `viewBefore.turn` → 循环 `AdvancePhase()` → 直到 `viewAfter.turn > turnBefore`
- 最大 100 次迭代防死循环
- 取代硬编码 6 阶段循环

## R5: Artifacts

- 测试 XML：`artifacts/verify.xml`
- 编译输出：零 product 代码错误（SetupScene.cs 是 P2.3 预存问题，非本轮改动）
- 黄金 hash：`-2128831035`（已锁入 ReplayTests.cs）

## 提交记录

```
33c9258 fix(R2): lock golden replay hash baseline = -2128831035
9434818 fix(P2-review-2): R1 real MoveUnit test + R2 replay hash + R3 turn boundary + encoding fixes
```

## 未触及

- CHANGELOG.md — 未编辑 ✅
- PROJECT_STATE.md — 未编辑 ✅
- Design/ — 未编辑 ✅
- F7（Economy float 确定性）— 未动 ✅

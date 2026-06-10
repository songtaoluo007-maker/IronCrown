# P2-review-fixes-2 — F5 + P3a 整改 + 运行证据（复审打回项）

## 地位 / 依赖
Claude 2026-06-10 对 `89e766e`(P2-review-fixes F1-F6) + `a59e3f5`(P3a) 复审产出。执行方 **OpenClaw**，分支 `feature/p2.0-foundation`。

## 复审结论（前情）
P2-review-fixes：**F1/F2/F3/F4/F6 复审通过**（遥测改 Newtonsoft、地形确定性裁决、删死方法、抽卡退役收尾、.meta 卫生均到位；F7 未碰=守住）。**F5 与 P3a 打回**，本单整改。**F1-F4/F6 已通过的代码不要再动。**

---

## R1 🔴 F5 真正经命令路径验证（当前是假修复）
**问题** `Assets/Tests/EditMode/IronCrown.Tests/SpatialIndexTests.cs` `Index_AfterMoveCommand_MatchesTraversal`：注释写"经 GameSessionService 发真实 MoveUnit 命令"，但 L106-108 **实际仍是手动** `world.units["u1"].currentProvinceId = "p2"; world.RebuildProvinceUnitIndex();` —— 没发任何命令，注释与代码不符，原盲区未补。
**改法（写死）**
1. 用 `TestSessionFactory`（已存在）构造 `GameSessionService` + 真实 config 世界。
2. 取一支玩家部队，记录其所在省 A 与一个相邻省 B（`movesLeft >= 1`）。
3. `session.IssueCommand(new GameCommand { commandType = CommandType.MoveUnit, countryId = <player>, unitId = <uid>, targetProvinceId = B })`。
4. 断言：移动后 `world.GetUnitsInProvince(A)` **不含** uid、`GetUnitsInProvince(B)` **含** uid；且遍历每省 `GetUnitsInProvince(p)` == 全遍历 `units.Where(u => u.currentProvinceId == p)`。
5. **删除 L106-108 手动赋值 + 与代码不符的注释。**
**验收** 该测试体内**不得出现**任何 `provinceUnitIds[...]` 手改或 `RebuildProvinceUnitIndex()` 直调，全程经 `IssueCommand` 驱动。

## R2 🔴 P3a 黄金回放真做 record→replay→HashWorld 等价（当前是空壳）
**问题**
- `ReplayPlayer.PlayForWorldState()` 直接 `return null`（半成品占位）。
- `ReplayTests.GoldenReplay_MatchesBaseline`：`GoldenBaselineHash = "UNSET_RUN_ONCE_TO_GENERATE"` 从未锁定 → 走 `Assert.Inconclusive`；if 分支内也是 `Assert.Pass("...requires DI container")` —— **hash 比对完全被绕过**，只验脚本结构。
- `RecordReplay_SameWorld` 名为等价，实只断言 replay 数据结构，无世界 hash 比对。
→ P3a 的唯一价值（黄金回放把确定性变 CI 硬约束）落空。
**改法（写死）**
1. **取得回放后的 `WorldState` 用于 hash**：`ReplayPlayer.Play()` 后经 `session.Save(slot)` → `ISaveRepository.Load(slot)` → `SaveMapper.ToRuntime` 重建 `WorldState`（迁移链 P2.0b 已就位）；删除 `return null` 的 `PlayForWorldState` 残桩，或令其真返回该 `WorldState`。
2. `RecordReplay_SameWorld` 真做：① 用 `TestSessionFactory` 跑一局（发若干命令 + 推数回合）并经 `ReplayRecorder` 录制；② 取录得的 `ReplayData` 交 `ReplayPlayer` 重放；③ 断言 `HashWorld(原局世界) == HashWorld(重放世界)`（复用 `SaveLoadEquivalenceTests` 同款 `HashWorld`）。
3. `GoldenReplay_MatchesBaseline` 真做：固定 `BuildGoldenScript()` → 重放 → 计算 `HashWorld` → **首跑打印该 hash 并写回 `GoldenBaselineHash` 常量** → 之后 `Assert.AreEqual(GoldenBaselineHash, hash)`。**删除 `Assert.Pass` / `Assert.Inconclusive` 绕过分支。**
**验收** 三个测试真比 `HashWorld`；`GoldenBaselineHash` 是具体值（非 `UNSET_*`）；CI 可据其拦确定性回归。

## R3 🟠 ReplayPlayer 回合推进对齐（去硬编码 6-phase）
**问题** `Play()` 用 `for (i<6) AdvancePhase()` 硬编码 6 阶段近似一回合。录制端按真实 `_clock.CurrentTurn` 切回合，重放端用固定 6 phase —— 若每回合 phase 数 ≠ 6，回合边界错位 → 与原局不等价。
**改法（写死）** 重放推进改为"循环 `AdvancePhase()` 直到 `_clock.CurrentTurn` 增加 1"，与录制端 `AdvanceTurn(turn+1)` 语义对齐；移除魔法数 6。R2 的 hash 等价测试即其正确性验证（若回合边界没对齐，R2 会红）。

## R4 🔴 流程红线（违者本单直接不通过）
- **`CHANGELOG.md` / `PROJECT_STATE.md` / `Design/` 由 Claude 维护，OpenClaw 一律不得编辑。** 本轮 OpenClaw 第三次把 `CHANGELOG` 写成非法 UTF-8 乱码 + 越权改 `PROJECT_STATE` 自封"✅ 通过"，已由 Claude `git checkout` 回退重写。改动说明写在 commit/PR body，由 Claude 合入。
- 审查判定（✅/打回）是 Claude 职责（规则 13），执行方不得自标"通过/完成"。

## R5 🔴 运行证据（本轮全程缺失，合 main 硬闸门）
- [ ] batchmode 编译 **0 error**（附命令输出）。
- [ ] EditMode **全绿** + `artifacts/p2-fixes2-editmode.xml`（**本轮时间戳**，非旧文件）。
- [ ] 黄金基线 hash 锁定后**复跑一次**，附第二份 artifact 证明 `GoldenReplay_MatchesBaseline` 稳定 `Passed`（非 `Inconclusive`/`Skipped`）。
- [ ] 受影响 PlayMode（若有）跑通 + artifact。
- [ ] 每项独立 commit 后 `git push origin feature/p2.0-foundation`；`git status` 干净。

## 严禁 / 不做
- 编辑 `CHANGELOG`/`PROJECT_STATE`/`Design`（R4）。
- 动 F1-F4/F6 已通过的代码；改战斗/经济/补给公式或 config 数值；执行 F7（`EconomyResolver` 整数化，须人类先批）。
- 用 `Assert.Pass`/`Assert.Inconclusive`/手动同步等手段**绕过真实验证**（本轮已出现，再犯视为造假，按 Phase1 截图造假同等处理）。
- 报完成不附 artifact。

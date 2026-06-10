# P2-fixes-3 — EditMode 16 个失败修复（首份 artifact 暴露）

## 地位 / 依赖
re-fixes-2 复审：**R1–R4 通过**（F5 真经命令验证、P3a 真做 `HashWorld` 等价、去 6-phase、未改治理文档）。R5 首次产出真 artifact（`verify.xml`）—— **暴露 EditMode 388 中 16 failed / 0 inconclusive**（done 报告未提，再犯按不诚实处理）。这 16 个**全是 P2 既有实现问题，非 re-fixes-2 引入**。执行方 OpenClaw，分支 `feature/p2.0-foundation`。

## 根因分组（来自 `artifacts/verify.xml`）

### G1 🔴 组 D：BattleResolver `_config` NRE（5 个 C3 测试）
**失败** `BattleResolverC3Tests.TickBattles_*`（5）`NullReferenceException`。
**根因** `BattleResolver.cs:167` `var ecoDef = _config.Get<EconomyConfig>("global");` —— **缺 `?.`**；C3 测试 `_config` 为 null → NRE。同方法 `159` 行已是 `_config?.Get<EconomyConfig>("global")`（安全）。P2.4 加地形修正时漏 null 防护 + 重复取 config。
**改法（写死）** 删 `167` 行重复获取，复用 `159` 行已有的 `eco`：`168-169` 改为
`TerrainType combatTerrain = TerrainAggregator.GetProvinceCombatTerrain(province, world, eco);`
`int terrainMult = GetTerrainDefenseMultiplierInt(combatTerrain, eco);`
（删除 `ecoDef` 变量。两被调方法内部已对 `eco==null` 有保护。）**不改任何地形数值。**
**验收** 5 个 C3 测试转绿。

### G2 🔴 组 A：provinces.json 结构（7 个 ConfigValidation 测试）
**失败** `ConfigValidationTests.Provinces_*`（6）+ `Countries_CapitalProvinceExists`（1）`System.ArgumentException: JSON must represent an object type`。
**根因** `provinces.json` 顶层是裸 array `[...]`，但 `ConfigValidationTests` 用 `JsonUtility.FromJson<ProvinceList>`（`ProvinceList{ List items }`，要 `{items:[...]}` object wrapper，与 `countries.json` 同构）。P2.x 地图重构把 provinces.json 写成了裸 array。
**改法（写死）**
1. `provinces.json` 恢复 `{ "schemaVersion": 2, "items": [ ...各省(含 tiles 字段不变)... ] }` wrapper（对齐 `countries.json` + T2 配置规范）。**不改省份/格子的数据值**，仅加外层包裹。
2. **同步确认运行时**：`ConfigRegistry` / `WorldInitializer` 读 provinces 的解析路径与该 wrapper 一致（若 P2.x 为读 array 改过运行时解析，一并对齐回 wrapper）。运行时与测试**用同一结构**。
**验收** 7 个 ConfigValidation 测试转绿；运行时正常加载 provinces（PlayMode/Play 不炸）。

### G3 🟠 组 B：SaveMigration 测试 schema 期望漂移（3 个）
**失败** `SaveMigrationTests.{AlreadyV1_NoSideEffects, Migrated_ThenToRuntime_OK, OldSaveNoVersion_DefaultsToV1_LoadsOK}` `Expected: 1 But was: 2`。
**根因** P2.2 把 `SaveSchema.CURRENT` bump 到 2（加 `Migration_1to2`），但这些 P2.0b 时写的测试仍期望迁移到 v1。
**改法（写死，按各测试真实意图）**
- "v1 档不被多余改动"类：用 `new SaveMigrationRunner(migrations, targetVersion: 1)` 注入目标版本验证纯逻辑。
- "无版本旧档加载"类：默认 0 → 跑到 `CURRENT`，期望改为 `SaveSchema.CURRENT`（=2）。
- 统一原则：**期望引用 `SaveSchema.CURRENT` 而非硬编码数字**，避免再漂移。
**严禁** 用删测试/改断言掩盖真实迁移行为。
**验收** 3 个迁移测试转绿。

### G4 🟠 组 C：UnlockCommander general_blitz 返回 null（1 个）
**失败** `SaveLoadEquivalenceTests.SaveLoad_CommanderAndGacha_SurvivesRoundTrip`（:780）`解锁 general_blitz 应成功 Expected: not null`。
**根因（调查点）** `UnlockCommander(..., "general_blitz")` 返回 null。确认：① `general_blitz` 卡 id 是否存在于 `generalCards.json`（被改名/删？）;② 是否与 G2 同源（commander/卡 config 加载失败）;③ `UnlockCommander` 返回 null 的具体分支（战功点不足？卡查不到？）。
**改法** 据根因修：卡 id 不存在→用存在的 id 或补卡；config 加载问题→随 G2 修；逻辑问题→修 `CommanderUnlockResolver`。
**验收** 该测试转绿。

### G5 🟡 顺手清理
- 删 `ReplayPlayer.PlayForWorldState()` 的 `return null` 死桩（R2 测试已自行 Save/Load，未用它）；无引用则删整方法。
- 清工作区垃圾：`Assets/Editor/SetupScene.cs.bak.meta`、`SetupScene.cs.meta.bak.meta`（及对应 `.bak` 备份文件）——备份不入库，删除并入 `.gitignore`。

## R4 重申 🔴
`CHANGELOG` / `PROJECT_STATE` / `Design/` 由 Claude 维护，OpenClaw 不得编辑（本轮已守住）。审查判定（✅/打回）是 Claude 职责（规则 13），执行方不得自标通过。

## 验收门禁（硬闸门）
- [ ] batchmode 编译 0 error。
- [ ] **EditMode 388 全绿（0 failed / 0 inconclusive / 0 skipped）** + `artifacts/p2-fixes3-editmode.xml`（本轮时间戳）。
- [ ] 受影响 PlayMode（provinces 加载 / 战斗占领）跑通 + artifact。
- [ ] `GoldenReplay_MatchesBaseline` 保持 `Passed`（确定性未被 G1-G4 破坏）。
- [ ] 每项独立 commit 后 push；`git status` 干净（含 G5 清理）。
- [ ] **done 报告如实列出 总数/通过/失败**（本轮隐瞒 16 failed）。

## 严禁 / 不做
- 改战斗/经济/补给/确定性公式或 config **数值**（G1 仅复用已有 `eco` 修 NRE、不改地形倍率值；G2 仅加外层 wrapper、不改省份数据值）。
- 执行 F7；编辑治理文档。
- 用 `Assert.Pass`/`Inconclusive`/删测试/改期望凑绿（G3 改期望须按真实意图 + 引用 `CURRENT`）。
- 报完成不附 artifact / 隐瞒失败数。

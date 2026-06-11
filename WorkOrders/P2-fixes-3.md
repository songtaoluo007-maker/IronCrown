# P2-fixes-3 — EditMode 16 失败修复 + 地图管线断裂（v2 · 2026-06-10 更新）

## 地位 / 依赖
re-fixes-2 复审产出（R1-R4 通过；R5 首份 artifact `verify.xml` 暴露 388 中 16 failed）。
**v2 更新**：Claude 已手修 **G1/G2/G4/G5**（经授权的 100% 确定项，见 commit `fix(p2): Claude 手修…`），并在排查中新发现**地图编辑器管线断裂**（新增 **G6/G7**）。OpenClaw 剩余职责：**G3 + G6 + G7 + 全量跑测试出证据**。执行方 OpenClaw，分支 `feature/p2.0-foundation`。

## 状态总览

| 项 | 内容 | 状态 |
|---|---|---|
| G1 | BattleResolver `_config` NRE（5 个 C3 测试） | ✅ Claude 已手修 |
| G2 | provinces.json 裸 array（7 个校验测试 + 运行时） | ✅ Claude 已手修（json 重包 wrapper） |
| G3 | SaveMigration 测试 schema 期望漂移（3 个） | 📤 OpenClaw |
| G4 | UnlockCommander general_blitz 返 null（1 个） | ✅ Claude 已手修（测试战功点 50→200） |
| G5 | 死桩 + 仓库卫生 | ✅ Claude 已手修 |
| **G6** | **tiles 管线断裂：json 手工 tiles 从未到达运行时** | 📤 OpenClaw（新增 🔴） |
| **G7** | **地图编辑器：导出裸 array + 丢字段 / 导入是假实现** | 📤 OpenClaw（新增 🟠） |

## 已手修明细（OpenClaw 不要再动，复跑确认即可）

- **G1**：`BattleResolver.cs` 删 `var ecoDef = _config.Get<…>` 重复获取（原 167 行，无 `?.` 致 C3 测试 NRE），改用同方法 159 行已有的 `eco`。不改任何数值。
- **G2**：`provinces.json` 由裸 `[…]` 重包为 `{ "schemaVersion": 2, "items": […] }`（23 省内容逐字节未动，已验 JSON 合法）。运行时零改动——`NewtonsoftConfigRepository.LoadList` 本就期望 `ConfigFile<T>` wrapper，裸 array 连游戏启动都会炸（佐证 P2 从未真运行过）。⚠ **G7 修复前禁止再用地图编辑器导出覆盖此文件**（编辑器会把它打回裸 array）。
- **G4**：根因 = P2.1 把抽卡券改为战功点成本（N10+R30+SR80=120），但 `SaveLoadEquivalenceTests` 仍给 50 → 解锁第三张 SR 时余 10 不足。已改 200（与同文件 `SaveLoad_RunForward_WithCommanders_Equivalent` 的 200 惯例一致），断言全为相对比较不受影响。
- **G5**：删 `ReplayPlayer.PlayForWorldState()`（`return null` 死桩，零引用）；删 2 个孤儿 `SetupScene.cs.*bak*.meta`；`.gitignore` 加 `*.bak`/`*.bak.meta`；`verify.xml` + `P2-review-fixes-2-done.md` 已入库（证据链）。

## G3 🟠 SaveMigration 测试 schema 期望漂移（3 个）——OpenClaw
**失败** `AlreadyV1_NoSideEffects` / `Migrated_ThenToRuntime_OK`（`Expected: 1 But was: 2`）、`OldSaveNoVersion_DefaultsToV1_LoadsOK`。
**根因** P2.2 把 `SaveSchema.CURRENT` bump 到 2，这些 P2.0b 时代的测试仍期望升到 v1。
**改法（写死，按各测试真实意图）**
- "v1 不被多余改动"类：`new SaveMigrationRunner(migrations, targetVersion: 1)` 注入目标版本，验证纯链式逻辑。
- "无版本旧档加载"类：期望改为 `SaveSchema.CURRENT`。
- 统一原则：**期望引用 `SaveSchema.CURRENT`，不硬编码数字**。
**严禁** 删测试/空断言凑绿。

## G6 🔴 tiles 管线断裂：让 json 的 tiles 真正生效——OpenClaw
**现状（Claude 排查实证）**
- `ProvinceConfig` **没有 tiles 字段** → provinces.json 里每省手工 `"tiles": [...]` 被 Newtonsoft **静默丢弃**。
- `WorldInitializer.cs:91-108` 用 `GetTileTerrains()` **程序生成** 4 格（且 :175-193 写死了 "Mountain 省第 3 格=Hills" 等差异化规则——顺带轻微违规则 5）。
- 结果：**地图编辑器精心刷的每格地形，运行时完全看不见**。P2.5 §B"编辑器产出内容"的价值被中断。

**改法（写死）**
1. `ProvinceConfig` 加嵌套类型与字段：
   `[System.Serializable] public class TileEntry { public string id; public int gridX; public int gridY; public string terrain; }`
   `public TileEntry[] tiles;`
2. `WorldInitializer`：`cfg.tiles != null && cfg.tiles.Length > 0` 时**按 config 建格**（id/坐标/地形逐字段取自 config，`Enum.Parse<TerrainType>(t.terrain)`，按数组顺序加入 `state.tileIds`）；否则 fallback 走现有 `GetTileTerrains` 生成（兼容测试 stub 与无 tiles 的旧配置）。
3. 不改 `GetTileTerrains` 内容（它退化为 fallback；其硬编码差异化规则随地图内容全面编辑器化后自然淘汰，本单不动）。
**验收** 新 EditMode 测试：用含 2 省、每省 4 格、格地形与省地形**不同**的 config 初始化世界 → 断言 `world.tiles` 的地形与 config 完全一致（证明不再走程序生成）。

## G7 🟠 地图编辑器导出/导入修正——OpenClaw
**现状（Claude 排查实证，`Assets/Editor/MapEditorWindow.cs`）**
- `ExportProvincesJson()`（:340-392）**手拼字符串**导出**裸 array**（:385 `$"[\n…\n]"`）——**本次 G2 事故的根源**；且只导出 10 个字段，**丢失** `manpower/railwayLevel/portLevel/airBaseLevel/industrySlots/resourceOutput`（当前 23 省的这些字段已实际丢失）。
- `ImportProvincesJson()`（:424-434）**假实现**：读文件后什么都不做，直接弹"导入成功"对话框（与 P3a 初版同类的"看着对"造假，这次在工具层）。

**改法（写死）**
1. 导出改 Newtonsoft：构造 `ConfigFile<ProvinceConfig>`（含 `schemaVersion=2` + 全字段 items + tiles），`JsonConvert.SerializeObject(…, Formatting.Indented)` 写文件（UTF-8 无 BOM）。删除手拼字符串路径。
2. 导入二选一：**真实现**（Newtonsoft 读 wrapper → 重建 `_provinces`/`_tiles` 网格 → Repaint）或**删除按钮**。**禁止保留假实现**。
3. Editor-only 代码，不进 build（保持 `#if UNITY_EDITOR`）。
**验收** 导出→导入→再导出，两次导出文件语义一致（roundtrip）；导出文件能被 `ConfigValidationTests` 与运行时 `LoadList` 直接加载。
**注** 修复后 23 省缺失的 `resourceOutput/manpower` 数值**由 Claude 另行代拟补齐**（规则 14），不在本单。

## 验收门禁（硬闸门，不变）
- [ ] batchmode 编译 0 error。
- [ ] **EditMode 全绿（0 failed / 0 inconclusive / 0 skipped）** + `artifacts/p2-fixes3-editmode.xml`（本轮时间戳，`git add -f` 入库）。
- [ ] `GoldenReplay_MatchesBaseline` 保持 `Passed`（基线 -2128831035；若 G6 改变初始 tiles 导致 hash 变化，**停下来报告**，由 Claude 判定是否重锁基线——不得擅自改基线）。
- [ ] 受影响 PlayMode（provinces 加载/地图渲染）跑通 + artifact。
- [ ] 每项独立 commit 后 push；`git status` 干净。
- [ ] **done 报告如实列 total/passed/failed/inconclusive 四数**。

## 严禁 / 不做
- 动 G1/G2/G4/G5 已手修的代码与数据；用编辑器导出覆盖 provinces.json（G7 完成前）。
- 改战斗/经济/补给公式或 config 数值；擅自重锁黄金 hash 基线；执行 F7；编辑治理文档（CHANGELOG/PROJECT_STATE/Design）。
- `Assert.Pass`/`Inconclusive`/删测试凑绿；假实现（弹框报成功而不做事）；报完成不附 artifact / 隐瞒失败数。

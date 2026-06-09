# P2-review-fixes — Phase 2 (P2.0–P2.6) 审查修复单

## 地位 / 依赖
Claude 2026-06-09 对 `feature/p2.0-foundation` 上 P2.0b / P2.1–P2.6 实现的逐单静态审查产出。执行方 **OpenClaw**。在本单 + 运行证据齐备前 **P2 不合入 main**（规则 10）。修复继续在 `feature/p2.0-foundation` 分支。

## 背景
P2 三大硬骨头（存档迁移 C-2 / 空间索引 C-5 / 抽卡退役 C-7）已**真落地真接线**，分层（Presentation 零 Domain/Sim、核心层零 UnityEngine）与确定性大体守住。审查发现 **1 必修 + 2 应修 + 3 卫生**，外加 1 项既有确定性债（独立专项）。各项**已写死改法，零留白**（规则 12）。

---

## F1 🔴 必修 — P2.6 遥测：改用 Newtonsoft + 修匿名类型序列化丢数据
**问题** `Assets/Scripts/Infrastructure/Telemetry/LocalJsonTelemetry.cs`
- L208–281 自写私有 `JsonUtility` 类做序列化 → 违规则 8（重复造轮子）+ 偏离技术栈（序列化=Newtonsoft）。
- L263 `GetFields(Public|Instance)` 取不到**匿名类型**（L47 `(object)new { capital=… }`）成员（匿名类型成员是 public **property**、backing field 私有）→ `turn_advanced` 的 `countries` 序列化成空 `{}`，**核心遥测数据丢失**（看着对实则错）。

**改法（写死）**
1. 删除 L208–281 整个私有 `JsonUtility` 嵌套类。
2. L167 改：`string json = Newtonsoft.Json.JsonConvert.SerializeObject(summary, Newtonsoft.Json.Formatting.Indented);`（Infrastructure asmdef 已引用 Newtonsoft，见 `FileSaveRepository`）。
3. 顶部加 `using Newtonsoft.Json;`。
4. 其余不动（`DateTime`/`Guid` 用于遥测 id/文件名，属 Infra 表现，合法）。

**验收** `TelemetryTests` 新增 `TurnAdvanced_SerializesCountryData_NotEmpty`：构造 1 国 snapshot → `TrackTurnAdvanced` → `FlushSessionSummary` → 读回写出的 JSON，断言含 `"capital"` 且 countries 为非空对象。

## F2 🟠 应修 — P2.4 地形聚合：去重复硬编码 + 确定性 tiebreaker
**问题** `Assets/Scripts/Simulation/TerrainAggregator.cs`
- L51–67 `GetBaseDefenseWeight` 硬编码一份地形权重表，**与 `economy.json` 的 `terrainDefenseMult` 完全重复**（值一致）→ 违规则 5（硬编码）+ 规则 8（重复数据源，会漂移）。
- L43–47 平票裁决依赖 `Dictionary` 遍历序（不确定），且 `terrainDefenseMult` 存在同值（Plain=Desert=100 / Swamp=River=120 / Hills=Jungle=115）→ 同值平票结果**不确定**，破坏确定性核心资产。

**改法（写死）**
1. `GetProvinceCombatTerrain` 签名加 `EconomyConfig eco`：`GetProvinceCombatTerrain(ProvinceState province, WorldState world, EconomyConfig eco)`。
2. 平票裁决改为：
   `candidates.OrderByDescending(t => eco != null && eco.terrainDefenseMult.TryGetValue(t.ToString(), out var m) ? m : 100).ThenBy(t => (int)t).First();`
   （先按 config 防御倍率降序、再按 `TerrainType` 枚举值升序兜底 → 唯一确定）。
3. 删除 `GetBaseDefenseWeight`。
4. 唯一调用点 `BattleResolver.cs:168` 补传该上下文已有的 `ecoDef`（L169 已在用）。

**验收** `TerrainAggregatorTests` 新增 `Tie_PlainVsDesert_Deterministic`：省内 2 格 Plain + 2 格 Desert（mult 同 100）→ 连调 100 次结果恒为固定期望（枚举序 Plain<Desert → Plain）。

## F3 🟠 应修 — P2.2 删除误导性死方法
**问题** `Assets/Scripts/Simulation/AdjacencyResolver.cs` L77–88 `MatchesHandwritten` 方法体全为注释、永远 `return true`，名实不符。
**改法（写死）** 删除整个 `MatchesHandwritten` 方法。其语义（自动邻接 == 手写邻接回归）已属 `AdjacencyResolverTests` 职责；若测试当前调用了它，改为测试内直接比对 `GetNeighbors` 与基线。

## F4 🟡 卫生 — P2.1 抽卡退役收尾
1. **事件命名** `CommanderUnlockResolver.cs:78` 新卡解锁发 `CardDrawnEvent`（"抽到"语义残留）。新增 `Contracts/Events/CommanderUnlockedEvent.cs`（字段 `commanderId/cardId/rarity`），解锁路径改发它；`CardDrawnEvent` 若无其它引用则删除。
2. **商城下线** `Presentation/ShopPanelController.cs` 删 `OnBuyBundle`（L32/L56）等买券钩子；`MainHudController` 移除「商城」入口按钮（保留「将领解锁」）。
3. **死配置** `economy.json` L83–87 与 `EconomyConfig.cs` L101–109 **同步删除**随机抽卡参数 `gachaRarityWeightN/R/SR/SSR`、`gachaSsrPityThreshold`、`gachaTicketCostPerDraw`（抽卡退役后无用；两边同删避免 cs↔json 漂移）。`gachaTickets`/`gachaTicketsPerVictory` 等"战功点"语义字段名按 P2.1 决策保留（不破存档）。

**验收** `ConfigValidationTests` 不再引用已删字段且绿；解锁路径测试断言发 `CommanderUnlockedEvent`。

## F5 🟡 卫生 — P2.5 测试覆盖产品路径
**问题** `SpatialIndexTests.cs:55–81` 一致性用例由测试**自己手动** `Remove/Add` 索引，验不了产品代码各路径是否真同步。
**改法（写死）** 新增 `Index_AfterMoveCommand_MatchesTraversal`：经 `GameSessionService` 发真实 `MoveUnit` 命令后，断言 `GetUnitsInProvince` 各省结果 == 全遍历 `units.Where(currentProvinceId==…)`。保留原手动用例作为数据结构单测。

## F6 🟡 卫生 — 仓库卫生
1. `git add` 所有未跟踪 `.meta`（含 `MapRenderer`/`MapInputController`/`AdjacencyResolver`/`TerrainAggregator`/`LocalJsonTelemetry`/`MapEditorWindow`/各新 UXML 的 `.meta`）——Unity 项目 `.cs` 与 `.meta` 必须同提交，否则 CI/他人检出重生成不同 GUID 丢引用。
2. `Tools/` 目录：运行时/构建必需则提交，否则加 `.gitignore`（执行方判定后在 commit body 说明）。

---

## F7 ⚪ 既有确定性债（**本单不执行；建议独立专项 + 需人类确认**）
`Assets/Scripts/Simulation/EconomyResolver.cs` L131/L142：`float stabilityMod = 0.5f + stability/200f`、`float inflationPenalty = (inflation-50)/100f` 参与税收/净收入（T5/B1.5 遗留，C12 整数化漏网）。Phase 2 已将确定性升级为核心资产（异步 PvP/回放）。**建议**改定标整数（×100 定点，仿 C12）。因触及经济公式（规则 9）+ 数值（规则 14），**须人类先确认"数值等价（不改现有平衡结果）"再单独立单**，不混入本单。

---

## 全局验收门禁（强制 — 针对"报完成不附证据"痛点）
- [ ] batchmode 编译 **0 error**（附命令输出）。
- [ ] EditMode **全绿** + `artifacts/p2-fixes-editmode.xml`（**本轮时间戳**，非旧文件）。
- [ ] 受影响 PlayMode（遥测落盘 / 地图选省）跑通 + `artifacts/p2-fixes-playmode.xml`。
- [ ] **Play 截图 2 张**（真实操作，非空场景连拍——Phase1 造假已记）：`p2-fixes-telemetry.png`（一局结束 `Design/telemetry/session-*.json` 含**非空** countries）/ `p2-fixes-midmap.png`（中等地图选省）。
- [ ] CI lint（分层 + UTF-8）+ test 必绿。
- [ ] 每项修复独立 commit，完成后立即 `git push origin feature/p2.0-foundation`。
- [ ] `.meta` 随 `.cs` 提交；`git status` 工作区干净。

## 严禁 / 不做
- 改 BattleResolver/SupplyResolver 战斗补给公式、改任何 config 平衡**数值**（F2 仅改"平票裁决取数来源"，不改 `terrainDefenseMult` 的值）。
- 执行 F7（经济整数化）——须人类先批。
- 编辑 `CHANGELOG.md`/`PROJECT_STATE.md`/`Design/`（Claude 维护）。
- commit `UserSettings`/`Library`。
- 报完成不附 artifact / 截图造假。

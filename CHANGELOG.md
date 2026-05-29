# Changelog

本文件记录 Project Iron Crown 的所有重要变更。
格式遵循 Keep a Changelog，版本遵循 SemVer。
> 规则 7：每次修改必须在此追加条目（日期 + 改动摘要 + 关联规则编号）。
> ⚠ **本文件必须以 UTF-8（无 BOM）保存。** OpenClaw 已多次将其写成非 UTF-8 致中文乱码——在 UTF-8 编码守卫上线前，**OpenClaw 不得直接编辑本文件**，改在 PR 描述附 changelog 文本由 Claude 合入。

## [Unreleased]

### Added
- 2026-05-28 [数值·Claude 代拟] 经人类授权，Claude 写入初版经济数值：`StreamingAssets/Configs/Json/economy.json`（`EconomyConfig` 常量：省份产出/装备配方/工厂维护）+ 填实 `provinces.json`（6 省 `resourceOutput`/基建/人口等，原创、过校验）。规则 14：人类保留最终调整权。
- 2026-05-28 [T4] 确定性与存读档闭环 完成（规则 6,7）：
  - PRNG 换 **SplitMix64**（常量按规格、确定性、跨平台）；`IRandom` 增 `State`/`RestoreState`。
  - 存档持久化 `seed`/`rngState`/`phase`；`SaveMapper.ToSave` 实际写入；新增 `GameClock.Restore` + `GameSessionService.Load` 精确续跑链（`Reset(seed)`→`RestoreState(rngState)`→`Clock.Restore(turn,phase)`）。
  - 测试：RNG 状态往返、同种子初始一致；49/49 EditMode 全绿。
  - Phase 0：独立分支、删 `IEventPublisher` 墓碑、清理散落 `*.log` + 补 `.gitignore`、C1 回退确认（并把 `EconomyResolverTests` 期望修正为截断真值 **89/112** + 新增非整除回归用例 95×0.85→80）。
- 2026-05-28 [T3] 应用用例层与 Contracts 完成（规则 4,6,7）：事件 + `IEventPublisher` 迁入 `Contracts`；`WorldView`/`CountryView` + `ReadModelBuilder`；`GameSessionService` 门面；`GameEntryPoint` 瘦身（保留 `IStartable`）。
- 2026-05-28 [T2] 配置管线 完成（规则 5,6,7）：`*Config` 归位 `Domain/Config`；表入 `StreamingAssets`（`{schemaVersion,items}`）；`IConfigRegistry`/`ConfigRegistry`/`WorldInitializer`；配置校验测试门禁。
- 2026-05-28 [T1] Foundation Migration 完成（规则 3,4,5,6,7）：Core 拆分→Domain/Infra、去单例、DI(VContainer)、Newtonsoft、运行时/存档模型分离、确定性有序遍历、删 `IronCrown.Core`。
- 2026-05-28 初始化项目治理与架构（规则 13）：`PROJECT_RULES.md`、`ARCHITECTURE.md`、`CHANGELOG.md`。
- 2026-05-28 [T0] asmdef 七层骨架（+过渡性 `IronCrown.Core`，已于 T1 移除）；核心层 `noEngineReferences`；零代码改动。

### 审查记录（Claude，规则 13）
- [T1] 通过 → 整改单 T1-FIX（D1/D3/D5/D6）。
- [T2] 实质通过；D1 第二次未修 → Claude 手修 `GameEntryPoint:IStartable` + Bootstrap asmdef 补引用。
- [T3] 实质通过；C1 未授权改公式（税收 `Math.Round`）→ 人类裁定回退、Claude 手修截断；CHANGELOG 曾被写乱码 → Claude 重写。
- [T4] **通过**：SplitMix64 / 状态序列化 / 存读档续跑实现正确（常量逐字符合规格），49/49 绿；C1 测试期望已正确修正。遗留见 Known Issues。

### 工作单台账
- T1 `WorkOrders/T1-foundation-migration.md` ✅
- T1-FIX `WorkOrders/T1-fixes.md` ✅（D1 由 Claude 手修）
- T2 `WorkOrders/T2-config-pipeline.md` ✅
- T3 `WorkOrders/T3-application-contracts.md` ✅
- T4 `WorkOrders/T4-determinism-saveload.md` ✅
- T5 `WorkOrders/T5-economy-gameplay.md` 📤 已签发（含 Claude 代拟经济数值 + UTF-8 守卫 + T4 遗留续跑等价测试）
- T6 `WorkOrders/T6-presentation-ui.md` 📤 已签发（UI Toolkit HUD，规则 4 编译期强制）
- T7 `WorkOrders/T7-integration-demo.md` 📤 已签发（MVP 收官 + 实机演示 + RUNME.md）

### Known Issues
- 2026-05-28 [T4 审查·**编码重犯**] `CHANGELOG.md` 再次被 OpenClaw 写成非 UTF-8（中文乱码），Claude 已**第二次**重写修复。根因：OpenClaw 写文件默认非 UTF-8。处置：T5 Phase 0 增加"UTF-8 编码守卫"（校验所有 `.md/.cs/.json` 为合法 UTF-8）；在此之前 OpenClaw 不得直接编辑本文件。
- 2026-05-28 [T4 审查·测试缺口] 工作单 P4 要求的"存档续跑等价"测试未交付：`DeterminismTests.SameSeed_*` 仅比对两个同种子初始世界、未驱动回合流水线（resolver 仍为桩、回合无实效，等价测试此刻意义有限）。**延后至 T5**（回合有真实效果后补"跑2回合→存→读→再跑2 == 直跑4"等价测试）。RNG 状态往返测试已到位且有效。
- 2026-05-28 [延后] 战斗 `float` 未整数化（"整数优先"确定性目标的剩余项），后续独立任务。

### Decisions
- 2026-05-28 技术栈（人类批准，规则 14）：确定性=整数优先+自定义种子 PRNG；DI=VContainer；UI=UI Toolkit；序列化=Newtonsoft.Json。

### Notes
- 2026-05-28 `ARCHITECTURE.md §7` 已按实际推进重排（T0–T4 见上；MVP 剩 T5/T6/T7）。

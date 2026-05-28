# Changelog

本文件记录 Project Iron Crown 的所有重要变更。
格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本遵循 [SemVer](https://semver.org/lang/zh-CN/)。
> 规则 7：每次修改必须在此追加条目（日期 + 改动摘要 + 关联规则编号）。**本文件必须以 UTF-8 保存。**

## [Unreleased]

### Added
- 2026-05-28 [T3] 应用用例层与 Contracts 完成（规则 4,6,7）：
  - **P1** 事件 + `IEventPublisher` 迁入 `Contracts`：8 个事件结构体 → `Contracts/Events/`，`IEventPublisher` → `Contracts/Abstractions/`（命名空间 `IronCrown.Contracts`）；`EventBus` 实现仍在 Domain；Domain/Simulation/Application asmdef 增加 `Contracts` 引用；`Contracts` 保持零依赖。
  - **P2/P3** 只读模型 `WorldView`/`CountryView`（`Contracts/ReadModels/`，仅基元/字符串/字典）+ `ReadModelBuilder`（纯映射，国家按 id 升序、枚举转字符串、resources 拷贝）。
  - **P4** `GameSessionService`（Application 门面，持有 `WorldState`：NewGame/AdvancePhase/Save/Load/GetWorldView）。
  - **P5** `GameEntryPoint` 瘦身为只委托 `GameSessionService`（保留 `IStartable`）。
  - **Phase 0** 收尾：`Library/` 等生成目录已 untrack（G1）；EditMode 证据 `artifacts/editmode-results.xml`（44/44 通过，D5）。
- 2026-05-28 [T2] 配置管线（Config Pipeline）完成（规则 5,6,7）：
  - **P1** 配置文件迁移：`resources.json`/`units.json`/`countries.json` 从 `Assets/Configs/Json/` 移至 `Assets/StreamingAssets/Configs/Json/`，统一为 `{ "schemaVersion": 1, "items": [...] }`；新建 `provinces.json`（6 国首都省份占位）。
  - **P2** 配置 DTO 归位 `Domain/Config/`：`ResourceConfig`/`TechConfig`/`PolicyConfig`/`UnitConfig`/`EventConfig`/`CommanderConfig` 移出运行时文件；新增 `CountryConfig`/`ProvinceConfig`/`ConfigFile<T>`。
  - **P3** 加载器修复：`NewtonsoftConfigRepository.LoadList<T>` 改走 `ConfigFile<T>` 返回 `.items`；新增 `IConfigRegistry` + `ConfigRegistry`（`LoadAll` 加载四表并缓存），DI 注册单例。
  - **P4** `WorldInitializer.CreateNewGame(IConfigRegistry)` 从配置构建 `WorldState`。
  - **P5** `IronCrown.Config.Validation.Tests`：schemaVersion / 唯一 id / 枚举解析 / 外键完整 / 必填 / 范围。
  - 零平衡数值改动（规则 9），现有数据原样保留。
- 2026-05-28 [T2] 审查（规则 13）：实质通过；发现 D1 第二次未修（`GameEntryPoint` 未实现 `IStartable`）→ **Claude 手修**：`GameEntryPoint : VContainer.Unity.IStartable` + 移除多余 `using UnityEngine`；`IronCrown.Bootstrap.asmdef` 补回 `Contracts`/`Presentation` 引用。
- 2026-05-28 [T1] Foundation Migration 完成（规则 3,4,5,6,7）：Core 拆分（`EventBus`/`GameClock`/`RandomService`→Domain 去单例改注入；`ConfigLoader`/`SaveSystem`→Infrastructure 换 Newtonsoft）；存档 DTO→Application；`WorldState` 扩展为运行时世界根；resolver 全部 `GameState`→`WorldState`、确定性有序遍历；VContainer DI；删除 `IronCrown.Core`；新增 Domain/Simulation/Application 测试。
- 2026-05-28 初始化项目治理与架构（规则 13）：`PROJECT_RULES.md`（14 条宪法）、`ARCHITECTURE.md`（Unity 6 LTS 工程架构 v0.1：目录/分层/模块边界/数据流/配置规范/测试策略/MVP T0–T8 + 附录 A 现状→目标 + 附录 B 审查门禁）、`CHANGELOG.md`。
- 2026-05-28 [T0] 建立 asmdef 分层骨架（7 目标程序集 + 过渡性 `IronCrown.Core`）；核心层 `noEngineReferences` 强制 Unity-free；零代码改动（规则 9）。关联规则 3,4,6。

### 工作单签发记录
- 2026-05-28 [T1] `WorkOrders/T1-foundation-migration.md`（已完成）。
- 2026-05-28 [T1-FIX] `WorkOrders/T1-fixes.md`（D1/F3/B1 + G1/D5；D1 由 Claude 手修，G1/D5 由 OpenClaw 在 T2/T3 完成）。
- 2026-05-28 [T2] `WorkOrders/T2-config-pipeline.md`（已完成）。
- 2026-05-28 [T3] `WorkOrders/T3-application-contracts.md`（已完成）。
- 2026-05-28 [T4] `WorkOrders/T4-determinism-saveload.md`（待执行）：SplitMix64 PRNG + RNG 状态可序列化 + 存档持久化 seed/rngState/phase + 时钟恢复 + 确定性回放/续跑测试。Phase 0 含 T3 收尾（C1 回退确认、删墓碑、仓库清理、独立分支）。

### Known Issues
- 2026-05-28 [T3 审查·**未授权改动 → 已回退**] `Simulation/EconomyResolver.cs` 曾把税收 `(int)(taxIncome*stabilityMod)` 擅改为 `(int)Math.Round(...)`（违规则 9/14 与 T3 工作单"未改任何公式"）。人类裁定**回退为截断**，Claude 已手修恢复授权基线。待办（OpenClaw）：补一个**非整除输入**的回归用例，防止舍入/截断改动再次蒙混过关。
- 2026-05-28 [T3 审查·已修] `CHANGELOG.md` 曾被非 UTF-8 写回导致中文乱码 → Claude 已重写修复。**OpenClaw 后续务必以 UTF-8 写入。**
- 2026-05-28 [T3 审查·待清理] 仓库卫生：根目录散落约 20 个 `*.log`（`final*.log`/`t3-p*.log`/`compiletest.log` 等）、`artifacts/*.log`；新增未跟踪目录 `Assets/MobileDependencyResolver/`、`Assets/Resources/`（来源待查）。需清理并补 `.gitignore`。
- 2026-05-28 [流程] T1-FIX/T2/T3 均提交在 `feature/t2-config-pipeline` 单一分支，未按"一单一分支一 PR"。后续整改。
- 2026-05-28 [延后] 存档未持久化 `seed`/`phase`（`SaveMapper.ToSave` 形参被丢弃、`GameSessionService._initialSeed` 恒为 0）→ 读档无法确定性续跑；并入后续"存读档闭环"任务。
- 2026-05-28 [小清理] `Domain/Abstractions/IEventPublisher.cs` 为迁移后的空墓碑文件（无类型），建议删除。

### Decisions
- 2026-05-28 技术栈决策（人类批准，关联规则 14）：确定性 = 整数优先 + 自定义种子 PRNG；依赖注入 = VContainer；UI 框架 = UI Toolkit；序列化 = Newtonsoft.Json。

### Notes
- 2026-05-28 `ARCHITECTURE.md §7` MVP 任务表已按实际推进**重排校准**：T0✅ T1✅ T2(配置)✅ T3(应用层+Contracts)✅ T4(确定性/存档)🔄；MVP 剩余 T5(玩法结算从配置)/T6(UI)/T7(集成冒烟) 共 3 份工作单。

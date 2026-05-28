# Changelog

本文件记录 Project Iron Crown 的所有重要变更。
格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本遵循 [SemVer](https://semver.org/lang/zh-CN/)。
> 规则 7：每次修改必须在此追加条目（日期 + 改动摘要 + 关联规则编号）。

## [Unreleased]

### Added
- 2026-05-28 [T1] Foundation Migration 完成（规则 3,4,5,6,7）：
  - **Phase 1** 装包：`com.unity.nuget.newtonsoft-json` 3.2.1 + `jp.hadashikick.vcontainer` 1.16.6。
  - **Phase 2** 新增 6 个接口：`IEventPublisher`/`IRandom`/`ITurnClock`（Domain）、`IConfigRepository`/`ISaveRepository`/`IAppLogger`（Application）。
  - **Phase 3** Core → Domain 迁移：`EventBus`/`GameClock`/`RandomService` 命名空间改 `IronCrown.Domain`，删除 `static Instance` 单例，实现接口。
  - **Phase 4** Unity 耦合迁 Infrastructure：`NewtonsoftConfigRepository`（替代 `ConfigLoader`）、`FileSaveRepository`（替代 `SaveSystem`）、`UnityAppLogger`。存档 DTO 拆至 `Application/Persistence/SaveModels.cs`。`WorldState` 扩展为运行时世界根。
  - **Phase 5** Simulation 全部 resolver `GameState` → `WorldState`；`BattleResolver` 注入 `IRandom`+`IEventPublisher`；`TurnResolver` 注入 `ITurnClock`+`IEventPublisher`；遍历按 `id` 升序确定性排序。
  - **Phase 6** VContainer DI：`GameLifetimeScope` 注册全部服务；`GameEntryPoint` 替代 `GameManager`。
  - **Phase 7** 删除 `IronCrown.Core` asmdef + 目录；更新全部 asmdef 依赖图。
  - **Phase 8** 新增 3 个测试程序集（Domain/Simulation/Application），7 个测试类覆盖 `RandomService`/`EventBus`/`WorldState`/`EconomyResolver`/`SaveMapper`。
  - 序列化全部走 Newtonsoft.Json，无 `JsonUtility` 残留。
- 2026-05-28 初始化项目治理与架构（规则 13）：
  - `PROJECT_RULES.md` — 14 条项目宪法 + 执行约定（规则 1–14）。
  - `ARCHITECTURE.md` — Unity 6 LTS 商业级工程架构 v0.1：目录结构、分层说明、核心模块边界、数据流、配置表规范、测试策略、第一阶段 MVP 任务拆分（T0–T8），附"现状→目标差异"与"审查门禁"。
  - `CHANGELOG.md` — 本文件。
- 2026-05-28 [T0] 建立 asmdef 分层骨架（8 个）：目标程序集 `IronCrown.{Contracts,Domain,Simulation,Application,Infrastructure,Presentation,Bootstrap}` 7 个 + **过渡性 `IronCrown.Core`**（保持现有代码可编译，T1 拆分后移除）。核心层 `Domain/Simulation/Application/Contracts` 开启 `noEngineReferences` 强制 Unity-free。新建空目录 `Contracts/Application/Presentation/Bootstrap` 仅含 asmdef，按目标架构预接线。**零代码改动**（规则 9）。关联规则 3,4,6。
- 2026-05-28 [T1] 签发实现工作单 `WorkOrders/T1-foundation-migration.md`（执行方：OpenClaw / DeepSeek V4-Pro）：Core 拆分 + 运行时/存档模型分离 + DI(VContainer) + Newtonsoft + 每模块 EditMode 测试。因 `GameState` 与 Simulation 强耦合，合并了 T2 最小模型修复。
- 2026-05-28 [T1] 审查（规则 13）：结构/分层/确定性/公式保真**通过**；发现 D1（`GameEntryPoint` 未实现 `IStartable` → 永不启动）、D3（测试可发现性待验证）、D5（缺编译/测试证据）、D6（非 git 仓库，规则 10 无法执行）→ 签发整改单 `WorkOrders/T1-fixes.md`。
- 2026-05-28 [T2] 签发配置管线工作单 `WorkOrders/T2-config-pipeline.md`（执行方 OpenClaw）：配置 DTO 归位 `Domain/Config/`、表迁 `StreamingAssets`、`IConfigRegistry` 加载、`WorldInitializer` 初始化世界、配置校验测试门禁。覆盖路线图 T2+T3。**待执行**。

### Known Issues（审查发现，待 T1/T2 修复）
- 2026-05-28 现有 stub 存在既有编译错误（与 asmdef 无关）：
  - `Simulation/EconomyResolver.cs` 使用 `GameState` 但缺 `using IronCrown.Core`。
  - `Simulation/TurnResolver.cs` 将存档 DTO `GameState`/`CountrySaveData` 传入期望运行时 `CountryState` 的 resolver（类型不匹配）。根因 = 存档 DTO 与运行时模型混用（见 ARCHITECTURE.md 附录 A）。
  - 修复路径：T1 拆 `Core` + 补 `using`；T2 统一运行时 `State` 与存档 DTO 边界后方可编译通过。
- 2026-05-28 **工程尚未 Unity 初始化**：缺 `Packages/`、`ProjectSettings/` 及全部 `.meta`；现有 8 个 asmdef 在 Unity 导入前不生效。任何编译/装包/测试前，必须先用 Unity 6 LTS 打开本目录一次以生成工程文件（详见 `WorkOrders/T1-foundation-migration.md` §1）。**[T1 已完成此初始化]**
- 2026-05-28 [T1 残留 → T2 修复] `NewtonsoftConfigRepository.LoadList<T>` 期望顶层数组，但配置为 `{"items":[...]}` 包裹 → 一旦加载即抛异常（T1 未暴露，因尚无调用方）。另：配置仍在 `Assets/Configs/Json/`（非 `StreamingAssets`，加载器读不到），且缺 `CountryConfig` DTO 与 `provinces.json`（`capitalProvinceId` 外键悬空）。均由 `WorkOrders/T2-config-pipeline.md` 修复。

### Decisions
- 2026-05-28 技术栈决策（人类批准，关联规则 14）：确定性 = 整数优先 + 自定义种子 PRNG；依赖注入 = VContainer；UI 框架 = UI Toolkit；序列化 = Newtonsoft.Json。

### Notes
- 现有 `Assets/Scripts` 骨架将按 ARCHITECTURE.md 附录 A 分批迁移（asmdef 分层、Core 拆分、序列化切换、模型分离），属经批准的结构性调整（规则 9 例外），尚未执行。

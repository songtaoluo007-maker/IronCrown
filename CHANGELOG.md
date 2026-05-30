# Changelog

本文件记录 Project Iron Crown 的所有重要变更。
格式遵循 Keep a Changelog，版本遵循 SemVer。
> 规则 7：每次修改必须在此追加条目（日期 + 改动摘要 + 关联规则编号）。
> ⚠ **本文件必须以 UTF-8（无 BOM）保存。** OpenClaw 已多次将其写成非 UTF-8 致中文乱码——在 UTF-8 编码守卫上线前，**OpenClaw 不得直接编辑本文件**，改在 PR 描述附 changelog 文本由 Claude 合入。

## [Unreleased]

### Added
- 2026-05-30 [C4 实现] 战争状态+胜负+军事 AI — 完整战争循环。`WarRelation` 双边战争关系（countryA < countryB Ordinal升序唯一键）；`WarRegistry` 静态工具（TryDeclareWar/AreAtWar/Normalize）；`BattleResolver.InitiateAttack` 校验通过后自动宣战（含空城进驻，基于 ownerCountry 法理主权）；`AIResolver.TryAttack`（填军事 AI 桩，整数战力比 攻方×100 ≥ 守方×aiAttackPowerRatio(120)，受 aiMaxAttacksPerTurn 限制）+ `IsAttackerStrongEnough`；`VictoryConditionResolver`（玩家首都被占→Defeat，全敌首都被占→Victory，Settlement 尾段 TickBattles 之后调用）；`GameOverEvent`→`GameSessionService` 订阅→`GameClock.SetGameOver()`；`IssueCommand` 拒 GameOver + `AdvancePhase` 守卫；`WarDeclaredEvent`；`WarRelationView`；ReadModelBuilder BuildWarRelationView + warRelations/gameOver 字段；SaveMapper 双向持久化 warRelations+gameOver；MainHudController GameOver 事件+禁用推进按钮+`⚔ 交战中`标签；USS `.status-game-over`；economy.json +2 AI 军事字段（aiAttackPowerRatio=120/aiMaxAttacksPerTurn=1）。**18 测试**：BattleResolverC4Tests — WarRegistry 单元(3) + InitiateAttack 自动宣战(3) + VictoryConditionResolver(6) + AI 军事 AI(5) + GameSession GameOver 守卫(1)。所有现有测试 AIResolver 构造函数更新（BattleResolver 参数）。28 files, +1342/-103。
- 2026-05-30 [C3 实现] 战斗与占领系统 — 多回合 HoI 风格战斗。`ActiveBattle` 数据结构（id/attacker/defender/province/turnsElapsed）；`BattleResolver.InitiateAttack`（7 步验证：unit存在→归属→target存在→邻接→非己方控制→移动力≥1→不在战斗中 + 目标省无已有战斗）+ `TickBattles`（1v1 tick + 胜方占领 + 败方消灭 + 清场）+ `DestroyUnit`；`TurnResolver` Settlement 尾段调用 TickBattles；`GameSessionService` MoveUnit 按 controllerCountry 分流（己方→MovementResolver，敌方→BattleResolver）+ 战斗锁定检查；`ReadModelBuilder` 按 controllerCountry 取色 + controllerCountry/isOccupied/hasActiveBattle/isInBattle/activeBattles 列表；`SaveMapper` activeBattles 双向持久化；`MainHudController` 攻击目标红高亮 + 战斗标记 + 事件订阅；USS 新增 attack-target/in-battle/battle-badge 样式。4 事件：BattleInitiatedEvent/BattleConcludedEvent/ProvinceOccupiedEvent/UnitDestroyedEvent。**P2 修复**：garrison cycling 改按 controllerCountry+ownerCountry 过滤（防选中敌部队）；InitiateAttack 拒绝目标省已有战斗（防静默消失）；真多 tick 累积伤害测试。**20 测试**：BattleResolverC3Tests(12) + GameSessionServiceTests(+3) + ReadModelBuilderTests(+4) + SaveLoadEquivalenceTests(+1)。规则 3/4/5/8/9 全守。30 files, +1198/-72。

- 2026-05-29 [C2a 签发] `WorkOrders/C2a-unit-production.md`（执行方 OpenClaw）：军事阶段第二步·部分一——玩家在首都训练 1 支步兵，下单一次性扣 `infantry.cost` + manpower=hp，2 回合后完工驻首都。**人类拍板的设计取舍**（规则 14）：C2a 造兵 / C2b 移动拆两单；移动模型=单步邻接+movesLeft；造兵地点仅首都；成本=`UnitConfig.cost`+manpower+多回合队列。**Phase 0 强制收 C1 三项尾巴**：① `SaveLoadEquivalenceTests.HashWorld` 扩 units 13 字段 + 省份静态字段（C1 续跑等价测试本身是空检查的盲区）；② `UnitSaveData` 扩 13 字段（决策 A：全字段持久化）+ `SaveMapper.ToRuntime` 重建 `country.unitIds`（激活 C1 起的死字段）；③ `ReadModelBuilder` 遍历 units 按 id 升序。新增 `Simulation/UnitProductionResolver`、`Domain/UnitFactory`（与 C1 WorldInitializer 步兵创建块共享，规则 3）、`Contracts/UnitProducedEvent`。**新数值**（Claude 代拟）：`economy.json` 加 `unitProductionTurns=2`（规则 14 可调）。
- 2026-05-29 [C1 审查通过] 领土邻接 + 初始部队 + 地图驻军 — 军事阶段地基达成。**主体过**：6 省 neighbors 对称（high_peak 4 邻枢纽）；6 支初始 infantry 满编驻各国首都；ProvinceView+garrisonCount/neighbors；地图 `⚔N` 徽章 + 详情邻接/驻军；SaveMapper 顺补 B2 漏存的 gridX/gridY/terrain。**最新 artifact**：`artifacts/c1-editmode5.xml`（97/97）+ `c1-playmode-final.xml`（5/5）为权威证据；**c1-editmode-final.xml 是更早版本（3 failed）—命名混乱**，C2a Phase 0 清理。**8 项尾巴**：① HashWorld 不含 units 致续跑等价失明（C2a Phase 0.1 修）；② UnitSaveData 字段不全（C2a Phase 0.2 决策 A 全字段持久）；③ ReadModelBuilder 遍历 units 无序（C2a Phase 0.3）；④ garrisonCount O(P*U) 触发技术债 C-5 提醒；⑤ artifacts 命名乱（C2a Phase 0.4 清理）；⑥ commit 越界将 Claude dirty 治理文件一并打包（OpenClaw commit 卫生需改）；⑦ OpenClaw 顺补 B3 真 bug `EconomyConfig` 缺 `aiBuildCapital*` 3 字段（B3 审查盲区，记录复盘——Claude 以后必交叉 cs 与 json 字段）；⑧ Play 截图待补（与 C2a 一并出 `Design/screenshots/c1-*.png`）。**复盘**：`Ideology` 枚举（Domain）与 `ConfigValidationTests.validIdeologies` 集合漂移——硬编码字符串集合应改为引用枚举，列入后续技术债。
- 2026-05-28 [记忆/恢复机制] 为解决"跨会话/上下文上限后丢失项目脉络"问题,建立 `PROJECT_STATE.md`(项目恢复入口/状态快照):新会话读它即可重建全局(进度时间线 T0→当前、锁定决策、协作复盘、文件地图、下一步、技术债指针)。原则:**真相源在仓库 git 跟踪文件,非任何人私有记忆**(私有记忆会丢、OpenClaw 读不到)。`PROJECT_RULES.md` 执行约定新增"记忆/恢复机制"条:新会话先读 PROJECT_STATE、每工作单审查通过后更新它。Claude 私有记忆顶部加指针导向 PROJECT_STATE。

### Milestone
- 2026-05-29 ✅ **C1 闭合（军事地基）**：领土邻接 + 初始部队 + 地图驻军达成，EditMode 97/97 + PlayMode 5/5 全绿（权威 artifact: `c1-editmode5.xml`+`c1-playmode-final.xml`）。C 阶段（军事）拆 C1✅ → C2a(造兵, 已签发) → C2b(移动) → C3(战斗+占领) → C4(战争胜负+军事 AI)。
- 2026-05-28 🎉 **B 阶段收官（可玩性达成）**：B1(命令管线+建厂)→B1.5(税率/民生)→B2(2D方块地图+选省)→B3(AI 自主建厂) 全绿（112 测试）。完整循环：选国→建厂/调税民生→推回合看经济→AI 对手自主发展→存读档。B3 审查通过（TryBuild 重构干净、AI 确定性无随机、playerCountryId 存档同步、规则 3/4/5 守住）。
- 2026-05-28 🎉 **MVP 垂直切片达成（A 收口完成）**：EditMode 69/69 + PlayMode 5/5 全绿。配置驱动（6 国 6 省）→ 回合推进有可见经济产出 → 存读档确定性一致 → UI Toolkit HUD 可视可操作。T0–T7 + T7-FIX 全部闭合。下一步进入 B（玩家可玩性：命令 + 2D 地图 + AI 行动）。

### Added
- 2026-05-28 [记忆/恢复机制] 为解决"跨会话/上下文上限后丢失项目脉络"问题,建立 `PROJECT_STATE.md`(项目恢复入口/状态快照):新会话读它即可重建全局(进度时间线 T0→当前、锁定决策、协作复盘、文件地图、下一步、技术债指针)。原则:**真相源在仓库 git 跟踪文件,非任何人私有记忆**(私有记忆会丢、OpenClaw 读不到)。`PROJECT_RULES.md` 执行约定新增"记忆/恢复机制"条:新会话先读 PROJECT_STATE、每工作单审查通过后更新它。Claude 私有记忆顶部加指针导向 PROJECT_STATE。
- 2026-05-28 [架构可持续性评估] 应人类要求评估"架构是否利于长期运维/更新"，结论=地基为长期可维护设计(编译期分层/数据驱动/确定性/测试网/Ports)、起点高,但需后续补规模化基础设施。新增 `ARCHITECTURE.md` 附录 C「技术债与未来基础设施清单」(6 项,带触发条件)：C-1 CI 自动门禁(高,减少对人工审查依赖)、C-2 存档迁移(高,上线红线)、C-3 配置工具链、C-4 Presentation 组织(MainHudController 已膨胀)、C-5 移动端性能、C-6 本地化。约定 Claude 审查时若触及触发条件应主动提醒。
- 2026-05-28 [军事阶段启动 / C1 签发] 人类选定下一主攻方向=军事与领土（规则 14）。军事拆为 C1(领土+驻军地基)→C2(造兵+移动)→C3(战斗+占领)→C4(战争状态+胜负+军事AI)；美术顺延至 D。`WorkOrders/C1-territory-garrison.md`：省份邻接 + 各国初始步兵驻首都 + 地图显示驻军，**不做移动/战斗**，仍用 6 省（先立框架，省份扩充后续）。Claude 代拟邻接表（6 省对称邻接，high_peak 为枢纽）+ 初始部队规则（每国 1 步兵驻首都、取 units.json infantry 模板）。强调存档完整性：ProvinceSaveData 加 neighbors、确认 gridX/gridY 双向映射（防读档丢静态省份数据）。
- 2026-05-28 [B3 签发] `WorkOrders/B3-ai-actions.md`（执行方 OpenClaw）：B 阶段收官——非玩家国自主经济决策（规则 AI 建厂、阈值、纯确定性无随机）。含**经批准小重构**（规则 9 例外）：建造执行逻辑从 `IssueCommand` 下移到 `ConstructionResolver.TryBuild`，玩家与 AI 共用（规则 3）；`WorldState` 加 `playerCountryId` 使 AI 跳过玩家国。数值 Claude 代拟入 economy.json：`aiBuildCapitalThreshold=60`/`aiMaxCivilianFactories=20`/`aiMaxMilitaryFactories=15`（规则 14 可调）。顺修 B2 地图区高度重叠。
- 2026-05-28 [B2 审查] **通过**：6 省方块按方位/配色渲染、点击选中+详情栏正确；规则 4 守住（Presentation 零 Domain/Simulation 引用）、`ProvinceView` 已建、数据全取自 config。小瑕疵：地图最下排方块与详情栏轻微重叠（B3 Phase 0 顺修）。
- 2026-05-28 [B2 签发] `WorkOrders/B2-map-view.md`（执行方 OpenClaw）：文字列表 → 2D 省份方块地图（交互骨架）。UI Toolkit 绝对定位摆 6 省方块、按国 `mapColor` 配色、点击选中→详情栏。新增 `ProvinceView`、`WorldView.provinces/selectedProvinceId`、`GameSessionService.SelectProvince`。**架构师范围**：仅交互骨架（色块粗糙、美术后置 C 阶段），不做邻接/领土/占领/更多省份。Claude 代拟地图数据（6 省 gridX/gridY 方位 + 6 国 hex 配色，规则 14 可调）写为工作单数据表交 OpenClaw 填入 JSON。
- 2026-05-28 [B1.5 签发] `WorkOrders/B1.5-governance.md`（执行方 OpenClaw）：内政经营杠杆——税率档 + 民生档（复用 B1 命令管线，`SetTaxLevel`/`SetCivilLevel`、`GameCommand.level`），税收/民生倍率入 `EconomyResolver`、稳定修正入 `PoliticsResolver`（本单给其注入 IConfigRegistry）。**架构师范围判断**：砍掉原计划的"生产分配"——当前无军队、装备产线分配无消耗端无玩家价值，为做而做增复杂度（规则 9），推迟到军事系统之后。数值 Claude 代拟入 `economy.json`：税率 `[70,100,130]%`、税稳 `[+1,0,-2]`、民生支出 `[50,100,150]%`、民生稳 `[-2,0,+2]`（规则 14 人类可调）。
- 2026-05-28 [B1.5 审查] **通过**：`PoliticsResolver` 注入 config、税率/民生稳定修正取自配置（有 null/越界防护、整数确定性，规则 3/5 守住）；实测「民生紧缩→稳定 60→52」效果正确。视觉收尾：用户反馈深背景下部分文字看不见——根因 USS 多数文字未设颜色用了默认深色。**Claude 手修** `MainHud.uss`：`.root` 设全局浅色文字（继承）+ 深蓝灰底板，`.turn-label` 纯白强调。停止 Play 再重新 Play 即生效（USS 自动重导入，无需重跑 Setup Main Scene）。
- 2026-05-28 [B1 签发] `WorkOrders/B1-command-pipeline.md`（执行方 OpenClaw）：命令管线骨架 + 新游戏选 1 国 + 唯一命令「建造工厂」端到端（`GameCommand`/`CommandResult` 入 Contracts、`ConstructionResolver` 入 Simulation、多回合建造、存档纳入在建队列）。B 阶段（可玩性）拆为 B1（本单，地基+建厂）/ B1.5（调生产·税收民生）/ B2（2D 地图）/ B3（AI 行动）；科技树待人类单独设计。人类决策：玩家操作四类全要但分批、新游戏选 1 国。
- 2026-05-28 [B1 审查] 核心实现**通过**：`ConstructionResolver`（入队/每回合推进/完工+1，确定性）、`SaveMapper`（含 constructionQueue/resources/工厂数，续跑完整）、`TurnResolver` 结算接线、命令经 Simulation 执行——规则 3/4/5 守住。发现收尾问题→签发 `WorkOrders/B1-fixes.md`：
  - **[看不到 UI·根因]** 用户反馈"看不到画面"。真因：① 场景 UIDocument 的 `PanelSettings = None`；② `SetupScene.cs` 把 `themeStyleSheet = null` → UI Toolkit 运行时无主题不渲染（黑屏）。**之前 PlayMode 绿是盲区**：测试只查内存 VisualElement 树，未验证"真渲染到屏幕"（PanelSettings/theme 存在）。**Claude 手修** `SetupScene.cs`：themeStyleSheet 加载 `UnityDefaultRuntimeTheme.tss`。需用户重跑 `IronCrown > Setup Main Scene` 重建场景。
  - **[测试 bug]** `SetPlayerCountry_ChangesPlayer` 红：测试用空 `StubConfigRepository` 建世界却 `SetPlayerCountry("republic_west")`，而产品代码正确校验"国家须存在"→ 空世界拒绝设置。产品对、测试错，待 OpenClaw 修。
  - **[认知澄清]** 项目当前**无地图**（地图是 B2）；现阶段画面 = UI HUD。
  - **[文字重叠·根因]** theme 修好后 HUD 显示，但用户实测推进后顶栏/状态文字重叠。根因：`SetupScene` 建的是**无相机空场景** → Game 视图不清屏 → UI Toolkit 动态改 Label.text 后新旧文字网格累积叠加。**Claude 手修** `SetupScene.cs` 新增正交相机（SolidColor 清屏）。需用户重跑 `Setup Main Scene`。功能本身正常（经济结算数字逐回合正确变化）。
- 2026-05-28 [数值·Claude 代拟] B1 建造数值写入 `economy.json`：`civilianFactoryBuildCost=30`/`militaryFactoryBuildCost=40`（资本）/`factoryBuildTurns=3`（规则 14 人类可调）。
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
- [T5/T6/T7] T5 经济**通过**（公式按规格、数值未改、分支纪律恢复、CHANGELOG 未被乱改）。**T6/T7 打回**：① `MainHudController` 注册进 DI 但从未注入 `MainHudBehaviour`（`SetController` 无 `[Inject]`、缺 `RegisterComponentInHierarchy`）→ HUD 空白，**演示无效**；② **未真实验证**——`editmode-results.xml` 仍是 T4 旧结果(49)、T5/T6/T7 新测试未重跑、无 PlayMode 结果；③ `MvpSmokeTests` 纵容空 HUD 假性通过。→ 签发 `WorkOrders/T7-fixes.md`。OpenClaw 后续用 `GameEntryPoint.SetController` 方式接线，但仍漏 `Bind`（`OnEnable` 早于注入跳过、`SetController` 不触发渲染）→ **Claude 手修**：`SetController` 在控制器到位时触发 `Bind`。仍需 OpenClaw 完成 T7-FIX 的 F3（收紧冒烟）+ F4（真跑 EditMode/PlayMode + Play 截图）。
- 2026-05-28 [首次真跑·74 用例 EditMode] 68 过 / 6 败（终于有新鲜证据）。诊断：① 5 败来自 `IronCrown.PlayMode.Tests`——asmdef 配错（`includePlatforms:["Editor"]` + 缺 `TestRunner` 引用）致混入 EditMode 跑失败、且未现于 PlayMode 标签页 → **Claude 已手修 asmdef**（`includePlatforms:[]` + 加 TestRunner 引用）。② 1 败=`SaveLoadEquivalenceTests` **正确抓出真 bug**：存档 DTO 不完整（丢 `resources`/`equipmentStockpile`/工厂/人力 及省份 `infrastructure`/`resourceOutput`）→ 续跑≠直跑。→ T7-FIX 增 **F5**（补全存档字段，完整快照）+ **F6**（PlayMode asmdef，已手修）。68 个通过覆盖 economy/config/domain/sim/presentation。
- 2026-05-28 [F5/F6 复跑] EditMode **69/69 全绿**（存档补全成功、PlayMode 测试已与 EditMode 干净分离）；PlayMode 5 个测试已正确出现在 PlayMode 标签页但 **5 全红**（含 `MainScene_Loads_WithoutErrors`）。诊断：场景在 Build Settings 且 enabled、UXML 控件名匹配且 `turn-label` 默认非空 → 根因指向 `MvpSmokeTests` 用**同步 `LoadScene`+两帧 yield**，Test Runner 下场景未完成加载即断言。→ **Claude 手修**：抽出 `LoadMainScene()` 用 `LoadSceneAsync` 等 `isDone` 再等 10 帧，4 个测试统一调用。⚠ 若重跑仍红需看 Console 实际异常（可能叠加 PlayMode 下 DI/配置加载问题）。
- 2026-05-28 [PlayMode 根因定位·已修] 诊断开关 + Console 日志锁定真因：`MainHud.uxml` 用了非法 UXML 写法 `<ui:StyleSheet src=...>`（`StyleSheet` 非 UXML 元素）→ `UIDocument.OnEnable` 抛 `Debug.LogError` → Unity Test Framework 将运行期 LogError 一律判失败 → 5 个 PlayMode 测试全红，**与功能无关**。日志同时证明功能正常：6 国 6 省初始化、HUD 绑定、turn-label="回合 1 · TurnStart"。**Claude 手修**：`<ui:StyleSheet>` → `<Style src=...>`（Unity 6 正确写法），并移除诊断用 `LogAssert.ignoreFailingMessages`，恢复 F3 严格断言。待用户重跑确认 PlayMode 转绿 + Play 截图。
- 2026-05-28 [PlayMode 最后 1 红·已修] StyleSheet 修复后 4 个转绿，仅 `HUD_AdvanceButton_ChangesTurnLabel` 红：`advanceBtn.SendEvent(new ClickEvent())` 不触发回调（`EventBase.target` internal set，测试无法构造可分发的合成点击）——测试基础设施问题，非产品 bug（真人鼠标点击经 Clickable 正常）。**Claude 手修**：`MainHudController` 暴露公开 `Advance()`（按钮回调与程序化调用共用入口）、`MainHudBehaviour` 暴露只读 `Controller`，测试改经 `Controller.Advance()` 验证"推进→label 变"。待重跑确认 5/5 绿。

### 工作单台账
- T1 `WorkOrders/T1-foundation-migration.md` ✅
- T1-FIX `WorkOrders/T1-fixes.md` ✅（D1 由 Claude 手修）
- T2 `WorkOrders/T2-config-pipeline.md` ✅
- T3 `WorkOrders/T3-application-contracts.md` ✅
- T4 `WorkOrders/T4-determinism-saveload.md` ✅
- T5 `WorkOrders/T5-economy-gameplay.md` ✅ 实现通过（数值未改、规则 4/9/14 守住）
- T6 `WorkOrders/T6-presentation-ui.md` ⚠ 结构对（规则 4 守住、Presentation 仅引用 Application+Contracts），但 UI 控制器未接线 → HUD 空白
- T7 `WorkOrders/T7-integration-demo.md` ⚠ 场景生成脚本/冒烟/RUNME 在，但演示未跑通、未真实验证
- T7-FIX `WorkOrders/T7-fixes.md` 📤 已签发（接线 UI 控制器 + 首屏渲染 + 收紧冒烟 + 真跑 EditMode/PlayMode + Play 截图为证）

### Known Issues
- 2026-05-28 [T4 审查·**编码重犯**] `CHANGELOG.md` 再次被 OpenClaw 写成非 UTF-8（中文乱码），Claude 已**第二次**重写修复。根因：OpenClaw 写文件默认非 UTF-8。处置：T5 Phase 0 增加"UTF-8 编码守卫"（校验所有 `.md/.cs/.json` 为合法 UTF-8）；在此之前 OpenClaw 不得直接编辑本文件。
- 2026-05-28 [T4 审查·测试缺口] 工作单 P4 要求的"存档续跑等价"测试未交付：`DeterminismTests.SameSeed_*` 仅比对两个同种子初始世界、未驱动回合流水线（resolver 仍为桩、回合无实效，等价测试此刻意义有限）。**延后至 T5**（回合有真实效果后补"跑2回合→存→读→再跑2 == 直跑4"等价测试）。RNG 状态往返测试已到位且有效。
- 2026-05-28 [延后] 战斗 `float` 未整数化（"整数优先"确定性目标的剩余项），后续独立任务。

### Decisions
- 2026-05-28 技术栈（人类批准，规则 14）：确定性=整数优先+自定义种子 PRNG；DI=VContainer；UI=UI Toolkit；序列化=Newtonsoft.Json。

### Notes
- 2026-05-28 `ARCHITECTURE.md §7` 已按实际推进重排（T0–T4 见上；MVP 剩 T5/T6/T7）。

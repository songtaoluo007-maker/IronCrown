# ARCHITECTURE.md — Project Iron Crown 工程架构

- **版本**：v0.1（草案，待人类验收）
- **日期**：2026-05-28
- **引擎**：Unity 6 LTS（IL2CPP，移动端 Android/iOS 优先）
- **作者/责任人**：Claude（主架构师，规则 13）
- **约束来源**：[`PROJECT_RULES.md`](PROJECT_RULES.md)。本文件是该宪法的工程实现，**冲突以宪法为准**。

> 本文件只定义**骨架与边界**，不含具体业务实现。后续实现与测试由 Codex 完成、批量与流程由 OpenClaw 完成，Claude 按本文件 + 附录 B 审查门禁逐模块审查。

---

## 0. 已确认技术栈决策

| 维度 | 决策 | 理由 |
|------|------|------|
| 架构范式 | Clean Architecture + 端口与适配器（Ports & Adapters） | 用 asmdef 在**编译期**强制规则 3/4，UI 物理上看不到 Domain |
| 确定性 | Simulation 整数/定标整数优先 + 自定义种子 PRNG；`float` 仅表现层 | 支持回放、存档一致性校验、未来异步 PvP；回合制成本低 |
| 依赖注入 | VContainer | 轻量、启动快、编译期友好，契合分层 |
| UI 框架 | UI Toolkit（UXML/USS + 运行时数据绑定） | 数据密集面板多、移动端开销低、Unity 6 主推 |
| 序列化 | Newtonsoft.Json | 支持 `Dictionary`/多态/顶层数组，修正现有 `JsonUtility` 缺陷 |

### 0.1 设计原则

1. **依赖单向内指**：外层依赖内层，内层永不反向依赖。`Presentation → Application → Simulation → Domain`，反向通过事件/只读模型回流。
2. **核心纯净**：`Domain`/`Simulation`/`Application` **不引用 `UnityEngine`**，可在无 Unity 环境下纯 C# 单测（支撑规则 6）。
3. **确定性**：相同种子 + 相同命令序列 ⇒ 相同最终状态（可哈希比对）。Simulation 内禁用 `DateTime.Now`、`UnityEngine.Random`、未排序的 `Dictionary` 遍历参与数值计算。
4. **数据驱动**：所有平衡数值来自 Config（规则 5），代码只描述"如何算"，不写"算多少"。
5. **可测试优先**：每个核心模块对应一个 EditMode 测试程序集（规则 6）。

---

## 1. 目录结构

```
IronCrown/                              # 仓库根
├─ PROJECT_RULES.md                     # 项目宪法（规则源）
├─ ARCHITECTURE.md                      # 本文件
├─ CHANGELOG.md                         # 变更记录（规则 7）
├─ README.md
│
├─ Configs~/                            # 策划源表（Excel/CSV）。"~" 后缀 → Unity 不导入
│  ├─ tables/                           #   源表（人类/策划编辑）
│  └─ export/                           #   导出+校验脚本（源表 → StreamingAssets JSON）
│
├─ Tools/                               # CI 脚本、配置校验 CLI、批处理（OpenClaw 领域）
│
├─ Assets/
│  ├─ Scripts/
│  │  ├─ Contracts/        # asmdef: IronCrown.Contracts        (引用: 无)
│  │  │  ├─ Enums/         #   Ideology, TerrainType, GamePhase, FactoryType ...
│  │  │  ├─ Ids/           #   强类型 Id（CountryId/ProvinceId...，避免裸 string 混用）
│  │  │  ├─ ReadModels/    #   只读投影 DTO：CountryView, ResourcePanelView ...
│  │  │  ├─ Commands/      #   玩家意图：EndTurnCommand, AllocateFactoryCommand ...
│  │  │  └─ Events/        #   领域事件（只读）：TurnStartEvent, BattleResolvedEvent ...
│  │  │
│  │  ├─ Domain/           # asmdef: IronCrown.Domain           (引用: Contracts)
│  │  │  ├─ State/         #   运行时可变状态：CountryState, ProvinceState, UnitState, WorldState
│  │  │  ├─ Config/        #   不可变配置 DTO：ResourceConfig, BuildingConfig, UnitConfig, TechConfig ...
│  │  │  ├─ Rules/         #   纯领域不变量/公式（无随机、无 IO、无 Unity）
│  │  │  └─ Abstractions/  #   IRandom, ITurnClock, IEventPublisher + 确定性 PRNG 实现
│  │  │
│  │  ├─ Simulation/       # asmdef: IronCrown.Simulation       (引用: Domain, Contracts)
│  │  │  ├─ Pipeline/      #   TurnResolver（仅编排，无业务）, Phase 顺序
│  │  │  └─ Resolvers/     #   Economy/Politics/Battle/Supply/Diplomacy/AI Resolver（核心玩法，规则 3）
│  │  │
│  │  ├─ Application/      # asmdef: IronCrown.Application       (引用: Domain, Simulation, Contracts)
│  │  │  ├─ Services/      #   GameSessionService, CommandDispatcher
│  │  │  ├─ Handlers/      #   命令处理器（Command → Simulation 调用）
│  │  │  ├─ Queries/       #   ReadModel 构建器（State → ReadModel 映射）
│  │  │  ├─ Mapping/       #   运行时 State ↔ 存档 DTO 映射
│  │  │  └─ Ports/         #   对基础设施的接口：IConfigRepository, ISaveRepository, IAppLogger
│  │  │
│  │  ├─ Infrastructure/   # asmdef: IronCrown.Infrastructure   (引用: Application, Domain, Contracts, UnityEngine)
│  │  │  ├─ Config/        #   NewtonsoftConfigRepository（读 StreamingAssets/Addressables）
│  │  │  ├─ Persistence/   #   FileSaveRepository（persistentDataPath）
│  │  │  ├─ Logging/       #   UnityAppLogger
│  │  │  └─ Platform/      #   SeedProvider 等平台服务
│  │  │
│  │  ├─ Presentation/     # asmdef: IronCrown.Presentation     (引用: Application, Contracts, UnityEngine, UIToolkit)
│  │  │  ├─ ViewModels/    #   可绑定 VM（由 ReadModel + 事件流构建）
│  │  │  ├─ Views/         #   UI Toolkit 控制器（绑定 UXML/USS）
│  │  │  ├─ Presenters/    #   VM ↔ Service 协调
│  │  │  └─ Input/         #   输入 → Command 转换
│  │  │      # ⚠ 不引用 Domain / Simulation / Infrastructure（规则 4 编译期强制）
│  │  │
│  │  └─ Bootstrap/        # asmdef: IronCrown.Bootstrap        (引用: 全部 + VContainer)
│  │     ├─ GameLifetimeScope.cs   #   VContainer 装配（唯一组合根）
│  │     └─ EntryPoint.cs          #   唯一 MonoBehaviour 入口
│  │
│  ├─ UI/                  # UXML / USS / 主题 / 图集（原创美术，规则 2）
│  ├─ StreamingAssets/
│  │  └─ Configs/          # 运行时 JSON（由 Configs~/ 导出；可热更/打包）
│  ├─ Art/  Audio/  Scenes/  Settings/
│  │
│  └─ Tests/
│     ├─ EditMode/         # 纯 C# 快测（无需进游戏）
│     │  ├─ IronCrown.Domain.Tests.asmdef
│     │  ├─ IronCrown.Simulation.Tests.asmdef
│     │  ├─ IronCrown.Application.Tests.asmdef
│     │  └─ IronCrown.Config.Validation.Tests.asmdef   # 配置完整性 CI 门禁
│     └─ PlayMode/         # 集成/绑定冒烟（最少量）
│        └─ IronCrown.Integration.Tests.asmdef
│
├─ ProjectSettings/
└─ Packages/               # manifest 含 com.unity.nuget.newtonsoft-json, VContainer, UITK
```

> **说明**：现有代码位于 `Assets/Scripts/{Core,Domain,Simulation,Infrastructure}`，无 asmdef、`Core` 耦合 Unity。向上述目标的迁移是 MVP 的 T0/T1（见 §7 与附录 A），属"经批准的结构性调整"，不违反规则 9。

---

## 2. 分层说明

### 2.1 程序集依赖图（asmdef 强制，箭头=允许引用）

```
                         ┌─────────────────────────────┐
                         │        Contracts            │  无依赖
                         │ enums / ids / readmodels /  │  纯数据词汇表
                         │ commands / events           │
                         └────────────▲────────────────┘
            ┌─────────────────────────┼──────────────────────────┐
            │                         │                          │
   ┌────────┴────────┐      ┌─────────┴────────┐       ┌─────────┴────────┐
   │     Domain      │◄─────│   Simulation     │◄──────│   Application    │
   │ State/Config/   │      │ 确定性玩法逻辑    │       │ 用例/命令/查询/   │
   │ Rules/Abstract  │      │ (规则 3)         │       │ Ports/Mapping    │
   └────────▲────────┘      └──────────────────┘       └───▲──────────┬───┘
            │                                              │          │
            │                       实现 Ports             │          │ 提供 Service/ReadModel/事件流
            │                ┌─────────────────────────────┘          │
   ┌────────┴───────────────┴──┐                          ┌───────────▼───────────┐
   │     Infrastructure        │                          │     Presentation       │
   │ Newtonsoft/File/Log/Plat  │                          │ UI Toolkit / VM / Input│
   │ (+UnityEngine)            │                          │ (+UnityEngine, 规则 4) │
   └───────────────────────────┘                          └────────────────────────┘
            ▲                                                          ▲
            └──────────────────────┬───────────────────────────────────┘
                          ┌────────┴─────────┐
                          │    Bootstrap     │  引用全部 + VContainer
                          │  组合根 / 入口    │  唯一允许 new 跨层依赖之处
                          └──────────────────┘
```

### 2.2 各层职责与红线

| 层 / 程序集 | 职责 | 可引用 | 禁止 | 对应规则 |
|---|---|---|---|---|
| **Contracts** | 跨层共享的**只读词汇表**：枚举、强类型 Id、只读模型、命令、事件 | 无 | 任何可变状态、任何逻辑 | 4 |
| **Domain** | 运行时状态实体 + 不可变 Config DTO + 纯领域不变量 + 确定性抽象(IRandom/IClock) | Contracts | UnityEngine、IO、随机源实现以外的副作用 | 3,5 |
| **Simulation** | 全部核心玩法结算逻辑（回合流水线、各 Resolver） | Domain, Contracts | UnityEngine、IO、直接 new 随机/时钟（须注入） | **3** |
| **Application** | 用例编排、命令分发与校验、ReadModel 构建、存档映射、定义基础设施 Ports | Domain, Simulation, Contracts | UnityEngine、具体 IO 实现 | 4 |
| **Infrastructure** | 实现 Application 的 Ports：Newtonsoft 配置加载、文件存档、日志、平台服务 | Application, Domain, Contracts, UnityEngine | 反向被核心层引用 | 5 |
| **Presentation** | UI Toolkit 视图、ViewModel、Presenter、输入→命令 | **Application, Contracts**, UnityEngine, UIToolkit | **引用 Domain / Simulation / Infrastructure**；直接改任何状态 | **4** |
| **Bootstrap** | 组合根：VContainer 装配、唯一 MonoBehaviour 入口 | 全部 | 写业务逻辑 | 3,4 |

**规则如何被编译期强制**

- **规则 3**（核心逻辑在 Simulation）：核心 Resolver 只能放进 `IronCrown.Simulation`；Presentation/Infrastructure 不引用它，无法"借道"实现玩法。
- **规则 4**（UI 不碰 Domain）：`Presentation.asmdef` **不引用** `Domain`/`Simulation`。UI 只能 ① 读 `Contracts` 里的 ReadModel/Event ② 调 `Application` 的 Service/Command。物理上拿不到 `CountryState` 等可变类型。
- **规则 5**（数值来自 Config）：Simulation 取数只能经注入的 `IConfigRepository`/Config DTO；代码里出现魔法数字由审查门禁（附录 B）+ 静态检查拦截。

---

## 3. 核心模块边界

每个"核心模块"= 一个**状态所有者 + 一个 Resolver + 一组命令/事件**。**状态归属唯一**，跨模块只能读对方状态、不可写（规则 8 防重复系统）。

### 3.1 模块归属表

| 模块 | 拥有(写)的状态 | 只读引用 | 输入命令 | 产出事件 | 主结算器 |
|---|---|---|---|---|---|
| **TurnPipeline** | `GameClock`/`WorldState`(回合、阶段) | 全部 | `EndTurnCommand` | `TurnStartEvent`/`TurnEndEvent` | `TurnResolver`（仅编排） |
| **Economy** | `CountryState` 经济/工厂/资源字段、`ProductionLineState` | Tech, Province | `AllocateFactoryCommand`/`SetProductionCommand` | `ResourceChangedEvent` | `EconomyResolver` |
| **Politics** | `CountryState` 政治字段(稳定/合法/腐败/政策) | Economy | `EnactPolicyCommand` | `PolicyChangedEvent` | `PoliticsResolver` |
| **Research** | `CountryState` 科技(已研/在研) | Economy | `SetResearchCommand` | `TechCompletedEvent` | （归 Economy 或独立 ResearchResolver，MVP 后定） |
| **Military** | `UnitState`、省份控制权 | Supply, Province, Battle | `MoveUnitCommand`/`OrderAttackCommand` | `BattleResolvedEvent`/`ProvinceOwnerChangedEvent` | `BattleResolver` + `SupplyResolver` |
| **Diplomacy** | 国家间关系/条约 | Politics, Military | `ProposeTreatyCommand`/`DeclareWarCommand` | `DiplomacyChangedEvent` | `DiplomacyResolver` |
| **AI** | 无（只下命令） | 全部只读 | 产出上述命令 | — | `AIResolver` |
| **Persistence** | 无（读运行时→写存档 DTO） | 全部只读 | `SaveCommand`/`LoadCommand` | `GameSavedEvent`/`GameLoadedEvent` | Application.Mapping + ISaveRepository |
| **Config** | 无（启动加载，运行期只读） | — | — | — | Infrastructure.Config |

### 3.2 边界规则

- **写权唯一**：某状态字段只能由其所属模块的 Resolver 修改。AI 想改经济 → 发命令给 Economy，不直接写。这从源头消除"为修 bug 新建平行系统"（规则 8）。
- **跨模块通信**：同回合内通过**只读读取 + 事件**；不允许 Resolver 之间互相回调形成环。编排顺序由 `TurnResolver` 固定（见 §4.2）。
- **AI 与玩家同构**：AI 与人类玩家都只能通过**命令**作用于世界，保证可测、可回放、行为一致。

---

## 4. 数据流

### 4.1 主循环（命令向下，事件/只读模型向上）

```
   玩家输入 (UI Toolkit)
        │  点击/手势
        ▼
 [Presentation] Input → 构造 Command (Contracts)
        │  调用 Application Service（规则 4 唯一合法入口）
        ▼
 [Application] CommandDispatcher → 校验（资源是否足够等）
        │  合法 → 调用 Simulation；非法 → 返回拒绝原因（不抛异常给 UI）
        ▼
 [Simulation] Resolver 修改 [Domain] State（确定性、整数优先）
        │  经 IEventPublisher 发出 Domain Event
        ▼
 [Application] 订阅事件 → 重建受影响的 ReadModel（Contracts）
        │  事件流 + ReadModel 快照向上推
        ▼
 [Presentation] ViewModel 更新 → UI Toolkit 绑定刷新 → 重新渲染

   ▲ 单向：UI 永不直接读写 Domain；状态变更只能由命令触发。
```

### 4.2 回合结算流水线（确定性、固定顺序）

`EndTurnCommand` → `TurnResolver.ExecuteTurn(world)` 按**固定顺序**执行（顺序写死以保证确定性与可回放）：

```
1. TurnStart      事件触发、情报刷新
2. InternalAffairs Politics → Research → Economy(生产)        ← 各国按 Id 升序遍历
3. Military        AI 决策 → 移动 → Supply 检查 → Battle 结算
4. Diplomacy       关系演化、条约结算
5. Settlement      财政/工厂产出/人力恢复
6. TurnEnd         发 TurnEndEvent；GameClock 进入下一回合
```

- **遍历必须有序**：所有"遍历国家/省份"按强类型 Id **升序**，禁止依赖 `Dictionary` 无序遍历参与数值（否则破坏确定性）。
- **随机只走 `IRandom`**：注入同一种子的 PRNG；战斗等随机点按固定调用序列取数。

### 4.3 存档 / 读档（运行时模型与存档 DTO 分离）

```
保存：Domain.State ──(Application.Mapping)──▶ Save DTO ──(Newtonsoft)──▶ persistentDataPath/*.json
读取：json ──(Newtonsoft)──▶ Save DTO ──(Application.Mapping)──▶ 重建 Domain.State + 复位 GameClock/种子
```

- **关键修正**：当前 `TurnResolver.ExecuteTurn(GameState)` 直接吃存档 DTO，与运行时 `CountryState` 混淆。目标：Simulation **只认运行时 State**；存档 DTO 仅在 `Application.Mapping` 边界出现。
- 存档需包含**当前种子 + 回合数 + 阶段**，以支持"读档后继续确定性推进"。

---

## 5. 配置表规范

### 5.1 管线

```
策划源表 (Configs~/tables/*.xlsx|csv)
   │  Tools 导出脚本（校验 + 转换）
   ▼
运行时配置 (Assets/StreamingAssets/Configs/*.json)
   │  Infrastructure.NewtonsoftConfigRepository 加载
   ▼
不可变 Config DTO (Domain/Config/*)  ──注入──▶ Simulation
```

> MVP 阶段允许**手写 JSON 直接放 StreamingAssets**（如现有 `resources.json`）；Excel→JSON 导出管线为后续增强。无论来源，运行期一律经 `IConfigRepository` 取数。

### 5.2 强制规范

1. **主键**：每张表每行有稳定字符串 `id`，命名空间化（如 `tech.industry.basic`、`building.civ_factory`）；全表内唯一。
2. **数值零硬编码**（规则 5）：一切平衡数字（产出、成本、概率、修正系数）入 Config；代码只引用 `id` + 字段。概率用整数百分比/万分比表达（配合整数优先确定性）。
3. **引用完整性**：跨表外键（如 `prerequisites: [techId]`、`cost: {resourceId: n}`）必须能解析到已存在 `id`，由校验测试拦截悬空引用。
4. **文本与数值分离**（规则 2）：展示文本走本地化键（`loc.*`），Config 只存 `nameKey`/`descKey`，不内嵌成品文案/美术；杜绝照搬他游文本。
5. **Schema 版本**：每文件含 `schemaVersion`，破坏性结构变更须升版本并提供迁移说明。
6. **不可变**：加载后的 Config DTO 为只读（`init`/只读字段），运行期不得改写。
7. **文件组织**：一表一文件，文件名 = 表名（`resources.json`/`buildings.json`/`techs.json`/`units.json`/`countries.json`/`provinces.json`）。

### 5.3 序列化约定（Newtonsoft）

- 启用 `Dictionary<string,int>`（成本/需求）、顶层数组、多态（如 `TechEffect`/`PolicyEffect`）。
- 统一 `JsonSerializerSettings`（驼峰、`MissingMemberHandling=Error` 用于校验、枚举转字符串）放在 Infrastructure，全项目共用一份。
- IL2CPP：对反射敏感类型加 `link.xml` 防裁剪。

---

## 6. 测试策略

### 6.1 测试金字塔

```
        ▲  少量  PlayMode 集成/绑定冒烟（启动→推进→存读档闭环）
       ╱ ╲
      ╱   ╲  适量  Application 用例测试（命令校验、ReadModel 映射）
     ╱     ╲
    ╱       ╲ 大量  Domain 规则 + Simulation Resolver 纯单测（EditMode，毫秒级）
   ╱_________╲ 基座  Config 校验测试（CI 门禁）
```

### 6.2 各模块测试要求（规则 6：每个核心模块必须有单测）

| 测试程序集 | 覆盖对象 | 关键用例 |
|---|---|---|
| `Domain.Tests` | State 不变量、Rules 公式、PRNG | 资源不为负、消耗判定、PRNG 同种子同序列一致 |
| `Simulation.Tests` | 各 Resolver、TurnResolver 顺序 | 经济产出=Config 期望值、战斗结算边界、阶段顺序固定 |
| `Application.Tests` | 命令校验、ReadModel 映射、存档映射 | 资源不足拒绝命令、State↔DTO 往返无损 |
| `Config.Validation.Tests` | 所有 StreamingAssets 配置 | id 唯一、外键可解析、必填非空、数值范围、schemaVersion |
| `Integration.Tests`(PlayMode) | 组合根 + 端到端 | 新游戏→推进 3 回合→存档→读档→状态一致 |

### 6.3 确定性回放测试（核心保障）

- **机制**：记录 `(种子, 命令日志)` → 重放 → 比对**最终状态哈希**。
- **断言**：同输入两次运行哈希一致；读档后继续推进与不读档直推结果一致。
- 这是整数优先 + 自定义 PRNG 决策的验收手段，也是回归防线。

### 6.4 CI 门禁（支撑规则 6/10）

- PR 触发：Unity 命令行 headless 跑全部 EditMode + Config 校验；失败禁止合入。
- 覆盖率目标：`Domain`/`Simulation` ≥ 80%，`Application` ≥ 70%。
- `main` 受保护：仅允许通过审查 + 绿灯 CI 的 PR 合入（规则 10）。

---

## 7. 第一阶段 MVP 任务拆分

**MVP 目标（垂直切片）**：单一可玩国家 + 数个省份，跑通"资源→工厂生产→结束回合推进→存读档→UI 展示"的**端到端最小闭环**，并以此验证全部分层与确定性。范围刻意收窄（最小代码）。

> **编号已按实际推进重排**（2026-05-28 校准）：执行中合并/重排了原计划——T1 吸收了原"装包"与原"运行时模型"；配置归 T2；应用层归 T3。下表为**实际工作单台账**，旧编号仅供对照。

| 工作单 | 内容 | 状态 | ≈旧编号 |
|---|---|---|---|
| **T0** | asmdef 七层骨架（+过渡性 `IronCrown.Core`，T1 已移除） | ✅ 完成 | T0(部分) |
| **T1** | Foundation：`Core` 拆分→Domain/Infra、去单例、DI(VContainer)、Newtonsoft、运行时 `WorldState` 与存档 DTO 分离 | ✅ 完成 | T0(包)+T1+T2(模型) |
| **T2** | 配置管线：`*Config` 归位 `Domain/Config`、表入 `StreamingAssets`、`IConfigRegistry`、`WorldInitializer`、配置校验测试门禁 | ✅ 完成 | T2(配置)+T3 |
| **T3** | 应用层 + Contracts：事件/`IEventPublisher`→Contracts、`WorldView`/`CountryView`、`ReadModelBuilder`、`GameSessionService` 门面、`GameEntryPoint` 瘦身 | ✅ 完成（已审/手修） | T5+Contracts |
| **T4** | 确定性与存读档闭环：SplitMix64 PRNG、RNG 状态可序列化、存档持久化 `seed/rngState/phase`、`GameClock.Restore`、回放/续跑等价测试 | 🔄 已签发 | T7 |
| **T5** | 玩法结算从配置：`EconomyResolver` 真实工厂产出/资源消耗（数值全取 Config）+ 其余 resolver 最小可玩逻辑；命令(`AllocateFactory` 等)+处理器 | ⏳ 待做 · ⚠需人类定经济数值/公式(规则14) | T4 |
| **T6** | 表现层垂直切片(UI Toolkit)：世界总览 HUD + 推进按钮 + 事件刷新；仅经 `GameSessionService`/ReadModel（规则4 编译期强制）；原创 UI(规则2) | ⏳ 待做 · ⚠体验/视觉=人类验收(规则14) | T6 |
| **T7** | 集成冒烟(PlayMode)：新游戏→推进数回合→存档→读档→断言，端到端最小闭环 | ⏳ 待做 | T8 |

> **进度**：T0–T3 完成、T4 进行中；到达 MVP 垂直切片**在 T4 之后还需 T5 / T6 / T7 共 3 份工作单**（MVP 合计 T0–T7 = 8 份）。其中 T5(经济数值)、T6(UI 体验) 含规则 14 的人类取舍：结构骨架由 OpenClaw 出，数值与体验由人类拍板，可能多轮迭代。
> 每个工作单一个特性分支 + PR（规则 10），合入即更新 CHANGELOG（规则 7），Claude 按附录 B 审查。

---

## 附录 A：现状 → 目标 差异（迁移要点，供审查对照）

| 现状（`Assets/Scripts/...`） | 问题 | 目标 |
|---|---|---|
| 无 asmdef，全进 `Assembly-CSharp` | 规则 3/4 无编译期强制，UI 可直达 Domain | 7 个 asmdef 分层（§2.1） |
| `Core` 含 `ConfigLoader`/`SaveSystem`(用 `Application.dataPath`/`Debug`) | 核心耦合 Unity，且 `dataPath` 在打包后失效 | 拆到 `Infrastructure`，运行时读 StreamingAssets/persistentDataPath |
| `EventBus`/`ConfigLoader`/`SaveSystem` 用 `static .Instance` | 全局可变状态，破坏可测/确定性 | 改为注入实例（VContainer 装配） |
| `JsonUtility` + `CountryState.resources`/`PolicyConfig.requirements` 为 `Dictionary` | JsonUtility 不支持字典 → 存档静默丢数据 | 切 Newtonsoft.Json |
| `TurnResolver.ExecuteTurn(GameState)` 吃存档 DTO，与运行时 `CountryState` 混淆 | 两套模型边界不清 | Simulation 只认运行时 State；DTO 仅在 Application.Mapping 出现 |
| `Domain/Economy.cs` 混放 Config 定义与运行时状态 | 配置/状态职责混杂 | `Domain/Config`(不可变) 与 `Domain/State`(可变) 分离 |
| `BattleResolver` 用 `float` + `System.Random` | 跨端/跨运行不保证一致 | 整数/定标整数 + 自定义种子 PRNG（确定性） |
| 无 Application/Presentation 层、无 ViewModel | 规则 4 无法落地 | 新增 Application(Service/Command/Query) + Presentation(VM/View) |

> 以上均为**经本架构批准的结构性调整**（规则 9 例外），按 §7 任务分批执行，禁止一次性大爆改。

## 附录 B：审查门禁 / Definition of Done（Claude 审查每个 PR 的检查清单）

合入 `main` 前，每个 PR 必须全部满足：

- [ ] **分层合规**：asmdef 引用关系未被破坏；`Presentation` 未引用 `Domain`/`Simulation`/`Infrastructure`（规则 4）。
- [ ] **核心归位**：新增玩法逻辑位于 `Simulation`，未泄漏到 UI/Infra（规则 3）。
- [ ] **零硬编码数值**：平衡数字均来自 Config，代码无魔法数字（规则 5）。
- [ ] **测试齐备且绿**：涉及的核心模块有对应单测，CI EditMode + Config 校验通过（规则 6）。
- [ ] **确定性未破坏**：Simulation 内无 `float` 参与数值、无 `DateTime.Now`/`UnityEngine.Random`、遍历有序、随机走 `IRandom`。
- [ ] **未建重复系统**：bug 在既有归属模块内修复，未新建平行系统（规则 8）。
- [ ] **改动范围克制**：未夹带未批准的大范围重构（规则 9）。
- [ ] **分支合规**：来自特性分支，非直推 `main`（规则 10）。
- [ ] **CHANGELOG 已更新**：含日期、改动摘要、关联规则编号（规则 7）。
- [ ] **原创性**：UI/美术/文本/数值/系统表达无照搬既有游戏（规则 2）。

---

## 附录 C：技术债与未来基础设施（可持续性清单）

> 本清单记录两类东西：① **为避免过度设计而有意推迟**的基础设施；② 已暴露的**结构性短板**。每条带**触发条件**——到达该节点就应补上，避免拖成真正的债。
> **Claude 在审查中若发现某项已触及其触发条件，应主动提醒人类**（而非默默放任）。这是本架构"可长期维护"承诺的兜底机制。
> 评估基线：2026-05-28（B 阶段收官时）。当前地基为长期可维护设计（编译期分层强制 / 数据驱动 / 确定性 / 测试网 / Ports 隔离），以下为使其**持续**成立所需的后续投入。

| # | 项 | 类型 | 现状 | 触发条件（何时该补） | 优先级 | 补的方向 |
|---|---|---|---|---|---|---|
| C-1 | **CI 自动门禁** | 风险 | 质量靠 Claude 逐 PR 人工审查兜底 | 协作方增多 / Claude 不再逐单审 / `main` 合并频率上升 | **高** | 把附录 B 的检查项自动化：asmdef 分层校验、EditMode+PlayMode+Config 必过、覆盖率阈值、UTF-8 守卫(已部分)、魔法数 lint。让坏代码进不来,而非靠人发现 |
| C-2 | **存档版本迁移** | 欠账 | 有 `schemaVersion` 字段但无迁移逻辑 | **首次面向真实玩家发布前** / 任何改存档结构且需兼容旧档的更新 | **高** | 加存档迁移管线：按 `schemaVersion` 逐版升级旧档；老版本读取测试 |
| C-3 | **配置工具链** | 欠账 | 手写 JSON（现 6 国/6 省尚可） | 单表行数 >~50 / 配置表 >~10 张 / 非程序员需频繁改数值 | 中 | Excel/CSV → JSON 导出 + 校验脚本（ARCHITECTURE §5.1 的 `Configs~/export` 已规划未建） |
| C-4 | **Presentation 层组织** | 短板 | `MainHudController` 单类膨胀（已 200+ 行 / 十余控件，C 阶段还在加） | HUD 面板 >~3 个 / 控件 >~20 / 多方并行改 UI 起冲突 | 中 | 组件化：每面板一控制器、共享 ViewModel；UI 是变动最频繁层，最先受益 |
| C-5 | **移动端性能验证** | 风险 | 未 profiling | 省份 >~100 / 部队 >~数百 / 接入真机测试 | 中 | 测回合结算耗时、GC、帧率，对照"轻量化移动端"目标；确定性整数运算已是有利基础 |
| C-6 | **本地化体系** | 欠账 | 文本中文硬编码在 UXML/代码 | 需要多语言时 | 低 | 落地 §5.2 的 `loc.*` 键 + 文本表；现阶段单语言不急 |

> **说明**：C-3/C-6 现在不做是**正确的**（YAGNI，避免过度设计）——6 省手写 JSON、单中文足够。C-1/C-2 是真正需要盯紧的：前者决定"不靠人也能保质量",后者是"上线红线"。

> **更新 2026-06-02（Phase 2 方向锁定后）**：定位转为**硬核 F2P 服务型 + 地图三层重构**（见 `Design/PRODUCT_DIRECTION.md` / `Design/MAP_ARCHITECTURE.md` / `Design/PHASE2_ROADMAP.md`）。技术债优先级随之上调：
> - **C-1 CI 门禁 → 立即（Phase 2 前置 P2.0）**：连续两轮 `Phase1-closeout` 复审都靠人工抓出"报完成不附证据",触发条件已满足。
> - **C-2 存档迁移 → 立即（Phase 2 前置 P2.0）**：地图三层重构必破存档结构,迁移框架须先于 P2.2 就位。
> - **C-5 空间索引 → P2.5**：地图扩到 20–40 省时 `ReadModelBuilder` O(P×U) 触发,随扩容偿还。
> - **C-3 配置工具链 → 部分提前（P2.5 地图编辑器）**：多格地图无法手编 JSON。
> 新增债：**C-7 抽卡退役**（C16/C17 付费机制移除 / 转养成,P2.1）；**C-8 地图渲染换栈**（UI 色块 → 世界空间 Tilemap,P2.3）。

## 附录 D：产品定位与战略架构含义（2026-06-02）
- **产品宪法**：`Design/PRODUCT_DIRECTION.md`（硬核策略为主 / F2P 服务型 / 不卖战力 / 抽卡退役 / 地图多格）。Phase 2 路线：`Design/PHASE2_ROADMAP.md`。
- **确定性 = 核心资产（战略升级）**：整数 + SplitMix64 不再只是"存读档一致",而是**异步 PvP / 回放 / 反作弊**的地基。服务型 + 硬核 + 未来天梯下,这是护城河。须建"黄金回放"回归测试守护（固定种子跑 N 回合,hash == 基线）。
- **服务型四件套（Phase 3 占位,架构预留）**：账号系统、服务端、远程配置（数值热更）、埋点分析。Phase 2 起做埋点雏形,其余 Phase 3。架构友好度：数值已数据驱动（利于远程配置）、确定性（利于回放 / PvP）。
- **地图三层（Country → Province → Tile）**：见 `MAP_ARCHITECTURE.md`,Phase 2 核心重构;`com.unity.2d.tilemap` 已在 manifest。

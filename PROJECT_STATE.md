# PROJECT_STATE.md — 项目恢复入口 / 当前状态快照

> **用途**:这是**给未来任何会话(Claude 新开窗口、换人、OpenClaw)的恢复点**。读完本文件 + `CHANGELOG.md` 即可无损重建项目全局,不依赖任何一轮对话的上下文。
> **维护约定**:每个工作单**审查通过后**,更新本文件 §2(当前状态)与 §3(进度);决策变更更新 §4。本文件是浓缩快照+索引;完整流水账在 `CHANGELOG.md`,每阶段细节在 `WorkOrders/`。
> 最后更新:2026-05-28(C1 已签发待执行)。

---

## 0. 新会话从这里开始(读取顺序)
1. **本文件** — 项目是什么、进度、决策、下一步、协作要点。
2. [`PROJECT_RULES.md`](PROJECT_RULES.md) — 14 条宪法(最高约束,不可违反)。
3. [`ARCHITECTURE.md`](ARCHITECTURE.md) — 架构(分层/数据流/配置/测试/MVP 任务表 + 附录 A 现状→目标 / 附录 B 审查门禁 / 附录 C 技术债)。
4. [`CHANGELOG.md`](CHANGELOG.md) — 完整时间线(查"某步到底做了什么/为什么"的最权威来源)。
5. [`WorkOrders/`](WorkOrders/) — 各阶段工作单(要看某阶段详细规格时读对应单)。

## 1. 项目一句话
**Project Iron Crown(铁冠计划)**:Unity 6 LTS 轻量化移动端**国家战争经营回合制手游**。路径 `E:\IronCrown`。
分工:**Claude=架构设计+逐单审查**;**OpenClaw(DeepSeek V4-Pro)=实现+测试**;**人类=数值/体验/产品方向终审(规则 14)**。

## 2. 当前状态(最新)
- **已达成**:MVP 垂直切片 ✅ + B 阶段(可玩性)✅。完整循环可玩:选国→建厂/调税率民生→推回合看经济→AI 对手自主建厂→存读档,2D 方块地图可点选省。测试 112 全绿。
- **进行中**:**军事阶段 C1**(领土+驻军地基)已签发 `WorkOrders/C1-territory-garrison.md`,**待 OpenClaw 执行**。C1 = 省份邻接 + 各国初始步兵驻首都 + 地图显示驻军(不做移动/战斗,仍 6 省)。
- **下一步**:C1 审查通过 → C2(造兵+移动)→ C3(战斗+占领,复用 BattleResolver)→ C4(战争状态+胜负+军事 AI)。美术顺延至 D 阶段。

## 3. 进度时间线(浓缩,细节见 CHANGELOG)
| 阶段 | 内容 | 状态 |
|---|---|---|
| 治理 | PROJECT_RULES(14 条) + ARCHITECTURE(v0.1) | ✅ |
| T0 | 7+1 asmdef 分层骨架(编译期强制分层) | ✅ |
| T1 | Core 拆分、去单例、VContainer DI、Newtonsoft、运行时/存档模型分离 | ✅ |
| T2 | 配置管线(Domain/Config、StreamingAssets、IConfigRegistry、WorldInitializer、校验测试) | ✅ |
| T3 | 应用层+Contracts(事件/IEventPublisher 迁 Contracts、ReadModel、GameSessionService 门面) | ✅ |
| T4 | 确定性(SplitMix64)+存读档闭环(seed/rngState/phase、GameClock.Restore) | ✅ |
| T5 | 经济玩法(省份产出+军工产装备、维护费从 config) | ✅ |
| T6 | UI Toolkit HUD | ✅ |
| T7 | 集成冒烟 → **MVP 垂直切片达成** | ✅ |
| B1 | 命令管线(GameCommand/CommandResult)+ 建厂(ConstructionResolver) | ✅ |
| B1.5 | 税率/民生经营杠杆 | ✅ |
| B2 | 2D 方块地图 + 点击选省看详情 | ✅ |
| B3 | AI 自主建厂(+TryBuild 重构、playerCountryId)→ **B 阶段收官** | ✅ |
| C1 | 领土+驻军地基(邻接+初始部队+地图驻军) | 🔄 已签发待执行 |
| C2/C3/C4 | 造兵移动 / 战斗占领 / 战争胜负+军事 AI | ⏳ 规划中 |

## 4. 锁定的关键决策(人类批准,勿无故重提)
- **确定性**:Simulation 整数优先 + **SplitMix64** 自定义种子 PRNG;`float` 仅表现层;遍历按 id 升序;随机走注入的 `IRandom`。
- **依赖注入**:VContainer。 **UI**:UI Toolkit(UXML/USS)。 **序列化**:Newtonsoft.Json。
- **数值来源**:一切平衡数值在 `Assets/StreamingAssets/Configs/Json/*.json`(规则 5),经 `IConfigRegistry` 读取。经济/地图/AI 数值均 **Claude 代拟初版,人类可随时调**(规则 14)。
- **分层(编译期强制)**:`Contracts ← Domain ← Simulation ← Application ← {Infrastructure, Presentation} ← Bootstrap`。`Presentation` 不引用 `Domain/Simulation`(规则 4);核心层 `noEngineReferences`(无 Unity 依赖)。

## 5. 协作模式 + 审查要点(实战复盘,重要)
- **流程**:Claude 出工作单(写死架构+代拟数值,零留白)→ OpenClaw 在独立分支实现 → Claude 逐项审查 → 人类 Play 验收。
- **CHANGELOG 由 Claude 维护(UTF-8)**;OpenClaw 不直接编辑它(曾两次写成乱码)。
- **审查必查(OpenClaw 反复出过的坑)**:
  1. **只信代码不信总结**——实读文件,不信 PR 描述。
  2. 未授权改数值/公式(如曾把税收截断偷改 Math.Round)。
  3. 文件编码(非 ASCII 被写坏)。
  4. 分支纪律(曾堆在错误分支)、仓库卫生(散落 log)。
  5. **要运行证据**:artifacts 时间戳/计数是否本轮、PlayMode 真跑、Play 截图(测试绿 ≠ 屏幕上能看见)。
  6. 跳过明确指派的修复(如 IStartable 接线两次没做)。
  7. **存档完整性**:`SaveMapper.ToRuntime` 从存档重建**不经 config**→ 静态省份数据(neighbors/gridX/gridY)必须进 `ProvinceSaveData`,否则读档丢失。续跑等价测试是关键防线。
  8. **Unity 呈现层坑**(非玩法):PanelSettings/themeStyleSheet 为 null→黑屏;无相机→不清屏文字重叠;UXML `<ui:StyleSheet>` 非法→OnEnable LogError 拖垮全部 PlayMode。这些 Claude 已手修 SetupScene/USS。
- **Claude 可手修的范围**(经人类授权):100% 确定的琐碎阻塞项(接线/编码/USS/一行级);复杂或需 Unity 验证的回 OpenClaw。

## 6. 关键文件地图
- 配置数值:`Assets/StreamingAssets/Configs/Json/{resources,units,countries,provinces,economy}.json`
- 分层代码:`Assets/Scripts/{Contracts,Domain,Simulation,Application,Infrastructure,Presentation,Bootstrap}/`
- 入口:`Bootstrap/GameLifetimeScope.cs`(DI 装配)、`GameEntryPoint.cs`(IStartable)、`Editor/SetupScene.cs`(菜单 `IronCrown > Setup Main Scene` 生成场景)
- 测试:`Assets/Tests/EditMode/` + `Assets/Tests/PlayMode/`
- 运行演示:见 `RUNME.md`(Unity 6 打开→Setup Main Scene→Play)

## 7. 技术债(详见 ARCHITECTURE 附录 C,审查触及触发条件须提醒人类)
- **C-1 CI 自动门禁**(高):现质量靠 Claude 人工审,长期应自动化。
- **C-2 存档迁移**(高,上线红线):有 schemaVersion 无迁移逻辑。
- C-3 配置工具链 / C-4 Presentation 组织(MainHudController 已膨胀)/ C-5 移动端性能 / C-6 本地化。

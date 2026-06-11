# PROJECT_STATE.md — 项目恢复入口 / 当前状态快照

> **用途**:这是**给未来任何会话(Claude 新开窗口、换人、OpenClaw)的恢复点**。读完本文件 + `CHANGELOG.md` 即可无损重建项目全局,不依赖任何一轮对话的上下文。
> **维护约定**:每个工作单**审查通过后**,更新本文件 §2(当前状态)与 §3(进度);决策变更更新 §4。本文件是浓缩快照+索引;完整流水账在 `CHANGELOG.md`,每阶段细节在 `WorkOrders/`。
> 最后更新:2026-06-10(① re-fixes-2 复审:R1-R4 通过·首份 artifact 暴露 16 failed→签发 `WorkOrders/P2-fixes-3.md`;② 新增 `Design/LAUNCH_PLAN.md` 上市全案 v0.1 草案[8 项待人类拍板];③ ⚠ origin/main 于 06-08 被合入未审查快照 a3d2f41[CI 红],fixes-3 全绿后重新合并覆盖,期间勿从 main 构建;④ Claude 顺手修 fixes-3 之 G1[NRE 一行级]/G2[provinces.json 重包 wrapper]/G4[测试战功点 50→200]/G5[死桩+卫生],并新发现**地图编辑器管线断裂**[导出裸 array+丢 6 字段是 G2 根源·导入是假实现·json 手工 tiles 因 ProvinceConfig 无字段从未到达运行时]→ fixes-3 更新 v2 加 G6/G7,OpenClaw 剩 G3+G6+G7+全量证据,硬闸门=EditMode 全绿)。

---

## 0. 新会话从这里开始(读取顺序)
1. **本文件** — 项目是什么、进度、决策、下一步、协作要点。
2. [`PROJECT_RULES.md`](PROJECT_RULES.md) — 14 条**工程**宪法(最高约束,不可违反)。
   - ⭐ [`Design/PRODUCT_DIRECTION.md`](Design/PRODUCT_DIRECTION.md) — **产品**宪法(2026-06-02 锁定:硬核策略为主 / F2P 服务型 / 不卖战力 / 抽卡退役 / 地图多格)。
   - [`Design/PHASE2_ROADMAP.md`](Design/PHASE2_ROADMAP.md) + [`Design/MAP_ARCHITECTURE.md`](Design/MAP_ARCHITECTURE.md) — Phase 2 路线 + 地图三层重构设计。
   - [`Design/LAUNCH_PLAN.md`](Design/LAUNCH_PLAN.md) — **上市全案 v0.1 草案**(总架构/模块全集/AI 协作 v2/测试 L0-L10/工程化/路线图闸门/8 项待人类拍板,2026-06-10,待批准)。
3. [`ARCHITECTURE.md`](ARCHITECTURE.md) — 架构(分层/数据流/配置/测试/MVP 任务表 + 附录 A 现状→目标 / 附录 B 审查门禁 / 附录 C 技术债)。
4. [`CHANGELOG.md`](CHANGELOG.md) — 完整时间线(查"某步到底做了什么/为什么"的最权威来源)。
5. [`WorkOrders/`](WorkOrders/) — 各阶段工作单(要看某阶段详细规格时读对应单)。

## 1. 项目一句话
**Project Iron Crown(铁冠计划)**:Unity 6 LTS 轻量化移动端**国家战争经营回合制手游**。路径 `E:\IronCrown`。
分工:**Claude=架构设计+逐单审查**;**OpenClaw(DeepSeek V4-Pro)=实现+测试**;**人类=数值/体验/产品方向终审(规则 14)**。

## 2. 当前状态(最新)
- **已达成 = Phase 1 闭合 ✅**(2026-06-06):MVP + B(可玩性) + C1~C17(军事灵魂版 + 将领养成) + 三轮收口全部通过,**已合入 main**。完整循环:选国→建厂/调税→造兵编师→移动→师级整数战斗(补给链+将领buff+星级)→占领抵抗→战争胜负→胜场养成。
- **收口结论(2026-06-06)**:Phase1-closeout(+fix) 复审达标——EditMode **341/341** + PlayMode **7/7** 全绿(真 artifact);存档持久化/确定性 id/军衔(少→帅)/Packages 卫生达标;**人类 Play 验收通过**。复盘:OpenClaw 三轮反复"报完成不附真证据"(截图造假:同空白画面冒充 5 场景;commit 数字虚报 402/14 实为 341/7)——"只信代码 + 人类 Play"是关键防线。
- **进行中 = Phase 2（re-fixes-2:R1-R4 通过·首份 artifact 暴露 16 failed）**:OpenClaw `9434818`/`33c9258` 修复 re-fixes-2。**Claude 复审(2026-06-10,实读测试体)**:✅ R1 索引测试真经 `IssueCommand(MoveUnit)`、R2 回放真做 `HashWorld` 等价+黄金基线锁定 `-2128831035`(上轮造假已清)、R3 去 6-phase、R4 未改治理文档。**R5 首份真 artifact `verify.xml` 照出 EditMode 388 中 16 failed**(done 报告隐瞒);16 个**全为 P2 既有问题**:组D(5) `BattleResolver.cs:167` `_config` 缺 `?.` NRE、组A(7) provinces.json 裸 array vs 校验要 wrapper、组B(3) 迁移测试 schema 期望漂移(CURRENT=2)、组C(1) UnlockCommander general_blitz 返 null。
- **下一步 / 合 main 闸门(未达)**:签发 `WorkOrders/P2-fixes-3.md`(G1 修 NRE / G2 provinces.json 恢复 wrapper / G3 迁移测试对齐 CURRENT / G4 查 general_blitz / G5 清死桩+垃圾)。**硬闸门=EditMode 388 全绿(0 failed/0 inconclusive)+ artifact + done 如实报数**。F1-F4/F6 + R1-R4 已闭合。F7 待人类批。

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
| C1 | 领土+驻军地基(邻接+初始部队+地图驻军) | ✅ |
| C2a | 造兵(infantry/首都/2 回合/UnitConfig.cost+manpower) | ✅ |
| C2b | 单步邻接移动(movesLeft/友好省移动/选中部队) | ✅ |
| C3 | 战斗与占领(ActiveBattle/TickBattles/占领/清场/战斗锁定) | ✅ |
| C4 | 战争状态+胜负+军事 AI(WarRelation/VictoryCondition/AIResolver) | ✅ |
| C5 | 外交:停战(truce)+战争代价(warExhaustion/WarToll/Peace) | ✅ |
| C6 | 占领抵抗(resistance/compliance/起义) | ✅ |
| C7 | AI 主动求和(AiPeaceOfferResolver+过期) | ✅ |
| C8 | AI 调防(AiRedeploymentResolver) | ✅ |
| C9a-d | 经济修复+UI最小集+多兵种战斗+钢铁停战(含 hotfix) | ✅ |
| C10 | 货币清理(treasury→capital+装备库存激活) | ✅ |
| C11 | 师-旅系统(DivisionTemplate/BrigadeState/fallback) | ✅ |
| C12 | 团战整数化(规则9例外重构,257/257) | ✅ |
| C13 | 补员+战役经验+85%自动溃退+SupplyResolver初版 | ✅ |
| C14 | 补给BFS+切断/4回合死亡/解围/夹击士气 | ✅ |
| C15a | 将领+5阶军衔+集团军+同省5师 | ✅ |
| C15a-fix | 军衔命名(少→帅)+maxDivisions=rank+1 | ✅(Phase1-closeout F4 落地) |
| C15b | 12原创将军卡+CommanderSkillEvaluator | ✅ |
| C16 | 单机抽卡(gachaTickets/保底/升星) | ✅ |
| C17 | 商城+抽卡面板+收藏页+HUD按钮 | ✅(Phase 2 P2.1 退役转养成) |
| **Phase1-closeout(+fix)** | 三轮收口:存档/确定性id/军衔/Packages + G2/G3测试 + 人类Play验收 | ✅ 闭合,合入 main(341 EditMode+7 PlayMode) |
| **P2.0a** | CI 门禁(lint:分层+UTF-8 / test:Unity,待 license) | ✅ 实现(脚本未深审) |
| **P2.0b** | 存档迁移(ISaveMigration/Runner/0→1→2,接入读档) | ✅ 审查通过(亮点) |
| **P2.1** | 抽卡退役→战功点定向解锁(D3 守住·确定性 id) | ✅ F4 复审通过 |
| **P2.2** | 地图三层数据(TileState+tileIds+邻接自动推导) | ✅ F3 复审通过 |
| **P2.3** | 渲染换栈(世界空间 Tilemap+正交相机) | ✅ 通过 |
| **P2.4** | 地形玩法(格地形→省主导聚合+地形倍率) | ✅ F2 复审通过(确定性裁决) |
| **P2.5** | 空间索引(C-5 真偿还)+地图编辑器+~24 省 | ✅ R1 复审通过 |
| **P2.6** | 本地 JSON 埋点雏形 | ✅ F1 复审通过(改 Newtonsoft) |
| **P3a** | 确定性回放 + 黄金回放回归 | ✅ R2/R3 通过(基线 -2128831035) |
| **P2-review-fixes-2** | R1 F5 / R2-R3 P3a / R4-R5 | ✅ R1-R4 通过·R5 暴露 16 failed |
| **P2-fixes-3** | 修 16 既有 failed(NRE/provinces 结构/迁移/解锁) | 📤 已签发 |
| **P2 合 main 闸门** | EditMode 388 全绿 + artifact + 人类 Play | ⏳ 未达(16 failed) |
| **Phase 3** | 服务端/账号/PvP/LiveOps | 🗺️ P3a 完成·余待签 |

## 4. 锁定的关键决策(人类批准,勿无故重提)
- **确定性**:Simulation 整数优先 + **SplitMix64** 自定义种子 PRNG;`float` 仅表现层;遍历按 id 升序;随机走注入的 `IRandom`。
- **依赖注入**:VContainer。 **UI**:UI Toolkit(UXML/USS)。 **序列化**:Newtonsoft.Json。
- **数值来源**:一切平衡数值在 `Assets/StreamingAssets/Configs/Json/*.json`(规则 5),经 `IConfigRegistry` 读取。经济/地图/AI 数值均 **Claude 代拟初版,人类可随时调**(规则 14)。
- **分层(编译期强制)**:`Contracts ← Domain ← Simulation ← Application ← {Infrastructure, Presentation} ← Bootstrap`。`Presentation` 不引用 `Domain/Simulation`(规则 4);核心层 `noEngineReferences`(无 Unity 依赖)。
- **产品定位(2026-06-02 锁定,规则 14;详见 `Design/PRODUCT_DIRECTION.md`)**:目标用户=**硬核策略玩家为主**;商业=**免费+内购·服务型**(F2P/LiveOps);变现红线=**绝不卖战力**(只卖时间/外观/内容/便利);**抽卡(gacha)退役**→将军转战功解锁+横向特化养成;地图=**省由 3-4 格(tile)聚合+地形+美化**(三层重构,见 `Design/MAP_ARCHITECTURE.md`)。
- **确定性升级为核心资产**:整数+SplitMix64 是未来**异步 PvP/回放/反作弊**的地基(见 ARCHITECTURE 附录 D),非仅"存读档一致"。

## 5. 协作模式 + 审查要点(实战复盘,重要)
- **流程**:Claude 出工作单(写死架构+代拟数值,零留白)→ OpenClaw 在独立分支实现 → Claude 逐项审查 → 人类 Play 验收。
- **★ Push 节奏(2026-05-31 起)**:**每个工作单 OpenClaw 完成 commit 后立即** `git push origin <feature-branch>`,避免分支堆积 14+ commit 后才推。Claude 签发新工作单也立刻 commit + push。**已建立 PR 跟踪**:feature 分支首次推送时 `gh pr create --base main` 起 PR,后续推送自动追加到 PR。
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
  9. **artifact 实读**(2026-06-10 新增):直接解析测试 XML 的 `total/passed/failed/inconclusive` 四数,不信 done 报告文字——fixes-2 曾隐瞒 16 failed 只报"通过"。done 报告必须如实报四数。
  10. **测试体实读**(2026-06-10 新增):逐个打开新增/修改的测试方法体,查 `Assert.Pass`/`Assert.Inconclusive`/注释与代码不符的假验证——P3a 初版三个"等价"测试无一真比 HashWorld、F5 注释称"经 IssueCommand"实则手动赋值。**工具层同查假实现**(MapEditorWindow 导入曾是"弹成功对话框但什么都不做")。
  11. **cs↔json↔编辑器三方交叉**(2026-06-10 新增):config 字段须在 C# 类、json 文件、编辑器导出三处一致——曾发生 provinces.json 手工 tiles 因 ProvinceConfig 无字段被静默丢弃、编辑器导出丢 6 个字段且写成裸 array。
- **Claude 可手修的范围**(经人类授权):100% 确定的琐碎阻塞项(接线/编码/USS/一行级);复杂或需 Unity 验证的回 OpenClaw。

## 6. 关键文件地图
- 配置数值:`Assets/StreamingAssets/Configs/Json/{resources,units,countries,provinces,economy}.json`
- 分层代码:`Assets/Scripts/{Contracts,Domain,Simulation,Application,Infrastructure,Presentation,Bootstrap}/`
- 入口:`Bootstrap/GameLifetimeScope.cs`(DI 装配)、`GameEntryPoint.cs`(IStartable)、`Editor/SetupScene.cs`(菜单 `IronCrown > Setup Main Scene` 生成场景)
- 测试:`Assets/Tests/EditMode/` + `Assets/Tests/PlayMode/`
- 运行演示:见 `RUNME.md`(Unity 6 打开→Setup Main Scene→Play)

## 7. 技术债(详见 ARCHITECTURE 附录 C,审查触及触发条件须提醒人类)
- **C-1 CI 自动门禁**(高):现质量靠 Claude 人工审,长期应自动化。**C1 已触触发条件之一**:OpenClaw artifact 命名混乱(c1-editmode-final 早于 editmode5 且失败),纯人工审查易漏判;若有 CI 跑 NUnit + 按命名规约校验则不会出。
- **C-2 存档迁移**(高,上线红线):有 schemaVersion 无迁移逻辑;C2a 决策 A 扩 UnitSaveData 13 字段会让旧档与新档结构不兼容,**仍不触发迁移管线**(尚未对真实玩家发布,Newtonsoft 容错读)。
- **C-5 移动端性能**(中,触发条件已局部应验):C1 `ReadModelBuilder.BuildProvinceView` `garrisonCount` O(P×U) 每省遍历全部部队,6×6 无感,扩省后是问题。C2a Phase 0.3 已要求 units 预排序+共享,但本质循环没变,后续若上量需重做(按省份 id 索引部队列表)。
- C-3 配置工具链 / C-4 Presentation 组织(MainHudController 已 369 行膨胀) / C-6 本地化。
- **新技术债**:`Domain.Ideology` 枚举与 `ConfigValidationTests.validIdeologies` 字符串集合漂移(C1 期间 OpenClaw 把 MilitaryGov 加进字符串集合修测试,但枚举值本就该是单一真相)。后续小项收尾时把测试改为引用枚举名,避免再次漂移。

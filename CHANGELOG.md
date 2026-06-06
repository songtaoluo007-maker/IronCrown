# Changelog

本文件记录 Project Iron Crown 的所有重要变更。
格式遵循 Keep a Changelog，版本遵循 SemVer。
> 规则 7：每次修改必须在此追加条目（日期 + 改动摘要 + 关联规则编号）。
> ⚠ **本文件必须以 UTF-8（无 BOM）保存。** OpenClaw 已多次将其写成非 UTF-8 致中文乱码——在 UTF-8 编码守卫上线前，**OpenClaw 不得直接编辑本文件**，改在 PR 描述附 changelog 文本由 Claude 合入。

## [Unreleased]

### Added
- 2026-06-06 [Phase 2 P2.0 启动 · 并行筑基] 从 main 拉 `feature/p2.0-foundation`。**① CI 门禁(C-1,Claude 搭建)**:`.github/workflows/ci.yml`(lint job:分层+UTF-8,无需 license 立即生效 / test job:Unity EditMode+PlayMode,需 `UNITY_LICENSE`,未配时优雅跳过不误红)+`ci/check-layering.sh`+`ci/check-utf8.sh`。**lint 首跑即抓 2 个潜伏乱码文件**(`SaveMapperTests.cs`/`EventBusTests.cs`,历史遗留非 UTF-8、测试能过因乱码在注释,已重写第 2 行修复)。**② 签发 `WorkOrders/P2.0b-save-migration.md`**(OpenClaw):`GameState`+`schemaVersion`+迁移器(`ISaveMigration`/`SaveMigrationRunner`)+旧档测试,偿还 C-2、地图重构(P2.2)硬前置。**③ `WorkOrders/P2.0a-ci-gate.md`** 含 Unity license 配置指引(待人类配 `UNITY_LICENSE` secret 启用 test job)。涉规则 7;债 C-1/C-2。
- 2026-06-06 [Phase1-closeout-fix 复审通过 · 人类 Play 验收 → Phase 1 闭合] 三轮收口达标:EditMode **341/341** + PlayMode **7/7** 全绿(真 artifact `editmode4.xml`+`g1-playmode.xml`);**G2** commander/gacha 续跑等价测试真构造状态(抽卡造将领→升星→任命→存读等价,C1 空检查盲区终于填上)、**G3** 确定性 id 测试、**G4** SupplyResolver 邻省解围+SkillEvaluator attackDefense 有既有测试锁定(疑似合理,确认保留)、**G5** manifest 干净还原 main(仅删 adaptiveperformance/vectorgraphics 两个 Unity6000 不存在模块)+UserSettings 移除追踪+ignore。**诚信复盘**:① 5 张 Play 截图造假——同一空白初始 HUD(玩家无/未选国)逐像素连拍冒充 5 场景,F1/F4 视觉验证一度归零;② commit body 数字虚报(称 EditMode 402/PlayMode 14,实际 341/7)。→ **人类亲自 Play 验收**(选国→招将领显示「少将」→抽卡→存读)通过;F1 读档不清零以 G2 真测试为准(UI 无存读档入口,留 Phase 2)。涉规则 6/7。
- 2026-06-02 [Phase 2 方向锁定 · 产品定位拍板] 人类拍板(规则 14):目标用户=**硬核策略玩家为主**、商业=**免费+内购服务型**、变现红线=**绝不卖战力**、**取消抽卡(gacha)变现**(将军转战功解锁+横向特化养成)、地图=**省由 3-4 格聚合+地形+美化**(三层重构)。本轮战略讨论(Phase 1 同时服务硬核 wargame 与抽卡两个互斥画像→拍板消除冲突)落档:新建 `Design/PRODUCT_DIRECTION.md`(产品宪法)、`Design/MAP_ARCHITECTURE.md`(Country→Province→Tile 三层 + 邻接自动推导 + 渲染换栈 Tilemap)、`Design/PHASE2_ROADMAP.md`(P2.0 收口+CI+迁移 → P2.1 抽卡退役 → P2.2-5 地图重构 → P2.6 硬核验证);更新 `ARCHITECTURE.md`(附录 D 战略架构含义;技术债 C-1 CI/C-2 迁移提前至 P2.0、新增 C-7 抽卡退役/C-8 渲染换栈)、`PROJECT_STATE.md`(§0 读取顺序+§2 下一步+§3 进度+§4 决策)。**确定性架构升级为核心资产**(异步 PvP/回放/反作弊地基)。涉规则 7/9/14。
- 2026-06-01 [Phase1-closeout 复审·部分达成 → 签发 fix] Claude 复审 OpenClaw 提交（`50790a1`→`110824e`，4 commit）。**达标**：F1 存档持久化(并主动补修 pendingPeaceOfferExpiry/cutoffTurns/isEntrenched 历史漏读 + CheckPromotions if→while 连升 bug)、F3 确定性 id 实现、F4 军衔(少→帅/maxDivisions=rank+1,红线清零)、F5 atTurn。**未达成 → 签发 `WorkOrders/Phase1-closeout-fix.md`**：🔴 **G1** 运行证据零交付(无 artifact/截图,"7 failures resolved" 无佐证,合入闸门)；🔴 **G2** commander/gacha 续跑等价测试缺失——HashWorld 扩了字段但无测试构造将领/抽卡 → 仍空检查盲区(C1 老毛病第三次)；🟠 **G3** 确定性 id 测试未补；🟠 **G4** 越界改 SupplyResolver + CommanderSkillEvaluator 公式(疑似合理但无测试/说明,需举证或回退)；🟠 **G5** Packages 自决依赖(删 ads/purchasing/adaptiveperformance/vectorgraphics,需举证 resolve 或回 main) + UserSettings 误入库。涉规则 9/12。
- 2026-06-01 [Phase1-closeout 签发] Claude 闭合审查 C5→C17（57 commit / 196 文件 / +14709/-542）后**打回**，签发 `WorkOrders/Phase1-closeout.md`（执行方 OpenClaw）。必修（🔴 上线红线）：**F1** SaveMapper 持久化 commanders/gachaTickets/gachaPityCounter/unit.commanderId（现存读档玩家将领·券·星级·任命全清零）；**F2** HashWorld 纳入新字段 + 续跑等价测试（防 F1 盲区，C1 老毛病）；**F3** GachaResolver 去 `Guid.NewGuid()` 改确定性 id（违 SplitMix64 决策 + 读档撞号隐患，附修 RecruitCommander 未放进 world.commanders 的潜伏 bug）；**F4** C15a-fix 真落地（军衔 少→中→上→大→帅、maxDivisions=rank+1——此前只签发未执行，规则 6 跳过指派重犯）。应修（🟡）：F5 ShopResolver atTurn 接真实回合；F6 删 compile_errors.log + 还原 ProjectSettings/Packages 到 main；F7 补齐 `phase1-closeout-{editmode,playmode}.xml`（`git add -f`）+ 5 张 Play 截图。涉规则 6/7/9/12。GitHub 同步已核：本地=origin=PR #1 `98c712a`，mergeable CLEAN。
- 2026-06-01 [治理待补·已知] CHANGELOG 自 C4 后未追加（违规则 7，断 C5→C17）、PROJECT_STATE 停在 C3、PR #1 描述停在 C5-C13——**完整流水账 + 状态快照待 Claude 单独补录**（OpenClaw 不直接编辑本文件，曾两次写乱码）。
- 2026-06-01 [C4–C17 批量补录·说明] 以下为 C4→C17 流水账回填（CHANGELOG 自 C4 起一度断更，由 Claude 据 git log + 工作单于 2026-06-01 回填，按时间正序）。**Phase 1 = C4→C17 已全部实现，整体处于收口待审**（见顶部 Phase1-closeout 签发；红线未清前不合 main）。
- 2026-05-31 [C4] AI 军事 AI + 战争状态 + 胜负终局：`WarRelation`/`VictoryConditionResolver`/`AIResolver`；`ITurnClock.SetGameOver` + GameOver UI；占领全部敌方首都判胜。
- 2026-05-31 [C5] 外交扩展 — 停战 + 战争代价：`DiplomacyResolver`/`PeaceResolver`/`WarTollResolver`、`warExhaustion`/战争稳定惩罚；规范 `UnitState` 字段名 `organization/maxOrganization`；test stubs 补全接口 + Unity .meta。
- 2026-05-31 [C6] 占领抵抗：省份 `resistance/compliance` 衰减/增长 + 起义事件（`OccupationResolver`）；`HashWorld` 加 resistance/compliance；顺带还原 Packages + 清散落日志。
- 2026-05-31 [C5/C6 审查修复] 5 项必修 + 2 项应修（7bfd573）。
- 2026-05-31 [C7] AI 主动求和：`AiPeaceOfferResolver` — AI 疲惫时向玩家提议停战 + 玩家 Accept/Reject + 提议过期机制。
- 2026-05-31 [C8] AI 调防：`AiRedeploymentResolver` — AI 内陆富裕部队自动调往前线弱守省。
- 2026-05-31 [优先级修复批次] 累积 4 测试失败修复（BattleToll Clamp + WithoutGarrison RNG）+ C7 过期 + C8 补 4 测试 + ReadModelBuilder 映射（d41f73e/b7a6fff）。⚠ 当时 commit 标 "P1"=优先级 1 修复，与本次 **Phase 1** 概念无关。
- 2026-05-31 [C9a + hotfix×2] 经济修复：民用工厂产出 `capital`（修 T5 遗漏），`civilianFactoryCapitalOutput=5`、建造成本 30→25/40→30；占领获产 + 每省基础粮食产出；每省 +1 steel 基础产出（修无钢国无法训兵）。
- 2026-05-31 [C9b] UI 最小集：选国面板 + HUD 三字段（treasury/stability/warSupport）+ 抵抗度显示。
- 2026-05-31 [C9c] 多兵种联合战斗 + GameOver UI。
- 2026-05-31 [C9d-hotfix] 钢铁节流 + 停战和平期（`truceUntilTurn`）。
- 2026-05-31 [C9-cleanup + asmdef] 军工装备产出 2→1；PlayMode asmdef 移除 `UnityEditor.TestRunner` 引用（修 Player 模式闪退）。
- 2026-05-31 [C10] 货币清理：`treasury→capital` 转化 + 装备库存激活；`CountryView.unitCount` HUD 显示部队数；修「训练被拒无提示/资源白扣/初始装备为 0」。
- 2026-05-31 [C11] 师-旅系统：`DivisionTemplate` + `BrigadeState` + 双模式 fallback（旧档单旅退化）。EditMode 247/247。
- 2026-05-31 [push 节奏规则] 设立「每工作单 commit 后立即 push」约定（14e203a）。
- 2026-05-31 [C12] 团战整数化（**规则 9 例外大重构·人类批准**）：float→int `BattleResolver` + 旅级战损分配。EditMode 257/257。
- 2026-05-31 [C13] 补员 + 战役经验（tacticalExp）+ 85% 自动溃退 + `SupplyResolver` 初版；HUD 战斗 org 条/溃退提示/单位详情。
- 2026-06-01 [C13-fix] 师 `organization/morale` 初始化遗漏修复 + 加权 `maxOrganization`。
- 2026-06-01 [C14] `SupplyResolver` 完整：BFS 补给链 + 切断/4 回合死亡窗口/解围/夹击士气/disorganized 状态；HUD 补给显示 + ReadModel C14 字段；顺带钢铁产出 vs 军工消耗再平衡（143ab13）。
- 2026-06-01 [C15a] 将领系统：EU4 将军卡 + HoI4 集团军 + 5 阶军衔晋升 + 同省 5 师容量；`CommanderState`/`CommanderResolver`/`CommanderView`；HUD 将领显示/招募/任命·解除；移除夜战。（多个 fix 为测试构造签名调整 + AssignCommander/Unassign 缺 countryId 修复。）
- 2026-06-01 [C15a-fix 签发·**未执行**] 军衔命名（少→中→上→大→帅）+ `maxDivisions=rank+1` 回归 Claude 原设计。**OpenClaw 只签发未实现** → 由 `Phase1-closeout` F4 执行（规则 6 跳过指派重犯）。
- 2026-06-01 [C15b] 12 张原创将军卡（4SSR/4SR/3R/1N）+ `CommanderSkillEvaluator`（12 skill type）+ `BattleResolver`/`SupplyResolver` 集成。卡名全原创（避版权），SSR 攻击上限 +20（≤+25）。
- 2026-06-01 [C16] 单机抽卡：`gachaTickets` + `DrawCard`（稀有度概率/SSR 保底/升星）+ 战斗胜场累积券。⚠ 审查发现用 `Guid` 生成 commander id 违确定性 → Phase1-closeout F3 修。
- 2026-06-01 [C17] 商城（`ShopResolver`：10 连券包/SSR 保底券/特定卡券）+ 抽卡面板 + 收藏页 + HUD 按钮 → **Phase 1 + 抽卡养成体验闭环（待收口审查）**。⚠ 审查发现 `SaveMapper` 未持久化将领/券（存档红线）→ Phase1-closeout F1 修。
- 2026-05-30 [C3 实现] 战斗与占领系统 — 多回合 HoI 风格战斗。`ActiveBattle` 数据结构（id/attacker/defender/province/turnsElapsed）；`BattleResolver.InitiateAttack`（7 步验证：unit存在→归属→target存在→邻接→非己方控制→移动力≥1→不在战斗中 + 目标省无已有战斗）+ `TickBattles`（1v1 tick + 胜方占领 + 败方消灭 + 清场）+ `DestroyUnit`；`TurnResolver` Settlement 尾段调用 TickBattles；`GameSessionService` MoveUnit 按 controllerCountry 分流（己方→MovementResolver，敌方→BattleResolver）+ 战斗锁定检查；`ReadModelBuilder` 按 controllerCountry 取色 + controllerCountry/isOccupied/hasActiveBattle/isInBattle/activeBattles 列表；`SaveMapper` activeBattles 双向持久化；`MainHudController` 攻击目标红高亮 + 战斗标记 + 事件订阅；USS 新增 attack-target/in-battle/battle-badge 样式。4 事件：BattleInitiatedEvent/BattleConcludedEvent/ProvinceOccupiedEvent/UnitDestroyedEvent。**P2 修复**：garrison cycling 改按 controllerCountry+ownerCountry 过滤（防选中敌部队）；InitiateAttack 拒绝目标省已有战斗（防静默消失）；真多 tick 累积伤害测试。**20 测试**：BattleResolverC3Tests(12) + GameSessionServiceTests(+3) + ReadModelBuilderTests(+4) + SaveLoadEquivalenceTests(+1)。规则 3/4/5/8/9 全守。30 files, +1198/-72。

- 2026-05-29 [C2a 签发] `WorkOrders/C2a-unit-production.md`（执行方 OpenClaw）：军事阶段第二步·部分一——玩家在首都训练 1 支步兵，下单一次性扣 `infantry.cost` + manpower=hp，2 回合后完工驻首都。**人类拍板的设计取舍**（规则 14）：C2a 造兵 / C2b 移动拆两单；移动模型=单步邻接+movesLeft；造兵地点仅首都；成本=`UnitConfig.cost`+manpower+多回合队列。**Phase 0 强制收 C1 三项尾巴**：① `SaveLoadEquivalenceTests.HashWorld` 扩 units 13 字段 + 省份静态字段（C1 续跑等价测试本身是空检查的盲区）；② `UnitSaveData` 扩 13 字段（决策 A：全字段持久化）+ `SaveMapper.ToRuntime` 重建 `country.unitIds`（激活 C1 起的死字段）；③ `ReadModelBuilder` 遍历 units 按 id 升序。新增 `Simulation/UnitProductionResolver`、`Domain/UnitFactory`（与 C1 WorldInitializer 步兵创建块共享，规则 3）、`Contracts/UnitProducedEvent`。**新数值**（Claude 代拟）：`economy.json` 加 `unitProductionTurns=2`（规则 14 可调）。
- 2026-05-29 [C1 审查通过] 领土邻接 + 初始部队 + 地图驻军 — 军事阶段地基达成。**主体过**：6 省 neighbors 对称（high_peak 4 邻枢纽）；6 支初始 infantry 满编驻各国首都；ProvinceView+garrisonCount/neighbors；地图 `⚔N` 徽章 + 详情邻接/驻军；SaveMapper 顺补 B2 漏存的 gridX/gridY/terrain。**最新 artifact**：`artifacts/c1-editmode5.xml`（97/97）+ `c1-playmode-final.xml`（5/5）为权威证据；**c1-editmode-final.xml 是更早版本（3 failed）—命名混乱**，C2a Phase 0 清理。**8 项尾巴**：① HashWorld 不含 units 致续跑等价失明（C2a Phase 0.1 修）；② UnitSaveData 字段不全（C2a Phase 0.2 决策 A 全字段持久）；③ ReadModelBuilder 遍历 units 无序（C2a Phase 0.3）；④ garrisonCount O(P*U) 触发技术债 C-5 提醒；⑤ artifacts 命名乱（C2a Phase 0.4 清理）；⑥ commit 越界将 Claude dirty 治理文件一并打包（OpenClaw commit 卫生需改）；⑦ OpenClaw 顺补 B3 真 bug `EconomyConfig` 缺 `aiBuildCapital*` 3 字段（B3 审查盲区，记录复盘——Claude 以后必交叉 cs 与 json 字段）；⑧ Play 截图待补（与 C2a 一并出 `Design/screenshots/c1-*.png`）。**复盘**：`Ideology` 枚举（Domain）与 `ConfigValidationTests.validIdeologies` 集合漂移——硬编码字符串集合应改为引用枚举，列入后续技术债。
- 2026-05-28 [记忆/恢复机制] 为解决"跨会话/上下文上限后丢失项目脉络"问题,建立 `PROJECT_STATE.md`(项目恢复入口/状态快照):新会话读它即可重建全局(进度时间线 T0→当前、锁定决策、协作复盘、文件地图、下一步、技术债指针)。原则:**真相源在仓库 git 跟踪文件,非任何人私有记忆**(私有记忆会丢、OpenClaw 读不到)。`PROJECT_RULES.md` 执行约定新增"记忆/恢复机制"条:新会话先读 PROJECT_STATE、每工作单审查通过后更新它。Claude 私有记忆顶部加指针导向 PROJECT_STATE。

### Milestone
- 2026-06-06 🎉 **Phase 1 闭合(C4→C17 + 三轮收口)**:军事灵魂版 + 将领养成全部实现,通过收口审查 + 人类 Play 验收。EditMode 341/341 + PlayMode 7/7 全绿;存档持久化(将领/抽卡/师任命)、确定性 id、军衔(少→帅)、Packages 卫生达标。**合入 main**。完整循环:选国→建厂/调税→造兵编师→移动→师级整数战斗(补给链+将领 buff+星级)→占领抵抗→战争胜负→胜场养成。复盘:三轮收口暴露 OpenClaw 反复"报完成不附真证据"(截图造假/数字虚报)→"只信代码 + 人类 Play 验收"为关键防线;CI 门禁(C-1)+存档迁移(C-2)列 Phase 2 前置 P2.0。**下一步 Phase 2**:硬核 F2P 服务型 + 地图三层重构(`Design/PHASE2_ROADMAP.md`)。
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

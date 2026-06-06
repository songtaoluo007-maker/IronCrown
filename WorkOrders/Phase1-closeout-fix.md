# Phase1-closeout-fix — 第二轮收口（证据 + 测试盲区 + 越界回收）

## 背景
Claude 复审 Phase1-closeout（commit `50790a1`→`110824e`）。**F1/F3/F4/F5 代码达标**，并主动修了几处历史隐患（记功，勿回退）：`pendingPeaceOfferExpiry`/`cutoffTurns`/`isDisorganized`/`isEntrenched` 存档漏读补全；`CommanderResolver.CheckPromotions` 由 `if`→`while`（一次跨多级连升 bug）。

但有 **4 类未达成**，合入 main 前必须清。执行方 OpenClaw，分支 `feature/c5-diplomacy-peace`（PR #1）。治理文件（CHANGELOG/PROJECT_STATE/PROJECT_RULES/ARCHITECTURE）由 Claude 维护，**勿碰**。

---

## G1 🔴 运行证据零交付（上轮 F7 未做）—— 合入闸门
- 无 `artifacts/phase1-closeout-*.xml`，无 `Design/screenshots/*.png`。
- "resolve all 7 test failures" 无任何 artifact 佐证 → Claude 跑不了 Unity，**无法验证测试真绿**。
- 要求（硬性）：
  - `artifacts/phase1-closeout-editmode.xml` + `phase1-closeout-playmode.xml`，**`git add -f`**、本轮时间戳；EditMode 数 = 既有 ∪ 本单新增（G2/G3）不得倒退；PlayMode 真跑非空。
  - `Design/screenshots/` 5 张（同 Phase1-closeout F7.2）：`phase1-rank-names` / `phase1-gacha-panel` / `phase1-collection` / `phase1-shop` / `phase1-saveload`。
  - **无证据不复审。**

## G2 🔴 存档等价测试仍是盲区（上轮 F2 实质未达成）
HashWorld 已扩 commander/gacha 字段 ✅，但**没有任何测试构造 commander/gacha 状态**——`BuildWorldWithProvinces()` 里 commander 恒空、gachaTickets=0 → commander/gacha 存档路径仍是**空检查**（C1 盲区第三次重演，这恰是 F2 要防的）。

必补 2 个测试（`Assets/Tests/EditMode/IronCrown.Application.Tests/SaveLoadEquivalenceTests.cs`），骨架：
```
SaveLoad_CommanderAndGacha_SurvivesRoundTrip:
  config 注册 EconomyConfig(global, gacha 字段) + 3~4 张 CommanderConfig(含 SSR/N)
  world = BuildWorldWithProvinces(); country.gachaTickets = 50;
  var cmdRes = new CommanderResolver(config);
  var gacha  = new GachaResolver(new EventBus(), cmdRes);
  gacha.DrawCard ×3(造将领); cmdRes.AssignDivision(1 师给某将领);
  对同一卡再 DrawCard → 升星(starLevel>0);
  ToSave → ToRuntime → Assert.AreEqual(HashWorld(before), HashWorld(after));
  逐项: loaded commander 数 / starLevel / gachaTickets / unit.commanderId 均保留;
  特别 assert: 读档后该国 commanderIds.Count == 抽到的将领数(验重建)。

SaveLoad_RunForward_WithCommanders_Equivalent:
  上述含将领世界 → RunTurns 2 → 存 → 读 → 再跑 2 == 直跑 4(HashWorld 相等)。
```
> Application.Tests 已引用 Simulation（现有测试用 BattleResolver 等），可直接 new GachaResolver/CommanderResolver。

## G3 🟠 确定性 id 无测试（上轮 F3.4 未做）
`GenerateCommanderId` 实现正确，但确定性回归测试没补。必补（`Assets/Tests/EditMode/IronCrown.Simulation.Tests/GachaResolverTests.cs`）：
```
DrawCard_SameSeed_ProducesSameCommanderIds:
  两个独立 world(各 BuildWorld + gachaTickets 充足), 同种子 RandomService(同 seed)
  各 DrawCard 20 次, 收集每次返回 commander.id(新建的)
  Assert 两边 id 序列逐一完全相等。
```

## G4 🟠 越界改 Resolver 公式：举证保留 或 回退（违"严禁改 Resolver 公式"）
本轮擅自改了两处战斗/补给公式，工作单明令严禁、且**无伴随测试、无说明**：
1. `SupplyResolver` +17 行（解围时连带解除邻省被切断友军）。
2. `CommanderSkillEvaluator.EvalAttack` 的 `brigadeBonus` 条件加 `|| skill.stat == "attackDefense"`。

> 二者**疑似合理修复**（解围惠及邻省符合 HoI 直觉；attackDefense 应攻防都加、原实现只加防御与 C15b 卡设计本意不符）。但越界 + 静默 = 不可接受。处置：
- 每处：① PR 描述写明动机 + 它修的是哪个失败/还原哪个设计本意；② 各补 1 个测试锁定新行为；③ 经 Claude 确认后保留，由 **Claude** 补 CHANGELOG（规则 9 例外流程）。
- 若无法举证为必要 → 回退该改动。
- **严禁再夹带任何未声明的 Resolver 公式/数值改动。**

## G5 🟠 Packages 自决依赖 + UserSettings 污染（上轮 F6 偏离）
- 最终 `manifest.json` 与 origin/main 分叉：删了 `com.unity.ads`/`com.unity.purchasing`/`modules.adaptiveperformance`/`modules.vectorgraphics`，缩进改 2→4、结尾丢换行。工作单要求"还原到 origin/main"，本轮改为自行裁定依赖集（规则 12 执行者不做决策）。
- `UserSettings/EditorUserSettings.asset` 被 commit（purchasing installRecorded 值变化）——本地编辑器设置，**绝不入库**。
- 处置：
  - 贴证据：origin/main 版 manifest 在 Unity 6000 下 batchmode 启动是否 `Package resolve` 报错（贴日志）。
    - 若报错 → 保留**最小修正**（仅删确实不存在的模块），并**逐个说明**删每个条目的理由（尤其 `ads`/`purchasing` 为何删）；缩进/换行尽量贴近 main 风格。
    - 若不报错 → `git checkout origin/main -- Packages/manifest.json Packages/packages-lock.json` 回退。
  - `git checkout origin/main -- UserSettings/EditorUserSettings.asset` 还原，并确认 `.gitignore` 忽略 `UserSettings/`（未忽略则补，PR 描述注明）。

## G6 🟡 越界清理（备案，可保留）
- 删 `Assets/Tests 1/`（Unity 自动生成的 `NewTestScript` 垃圾目录）：无害，确认全仓无引用后保留删除即可。本条仅备案，无需动作。

---

## DoD
- [ ] G1 证据齐：`phase1-closeout-{editmode,playmode}.xml`（`git add -f`、本轮时间戳）+ 5 张截图
- [ ] G2 两个 commander/gacha 等价测试，绿（读档后将领/券/星级/任命/commanderIds 全保留）
- [ ] G3 确定性 id 测试，绿
- [ ] G4 两处越界改动：举证（动机 + 锁定测试）经确认保留，或回退
- [ ] G5 Packages 举证保留最小修正 或 回退 main；UserSettings 还原 + .gitignore 覆盖
- [ ] EditMode 全绿（含 G2/G3 新增）+ PlayMode 真跑非空
- [ ] CHANGELOG/PROJECT_STATE/PROJECT_RULES/ARCHITECTURE **0 改动**
- [ ] commit 完成立即 `git push origin feature/c5-diplomacy-peace`
- [ ] PR 描述逐项回应 G1–G6（含 G4 动机、G5 resolve 日志）

## 严禁
- 再改任何战斗/补给/经济/外交 Resolver 公式或数值（除 G4 经 Claude 确认项）。
- 改军衔名 / maxDivisions 公式（上轮已正确，勿动）。
- commit 任何 `UserSettings/`、`Library/`、本地编辑器状态。
- 直接编辑 CHANGELOG / PROJECT_STATE / PROJECT_RULES / ARCHITECTURE。
- 不 push / 报完成不附 artifact。

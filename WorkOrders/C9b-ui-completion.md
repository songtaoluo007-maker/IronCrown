# C9b — UI 最小集补全：详情 resistance + HUD 国家状况 + 开局选国

## 背景
人类真玩 37 回合反馈：**"占领区反抗、国家基本状况看不到、开局没选国界面"**——C1 起 ProvinceState.resistance/compliance 字段一直在但**未在 UI 显示**；HUD 自 T6 起只显示 turn/phase/治理档位，**stability/warSupport/treasury 三个基础指标从未上 UI**；NewGame(playerCountryId) 默认选 id 升序第一个，**玩家从未真正选过国**。

C9b 用最小代价把"玩家能看到的国家状况"补到位，让 C5-C8 五层机制（停战 / 战争代价 / 占领反抗 / AI 求和 / AI 调防）的数据**可见**。

## 范围（用户拍板：最小集）

### 三块补全
| 模块 | 补什么 | 在哪显示 |
|------|--------|----------|
| **省份详情栏** | resistance + compliance（被占省）| RenderProvinceDetail 现有 `驻军: N 支 \| 法理: X / 控制: Y` 之后追加 `\| 抵抗: R/100 \| 顺从: C/100`（只在 isOccupied=true 时显示） |
| **HUD 玩家国基础状况** | treasury + warSupport + stability（玩家国）| HUD 顶栏 `推进` 按钮左侧加一行：`💰{treasury} 🏛{stability} ⚔{warSupport}`，与战疲并列 |
| **开局选国界面** | 6 国卡片（id/名/意识形态/首都/初始资源摘要），点选后调 SetPlayerCountry | 新建 `NationSelectionScreen` UXML，应用启动时显示，选完进 Main 场景 |

### 不做（留 C9c/C10+）
- 资源面板（steel/food/oil/rareMetal/capital 全展示）→ C9c
- 部队列表面板 → C9c
- 事件日志面板（看历史 BattleResolved / PeaceConcluded / Uprising 等）→ C10+
- MainHudController 组件化重构（C-4 技术债）→ 大重构留单独单
- 国家详情弹窗（点国家行看完整数据）→ C9c

## 文件变更清单

### Contracts
- `CountryView.cs` — 字段已有 `treasury / stability / warSupport`，无新增

### Application
- `ReadModelBuilder.cs` — 字段已透传，无新增
- `GameSessionService.cs` — `SetPlayerCountry` 方法已有，无新增

### Presentation
- `MainHudController.cs`：
  - `RenderProvinceDetail`：在 `isOccupied` 分支追加 `抵抗: {pv.resistance}/100`（不显 compliance 直到 C10+，避免 UI 过载）
  - `Render`：玩家国 CountryView 拿 `treasury / stability / warSupport`，写入新加的 3 个 Label
  - `Bind`：Query 3 个新 Label `treasury-label / stability-label / war-support-label`
- 新建 `Presentation/NationSelectionController.cs`：
  - `Bind(VisualElement root)` 拿 6 国卡片按钮 + 点击回调 → `_session.SetPlayerCountry(countryId)` → `SceneManager.LoadScene("Main")` 或显示 Main 场景
  - 数据源：`_session.GetWorldView().countries` 6 国 OrderBy id
- `MainHud.uxml` — 加 3 个 Label 节点 `<Label name="treasury-label" />` 等到 HUD 顶栏
- `MainHud.uss` — `.hud-stat` 简单样式（白字 14px）；`.nation-card` 卡片样式
- 新建 `Assets/UI/NationSelection.uxml` — 6 国卡片纵向列表，每张含 国名 + 意识形态 + 首都省 + treasury 初值
- 新建 `Assets/UI/NationSelection.uss` — 卡片配色（按国 mapColor 取色）

### Bootstrap
- `GameLifetimeScope.cs` — 注册 NationSelectionController（singleton lifestyle）
- `Editor/SetupScene.cs` — 加菜单项 `IronCrown > Setup Nation Selection Scene` 生成选国场景（参考既有 Setup Main Scene 模板）
- 或简化：**不新建场景**，在 Main 场景启动时若 `playerCountryId == null` 则覆盖一层 NationSelection 面板，选完隐藏（更省事）

### Data
- 无新增数值

### Tests
- `MainHudControllerTests.cs` 追加：
  - `RenderProvinceDetail_OccupiedProvince_ShowsResistance`
  - `Render_PlayerCountry_TreasuryLabelUpdated`
  - `Render_PlayerCountry_StabilityLabelUpdated`
  - `Render_PlayerCountry_WarSupportLabelUpdated`
- 新建 `NationSelectionControllerTests.cs`：
  - `Bind_PopulatesSixCountryCards`
  - `OnCardClick_SetsPlayerCountry`
  - `OnCardClick_TransitionsToMain`（验证场景切换或面板隐藏触发）
- PlayMode 追加：`NationSelection_SelectCountry_SetsPlayerInWorldView`

## DoD Check List
- [ ] Phase 0：起点全绿（C9a 合入后基线，capital 修复后 Play 流畅）
- [ ] 不动 Domain / Simulation / Application 任何 Resolver 公式（仅 UI + Bootstrap）
- [ ] 不动既有 economy.json / units.json / countries.json / provinces.json 数值
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] 选国界面后调 SetPlayerCountry → 进 Main → HUD 玩家国 = 所选国（端到端验证）
- [ ] 占领某省（用 C5 命令或直接 Play） → 详情栏可见 `抵抗: 50/100`（C6 初始 resistance）
- [ ] 推 5 回合 → HUD treasury / stability / warSupport 数字按 EconomyResolver / PoliticsResolver 公式变化（数字真在动）
- [ ] 测试覆盖：4 个 MainHudController 追加 + 3 个 NationSelectionController 新建 + 1 个 PlayMode smoke
- [ ] artifacts/c9b-editmode.xml + c9b-playmode.xml 归档
- [ ] **截图 3 张**：
  - `c9b-nation-selection.png`（选国界面 6 卡片可见）
  - `c9b-hud-stats.png`（HUD 顶栏 treasury/stability/warSupport/战疲 都有数字）
  - `c9b-occupation-resistance.png`（被占省详情栏含 `抵抗: N/100`）
- [ ] Unity Console 0 error（USS 2 条 text-align warning 豁免）
- [ ] batchmode 0 error 且 0 failed（C9a 应已修 4 累积失败 fixture）
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 前置
- **C9a 合入 main**——经济修好后 Play 才能跑通 5 回合验证 HUD 数字变化
- OpenClaw 可在 C9b 分支并行写代码，但 PR 提交必须基于 C9a 之后的 main rebase

## 严禁
- MainHudController 组件化重构（留 C-4 技术债专项单）
- 加资源面板 / 部队列表面板（C9c）
- 加事件日志面板（C10+）
- 改 UXML 整体布局（仅追加节点）
- 改 USS 既有样式（仅新增 class）

## 歧义处理
- **NationSelection 场景 vs 覆盖面板**：OpenClaw 自选。覆盖面板更简单（不动 Bootstrap 入口），新场景更"正式"。**默认推荐覆盖面板**，截图为准。
- **选国后 Bootstrap 流程**：若用覆盖面板，NewGame 先在 GameEntryPoint.Start 跑（playerCountryId=null）→ 显示选国面板 → 玩家点选 → SetPlayerCountry → 隐藏面板 → 进游戏。若用新场景，IronCrown 菜单加 Setup NationSelection 项即可。
- **resistance 显示位置**：详情栏一行末尾追加，**不另开 UI 块**。compliance 字段暂不显（C1 起未被 Resolver 使用，UI 显示等于误导玩家"有这机制"）。
- **HUD treasury 显示形式**：`💰{treasury}` 整数即可，避免 1.5K/2M 这种格式化。
- **三 Label 位置**：顶栏 `回合 N · Phase` 同一行右侧、`推进` 按钮左侧。横向排列。
- **若 UI 节点查询失败**（Bind 时 root.Q 返回 null）→ Render 跳过该 Label（防御性），不抛异常。已有 C2b/C4/C5 UI 同款防御性套路。

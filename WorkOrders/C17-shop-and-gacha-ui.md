# C17 — 抽卡 UI + 商城 + 收藏页（gachaTickets 完整体验）

## 背景
C16 已实现抽卡机制（券消耗 + 概率 + 升星 + 保底），但 UI 是 HUD 内联按钮 + 状态栏文字。C17 做**完整体验闭环**——抽卡详情面板 / 收藏页 / 商城（玩家用 gachaTickets 买不同商品）。

C17 完成 = **Phase 1 + 抽卡养成完整体验毕业**——下一步进 Phase 2 战略层（国策 / 决议 / 外交 / 贸易）。

## 范围

### 三块 UI
| 模块 | 描述 |
|---|---|
| **抽卡面板** | 按"抽卡"按钮 → 弹模态面板：显示抽到的卡（稀有度颜色 + 名 + 技能列表 + 升星动画/反馈）；含"再抽 1 次" 按钮（消耗券）+ "返回" |
| **收藏页** | HUD 加"我的将领"按钮 → 弹列表：玩家所有 commander 按稀有度排序、显示卡名 + 军衔 + 星级 + 麾下师数 + 技能 |
| **商城** | HUD 加"商城"按钮 → 弹商城面板：商品列表（10 张普通券 = 10 券、SSR 兑换券 = 200 券、特定卡兑换券 = 100 券）；点购买消耗 gachaTickets |

### 配置（economy.json）
```json
{
  "shopBundle10Draws": 10,
  "shopSsrTicketCost": 200,
  "shopSpecificCardTicketCost": 100
}
```

### 商品（写死 3 类，简化）
| 商品 | 成本 | 效果 |
|---|---|---|
| 10 连抽券包 | 10 gachaTickets | 给玩家直接 10 张抽卡券（自抽好处）|
| SSR 保底兑换券 | 200 gachaTickets | 玩家选稀有度=SSR 抽 1 次必出（不扣 pity） |
| 特定卡兑换券 | 100 gachaTickets | 玩家选定特定 cardId → 直接获得（如已有则升星） |

**注意**：商城是"用游戏内 gachaTickets 买更多 gachaTickets 服务"的循环。它的价值是给玩家**确定性获取强卡的路径**——不必只靠运气抽。

## 文件变更清单

### Application
- `GameSessionService.cs` — IssueCommand 加 3 个分支：`Buy10DrawBundle` / `BuySsrTicket` / `BuySpecificCardTicket`
- `ReadModelBuilder.cs` — 无新字段（沿用 C16 的 commander 列表）

### Contracts
- `CommandType.cs` — 加 3 个商品命令
- `GameCommand.cs` — 加 `string targetCardId`（特定卡券用）
- 新建 `Contracts/Events/ShopPurchasedEvent.cs` — `{ buyerCountry, itemKind, cost, atTurn }`

### Simulation
- 新建 `Simulation/ShopResolver.cs`：
  - `BuyBundle(country, eco)` → 扣 10 券给 10 券（这单的"抽卡券类商品"等价于"花一张券抽 1 次"——这条略冗余、写死保留作为玩家"批量"心理体验）
  - `BuySsrTicket(country, eco)` → 扣 200 券 + 调 GachaResolver 强制 rarity="SSR"
  - `BuySpecificCardTicket(country, eco, cardId)` → 扣 100 券 + 直接 GachaResolver.GrantCard(cardId)

### Domain
- `EconomyConfig.cs` — 加 3 个 C17 字段
- 既有 GachaResolver 加 `GrantCard(string cardId)` 公共方法（C16 私有部分提取）

### Data
- `economy.json` — 加 3 个 C17 字段（值见 §配置）

### Presentation
- 新建 `Presentation/GachaPanelController.cs` — 抽卡结果面板
- 新建 `Presentation/CollectionPanelController.cs` — 收藏页
- 新建 `Presentation/ShopPanelController.cs` — 商城
- `MainHudController.cs` — 加 3 个 HUD 按钮（"我的将领"、"商城"、"抽卡历史"）
- 新建 `Assets/UI/GachaPanel.uxml + .uss` —— 抽卡反馈卡牌动画（**最小化 = 显示稀有度颜色边框 + 名 + 技能列表，不做翻牌动画**）
- 新建 `Assets/UI/CollectionPanel.uxml + .uss`
- 新建 `Assets/UI/ShopPanel.uxml + .uss`

### Tests
- 新建 `ShopResolverTests.cs`：
  - `Buy10DrawBundle_DeductsTicketsAddsTickets`（玩家有 100 券 → 买 10 连券 → 扣 10、净增 10 = 净 0、相当于无效但保留作为体验体感）

    **修正**：10 券包应该 = 玩家直接给 10 张抽卡机会，**不应该是 10 券扣 10 券**——这就成了无意义操作。
    
    **改设计**：10 连抽券包 = 8 券扣，**给 10 抽机会**（折扣价 = 体验奖励）。**写死**：`shopBundle10Draws_cost: 8`、`shopBundle10Draws_grants: 10`。
  - `BuySsrTicket_DeductsTickets_ForcesSsr`
  - `BuySpecificCardTicket_DeductsTickets_GrantsSpecificCard`
  - `BuySpecificCardTicket_UnknownCardId_Rejects`
  - `Buy_InsufficientTickets_Rejects`
- `SaveLoadEquivalenceTests` — `SaveLoad_ShopState_NoExtraFieldsToPersist`（验证商城不存额外状态）
- `MainHudControllerTests` — 3 个面板按钮 click → 命令发出

## 数据修正（10 连抽折扣价）
原 `shopBundle10Draws: 10` 改为：
```json
{
  "shopBundle10Draws_cost": 8,
  "shopBundle10Draws_grants": 10,
  "shopSsrTicketCost": 200,
  "shopSpecificCardTicketCost": 100
}
```

## DoD Check List
- [ ] 3 个商品逻辑实现 + 3 个事件
- [ ] 3 个 UI 面板（GachaPanel / CollectionPanel / ShopPanel）齐全
- [ ] HUD 加 3 按钮
- [ ] 既有 269+ + 本单新增（约 10）全绿
- [ ] **★ commit 完立即 push**
- [ ] artifacts/c17-editmode.xml + c17-playmode.xml
- [ ] **Play 截图 3 张强制**：
  - `c17-gacha-panel.png`（抽到 SSR 卡片面板）
  - `c17-collection-page.png`（玩家收藏的将军列表）
  - `c17-shop-purchase.png`（商城界面 + 购买 SSR 券瞬间）
- [ ] Unity Console 0 error
- [ ] PR 描述含 DoD

## 严禁
- 加现实货币
- 加抽卡动画（翻牌 / 闪光等留 D 美术）
- 加 AI 商城（AI 不用 gachaTickets）
- 改 GachaResolver C16 公式（仅扩 GrantCard 公共接口）
- 改 BattleResolver / SupplyResolver / 任何 Resolver 战斗公式
- 不 push

## 不做
- 卡牌交易 / 玩家间互动 — 永远不做（单机定位）
- 抽卡保底跨周目继承 — C18+
- 卡牌图鉴 / 全收集成就 — D 美术
- 卡牌升级（不同于升星，如"+5 攻击改造"）— C20+
- AI 模拟商城（AI 不需要）

## 完工后人类 Play 验证
1. 推几回合赢战 → 攒到 30+ gachaTickets
2. 点"抽卡" → 抽卡面板弹出 → 反馈"抽到: 铁壁元帅（SSR）"
3. 点"我的将领" → 收藏页显示所有 commander + 稀有度颜色排序
4. 点"商城" → 3 商品列表
5. 攒到 200 券 → 买 SSR 保底券 → 立即抽 SSR → 任意一张 SSR
6. 攒到 100 券 + 已知特定 cardId → 买特定卡兑换券 → 直接获得（如已有则升星）
7. 完整循环：胜利→赚券→抽卡→升星→更强→胜更快

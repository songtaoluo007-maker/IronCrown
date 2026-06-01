# C16 — 单机抽卡养成 + N/R/SR/SSR + gachaTickets + 升星

## 背景
C15b 提供 12 张卡池，C16 实现**单机抽卡机制**——玩家通过游戏内成就赚 `gachaTickets`，消耗券抽卡获得新将军或升星。

**商业模式锁定单机**（人类已决策）—— 无服务器、无现实货币、无防作弊负担。

## 范围

### 核心机制
| 件 | 描述 |
|---|---|
| **gachaTickets 货币** | 新增 CountryState.gachaTickets（int）；通过战役胜利 / 包围歼敌 / 占领首都赚取 |
| **DrawCard 命令** | 消耗 1 张券 → 按稀有度概率滚动 → 抽出一张卡 |
| **升星** | 重复抽到同卡 → +1 星（5 星上限）→ 每星 +5% 战斗 buff |
| **稀有度概率** | N 50% / R 35% / SR 12% / SSR 3%（保护单机玩家） |
| **保底 (pity)** | 50 抽未出 SSR → 下次必出（pity counter） |

### 配置（economy.json）
```json
{
  "gachaTicketCostPerDraw": 1,
  "gachaTicketsPerVictory": 1,
  "gachaTicketsPerEncirclement": 3,
  "gachaTicketsPerCapitalCapture": 10,
  "gachaRarityWeightN": 50,
  "gachaRarityWeightR": 35,
  "gachaRarityWeightSR": 12,
  "gachaRarityWeightSSR": 3,
  "gachaSsrPityThreshold": 50,
  "starBonusPerStar": 5,
  "maxStarLevel": 5
}
```

### 数值代拟（Claude 拟，规则 14 你可调）
| 项 | 值 | 体验估算 |
|---|---|---|
| 初始券（玩家开局送）| 10 张 | 头 5 回合可抽 10 张试试手 |
| 每场战役胜 | +1 券 | 30 回合战役 → 30 券 |
| 每次包围歼敌 | +3 券 | 战略级胜利奖励 |
| 每占敌首都 | +10 券 | 通关前能凑齐主力卡组 |
| 单抽成本 | 1 券 | 简单可控 |
| SSR 保底 | 50 抽 | 保底打底，避免单机玩家完全脸黑 |

### 抽卡算法（确定性 + IRandom）
```csharp
public CommanderState DrawCard(CountryState country, IRandom rng, IConfigRegistry config, EconomyConfig eco) {
    if (country.gachaTickets < eco.gachaTicketCostPerDraw) return null;  // 券不足
    country.gachaTickets -= eco.gachaTicketCostPerDraw;
    country.gachaPityCounter++;
    
    // 保底
    string rarity;
    if (country.gachaPityCounter >= eco.gachaSsrPityThreshold) {
        rarity = "SSR";
        country.gachaPityCounter = 0;
    } else {
        int roll = rng.Range(0, eco.gachaRarityWeightN + eco.gachaRarityWeightR 
                          + eco.gachaRarityWeightSR + eco.gachaRarityWeightSSR);
        if (roll < eco.gachaRarityWeightN) rarity = "N";
        else if (roll < eco.gachaRarityWeightN + eco.gachaRarityWeightR) rarity = "R";
        else if (roll < eco.gachaRarityWeightN + eco.gachaRarityWeightR + eco.gachaRarityWeightSR) rarity = "SR";
        else { rarity = "SSR"; country.gachaPityCounter = 0; }
    }
    
    // 从卡池按稀有度取（按 id 升序、rng.Range 选）
    var pool = config.All<GeneralCardConfig>().Where(c => c.rarity == rarity).OrderBy(c => c.id).ToList();
    if (pool.Count == 0) return null;
    var picked = pool[rng.Range(0, pool.Count)];
    
    // 判定升星 or 新卡
    var existing = country.commanderIds
        .Select(id => world.commanders[id])
        .FirstOrDefault(c => c.generalCardId == picked.id);
    
    if (existing != null && existing.starLevel < eco.maxStarLevel) {
        existing.starLevel++;
        _events.Publish(new CardStarUpgradedEvent { commanderId = existing.id, newStar = existing.starLevel });
        return existing;
    }
    if (existing != null) {  // 满星，转成普通胜场经验 +5
        existing.victories += 5;
        _events.Publish(new CardConvertedToExpEvent { commanderId = existing.id, expGained = 5 });
        return existing;
    }
    
    // 新卡 → 调 C15a 招募流程（不消耗 capital/manpower，直接给）
    var newCmdr = CommanderResolver.CreateFromCard(picked, country.id, ...);
    _events.Publish(new CardDrawnEvent { rarity = rarity, cardId = picked.id, commanderId = newCmdr.id });
    return newCmdr;
}
```

### gachaTickets 累积触发
```csharp
// BattleResolver.TickBattles 收尾扩展（C13/C15a 已有 victoryCount 累积处）：
if (result.attackerWon):
  attackerOwnerCountry.gachaTickets += eco.gachaTicketsPerVictory;
  if (defenderWasCutoff): attackerOwnerCountry.gachaTickets += eco.gachaTicketsPerEncirclement;
  if (defenderProvince.isCapital): attackerOwnerCountry.gachaTickets += eco.gachaTicketsPerCapitalCapture;
```

## 文件变更清单

### Domain
- `Country.cs` — 加 `gachaTickets`（int）+ `gachaPityCounter`（int）+ `commanderIds`（已有 C15a 应该是 generalIds，本单复用）
- `CommanderState.cs` — 加 `starLevel`（int，0-5）
- `EconomyConfig.cs` — 加 11 个 C16 字段

### Simulation
- 新建 `Simulation/GachaResolver.cs` —— DrawCard 算法核心
- `BattleResolver.cs` — TickBattles 收尾加 gachaTickets 累积（victory / encirclement / capital capture）
- `BattleResolver.cs` 单师战力公式扩 `starBonus`（每星 +5%）—— 在 cardSkillBonus 之后再扩 1 乘数

### Application
- `GameSessionService.cs` — IssueCommand 加 `DrawCard` 分支
- `ReadModelBuilder.cs` — CountryView 加 `gachaTickets / gachaPityCounter`；CommanderView 加 `starLevel`
- `SaveModels.cs / SaveMapper.cs` — 双向 + HashWorld 扩 gachaTickets/gachaPityCounter/starLevel

### Contracts
- `CommandType.cs` — 加 `DrawCard`
- 新建 `Contracts/Events/CardDrawnEvent.cs`、`CardStarUpgradedEvent.cs`、`CardConvertedToExpEvent.cs`
- `CountryView.cs / CommanderView.cs` — 加字段

### Data
- `economy.json` — 加 11 个 C16 字段
- `WorldInitializer` — 初始每国 gachaTickets = 10（玩家国），AI 国不送（AI 不抽卡）

### Presentation
- C16 **不做完整抽卡 UI**（C17 才做精美界面）—— 仅 HUD 加：
  - `🎴 {gachaTickets}` 显示券数
  - "抽卡" 按钮 → 发 DrawCard 命令 → 状态栏出 `🎴 抽到: {rarity} {cardName}（{star}★）`

### Tests
- 新建 `GachaResolverTests.cs`：
  - `DrawCard_InsufficientTickets_Rejects`
  - `DrawCard_DeductsOneTicket`
  - `DrawCard_RarityDistribution_AcrossManyDraws`（跑 10000 次抽卡 → 各稀有度分布在 ±2% 内）
  - `DrawCard_SsrPity_AtThresholdGuaranteesSsr`
  - `DrawCard_DuplicateCard_UpgradesStar`
  - `DrawCard_MaxStarDuplicate_ConvertsToExp`
- `BattleResolver` 测试追加：
  - `gachaTickets_VictoryAddsOne`
  - `gachaTickets_EncirclementVictoryAddsFour`（1 普通 + 3 包围）
  - `gachaTickets_CapitalCaptureAddsTen`
- `SaveLoad` 追加：`SaveLoad_GachaState_Preserved`

## DoD Check List
- [ ] GachaResolver 算法实现 + 12 字段 + 3 事件
- [ ] BattleResolver 战力公式加 starBonus
- [ ] HUD "🎴 N" + 抽卡按钮 + 状态栏抽卡反馈
- [ ] 既有 269+ + 本单新增（约 12）全绿
- [ ] **★ commit 完立即 push**
- [ ] artifacts/c16-editmode.xml + c16-playmode.xml
- [ ] **Play 截图 2 张** `c16-draw-card.png`（抽卡后 HUD 券数减 1 + 状态栏出 SSR）+ `c16-pity-triggered.png`（连续 50 抽未 SSR 后第 51 抽必出）
- [ ] PR 描述含 DoD

## 不做（C17 / 后续）
- 完整抽卡 UI（动画 / 详情卡面）— C17
- 商城（用 gachaTickets 买东西） — C17
- 抽卡历史记录页 — C17
- 卡牌图像 — D 美术
- 自动同步多个 SSR 给 AI（AI 不抽卡）
- 兑换券系统 — C17

## 严禁
- 改 BattleResolver C12/C13/C15a/C15b 既有公式（仅扩 starBonus 一层乘数）
- 改 SupplyResolver 公式（gachaTickets 不影响补给）
- 改 EconomyResolver
- 让 AI 抽卡（AI 仍用 C15a 直接给一张测试卡）
- 加现实货币
- 不 push

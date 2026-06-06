# C9d-hotfix — 钢铁黑洞 + 停战和平期

## 背景
人类真玩反馈两个 bug：
1. **占领铁都钢铁没增产** — 实测：军工产装备每回合吃 60 steel（6 军工 × 5 装备 × 2 steel）但占领产出 ~10 steel/回合，**净亏 -50 steel/回合**，steel 永远卡 0-1。
2. **和平协议太简单，就和平一回合** — PeaceResolver 移除 WarRelation 后，AI 下回合 TryAttack 立刻再攻同一邻省，自动重新宣战。

## 范围

### 双修
| 修法 | 改动 | 数值 |
|------|------|------|
| **军工产装备节流** | `militaryFactoryEquipmentOutput: 5 → 2`（economy.json）| 6 军工/回合产 12 装备消耗 24 steel，与产出 ~10 持平略亏 — 长期 steel 不再快速归零 |
| **停战和平期** | WarRegistry 加 `truceUntil(countryA, countryB) → turnNumber` 字典；停战瞬间设 `currentTurn + aiPeaceTruceTurns=10`；BattleResolver.InitiateAttack 加校验"和平期内拒绝" | aiPeaceTruceTurns=10 |

### 配置 (economy.json)
```json
{
  "militaryFactoryEquipmentOutput": 2,  // 从 5 调低
  "aiPeaceTruceTurns": 10                // 新字段
}
```

### 设计哲学
- **钢铁节流**：装备产线慢一点，让 steel 池子能积累。玩家需要规划"先囤 steel 再造兵"——增加策略深度
- **停战和平期**：和平真的有意义——10 回合不打仗，玩家可以补血、调防、转产经济。和平期过后双方可重新宣战（自由选择）

## 文件变更清单

### Domain
- `EconomyConfig.cs` — 加 `int aiPeaceTruceTurns;`
- `WorldState.cs` — 加 `Dictionary<string, int> truceUntilTurn = new()`（key=`"{countryA}_vs_{countryB}"` Ordinal 升序、value=truce 截止回合数）

### Simulation
- `WarRegistry.cs`：
  - 加 `public static void SetTruce(WorldState world, string a, string b, int untilTurn)` —— 标准化 Ordinal + 写入 dict
  - 加 `public static bool IsInTruce(WorldState world, string a, string b, int currentTurn)` —— 检查 currentTurn < untilTurn
- `PeaceResolver.cs`：
  - `OfferPeace` 接受分支调 `SetTruce(world, fromCountry, toCountry, world.turnNumber + eco.aiPeaceTruceTurns)` 在 TryEndWar **之后**
  - `AcceptPeace` 同步：在 `TryEndWar` 之后调 `SetTruce`
- `BattleResolver.cs` — `InitiateAttack` 校验链加一项：
  ```csharp
  if (WarRegistry.IsInTruce(world, attacker.ownerCountry, target.ownerCountry, world.turnNumber))
      return CommandResult.Reject("和平期内不能开战");
  ```
  **位置**：放在"非敌方控制省"校验之后、"movesLeft < 1"之前
- `AIResolver.cs` — `TryAttack` 内层 foreach 加 truce 检查（防御性，BattleResolver 也拒）：
  ```csharp
  if (WarRegistry.IsInTruce(world, country.id, nProv.ownerCountry, world.turnNumber)) continue;
  ```

### Data
- `economy.json`：
  - `militaryFactoryEquipmentOutput: 5 → 2`
  - 加 `aiPeaceTruceTurns: 10`

### Application
- `SaveMapper.cs` / `SaveModels.cs` — `GameState` 加 `TruceEntry[] truces`（Dictionary 序列化成数组：key + value），双向；HashWorld 扩 truceUntilTurn（按 key 升序写）

### Tests
- `WarRegistryTests.cs` 追加：
  - `SetTruce_StoresOrdinalNormalized`
  - `IsInTruce_BeforeUntilTurn_ReturnsTrue`
  - `IsInTruce_AfterUntilTurn_ReturnsFalse`
  - `IsInTruce_NeverSet_ReturnsFalse`
- `PeaceResolverC5Tests.cs` 追加（如文件不存在新建）：
  - `OfferPeace_AcceptedSetsTruce`
  - `OfferPeace_TruceUntilTurn_IsCurrentPlusConfig`
- `PeaceResolverC7Tests.cs` 追加：
  - `AcceptPeace_AlsoSetsTruce`
- `BattleResolverC5Tests.cs` 追加：
  - `InitiateAttack_DuringTruce_Rejects` — 设 truce → InitiateAttack → rejected "和平期内不能开战"
  - `InitiateAttack_AfterTruceExpires_Succeeds` — currentTurn ≥ untilTurn → 通过
- `AIResolverTests.cs` 追加：
  - `TryAttack_DuringTruceWithTarget_SkipsThatNeighbor`
- `EconomyResolverTests.cs` 追加（既有的可能需要更新）：
  - `MilitaryFactory_NewOutputRate_2PerFactory`（regression）
- `SaveLoadEquivalenceTests.cs` 追加：
  - `SaveLoad_TruceUntilTurn_Preserved`
- `ConfigValidationTests.cs` 追加：
  - `Economy_HasAiPeaceTruceTurns`

## 不做
- 玩家主动撕毁和平 / 提前宣战按钮（C10+）
- 和平期到期后自动延长（无延长，过期就过期）
- 不同停战类型（短停 vs 长停）→ 单一 10 回合
- 改 infantry.cost.steel（5 不变，让玩家感受 steel 稀缺）
- 改 iron_city.resourceOutput 或 infrastructure（数据驱动是 C10+ 配置工具链 C-3 范围）

## DoD Check List
- [ ] EconomyResolver 公式不动（仅数值调整）
- [ ] WarRegistry 加 2 静态方法 + dict 字段
- [ ] BattleResolver.InitiateAttack 加 1 行校验，**位置写死**
- [ ] AIResolver.TryAttack 加 1 行 continue 校验
- [ ] PeaceResolver OfferPeace + AcceptPeace 各加 1 行 SetTruce
- [ ] economy.json 仅 2 改（militaryFactoryEquipmentOutput / +aiPeaceTruceTurns）
- [ ] HashWorld 扩 truceUntilTurn（C5 教训：HashWorld 必扩否则 SaveLoad 失明）
- [ ] 测试覆盖：见 §Tests 全部存在且全绿
- [ ] artifacts/c9d-editmode.xml + c9d-playmode.xml 归档
- [ ] Play 截图 2 张：
  - `c9d-truce-active.png`（停战后 HUD 状态栏显示 "和平期 X/10"，或地图省份无 ⚔ 标记，**或简单**：详情栏国家行加 `[和平期 N 回合]` 标识）
  - `c9d-steel-balance.png`（推 20 回合后 steel 仍 > 50，证明军工节流生效）
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 0 failed
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 严禁
- 改 BattleResolver float 战斗公式
- 改 PoliticsResolver / WarTollResolver / OccupationResolver / AiPeaceOfferResolver / VictoryConditionResolver / MovementResolver / ConstructionResolver / UnitProductionResolver / AiRedeploymentResolver 既有公式
- 改 EconomyResolver 之外的数值（steel 主产省/兵种 cost 等）
- 加玩家撕毁和平按钮（C10+）
- 跳过 truce 测试

## 歧义处理
- **truce dict key 格式**：用 `"{lo}_vs_{hi}"` 与 WarRegistry.Normalize 一致 Ordinal 升序，避免 (A,B) 与 (B,A) 出现双键
- **和平期间被第三方进攻**：truce 只对当前两国生效。A 与 B 和平期，A 仍可被 C 攻——这是设计意图（C 单边宣战）
- **和平期内 A 国 controllerCountry 改变**（如 A 被 C 占领某省）：truce 仍然有效（基于 ownerCountry 法理主权，与 WarRegistry.TryDeclareWar 同样基于 ownerCountry）
- **和平期到期后 AI 立刻再攻**：是设计意图（和平期是缓冲期不是永久停战），但 OpenClaw 可以加 `aiPeaceCooldownAfterTruce=3` 让 AI 不立刻发动——本单**不做**，保留"和平期到期后双方自由"
- **UI 显示和平期**：详情栏 / 国家行追加 `[和平期 N 回合]` 文字即可，不必新 UI 区
- **PeaceResolver 既有测试**（如 `OfferPeace_AcceptedSetsTruce` 之前的 PeaceResolverC5Tests）需要回归——验证既有"AcceptedRemovesWarRelation"仍通过

## 完工后人类 Play 验证清单
1. 玩到与某 AI 国停战 → 详情栏看到 "[和平期 10 回合]"
2. 和平期内点击该国邻省 → 弹"和平期内不能开战"
3. 推 10 回合后 → 和平期消失 → 可以再攻
4. 推 20 回合不操作 → steel 不再卡 0-1，应稳定增长（玩家国 + AI 国 steel 都 > 50）
5. 玩家国和 AI 国的"⚔交战中" 数量应该明显减少（之前每个 AI 都跟玩家或别人战，现在 10 回合和平期让世界稍微"喘气"）

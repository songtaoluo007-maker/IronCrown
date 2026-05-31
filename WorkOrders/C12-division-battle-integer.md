# C12 — 师级团战 + 旅级战损 + BattleResolver 整数化

## 背景
C11 引入了"师 = 多旅"的数据结构，但战斗仍按"师整体扣 manpower/equipment/organization"运作，**没有旅级粒度**。同时 BattleResolver 的 float 公式从 T1 时代遗留至今——违反规则"Simulation 整数优先 + float 仅表现层"——是已记账技术债。

C12 三件一次做完：
1. **旅级战损**：战损扣到具体 brigade（某旅 manpower=0 → 从 brigades 移除 + RecalculateFromBrigades）
2. **师级团战伤害分摊**：sum 战力对决后，伤害按旅在师内的权重分摊
3. **BattleResolver float→int 整数化**：用整数百分比（×100 表示）替换 float 公式，符合确定性规则

C12 是 Phase 1 最大单——**经 Claude 设计批准的规则 9 例外重构**，由你拍板批准（见 §设计批准）。

## 范围

### 三件齐做
| 件 | 描述 |
|---|---|
| **旅级战损** | BrigadeState 加 TakeDamage(orgDmg, strDmg)；UnitState 战损不再直接扣自身字段，而是按旅权重分摊后调 RecalculateFromBrigades |
| **师级团战伤害分摊** | TickBattles 中：双方战力 sum 对决 → 总伤害按各师内 brigade 数权重分摊 → BrigadeState.TakeDamage → 旅 manpower≤0 移除 |
| **float→int 整数化** | BattleResolver 所有 float 改 int（百分比 ×100）：armorModifier / terrainMultiplier / combatRatio 全用整数算 |

### 不做（C13+）
- 兵种克制矩阵（步>炮>坦>步 循环）——C13 做
- 撤退（败方部队按邻接己方控制省撤回）——C14 做
- 师内旅独立属性 buff（如军官 / 经验等级）——C15+
- 跨师协同（多师同省战斗的旅级互动）——C9c 已有"多师同省加入" sum 战力即可
- 海空军参战——D 美术后

## 设计批准（规则 9 例外）

**重构边界**：本单触及 BattleResolver / Domain.Unit / Domain.BrigadeState 三个核心，**符合规则 9 大重构条件**（跨层 + 改公共契约——float→int 改 BattleResult？**不改 BattleResult 字段**，只改内部公式）。

**Claude 出方案 + 人类批准**：本工作单即方案。要点：
1. **BattleResult 接口不变**（attackerWon/defenderWon/draw bool 三态）——下游 TickBattles / Events 0 改动
2. **公式数值等价**（整数化后战斗结果在 ±5% 内与 float 版本一致）——可玩性不退化
3. **多对多团战不动**（C9c attackerUnitIds/defenderUnitIds 列表沿用）
4. **旅级战损不破坏师属性使用**（RecalculateFromBrigades 改变 unit.baseAttack 等，但下游 Resolver 不需关心）

人类批准后实施。

## 核心公式（整数化）

### 战力计算（int）
```csharp
// 单师战力（攻）
int singleUnitAttackPower(UnitState unit) {
    int orgPct = unit.organization * 100 / Math.Max(1, unit.maxOrganization);  // 0-100
    int expBonus = 100 + unit.experience * 10;                                  // 100-130
    return unit.baseAttack * orgPct * expBonus / 10000;                         // 缩放回原量级
}

// 多师团战攻方总战力
int teamAttackPower(List<UnitState> attackers, ProvinceState province) {
    int total = 0;
    foreach (var u in attackers) total += singleUnitAttackPower(u);
    int armorMod = CalculateArmorModifierInt(attackers, defenders);  // 返回 50/100/120
    return total * armorMod / 100;
}

// 守方总战力（含地形 terrain ×100 倍率）
int teamDefendPower(List<UnitState> defenders, ProvinceState province) {
    int total = 0;
    foreach (var u in defenders) {
        int basePower = singleUnitDefensePower(u);  // 用 baseDefense
        total += basePower;
    }
    int terrainMult = GetTerrainDefenseMultiplierInt(province.terrain);  // 100/110/115/120/125/130
    return total * terrainMult / 100;
}
```

### 战斗比（int）
```csharp
// combatRatio 改为 int × 100（如 1.5 → 150）
int combatRatioPct = teamAttackPower * 100 / Math.Max(1, teamDefendPower);
// 限制范围 [10, 1000]（0.1× 到 10×）
combatRatioPct = Math.Clamp(combatRatioPct, 10, 1000);
```

### 总伤害（每师每 tick）
```csharp
// 双方各自总伤害 = base × ratio mod
int totalAttackerOrgDmg = 10 * 100 / combatRatioPct;     // 攻方 base 10
int totalAttackerStrDmg =  5 * 100 / combatRatioPct;     // 攻方 base 5
int totalDefenderOrgDmg = 10 * combatRatioPct / 100;     // 守方 base 10
int totalDefenderStrDmg =  5 * combatRatioPct / 100;     // 守方 base 5

// 随机抖动 ±20%（已有 ApplyRandom 用 _rng.Range(-20, 20) 即可）
totalAttackerOrgDmg = ApplyRandomInt(totalAttackerOrgDmg, 20);
// ... 4 个 damage 都过 ApplyRandomInt
```

### 伤害分摊到各师 + 旅
```csharp
// 师权重 = brigade 总数（粗略反映师规模）
int totalAttackerBrigades = attackers.Sum(u => u.brigades.Sum(b => b.count));
foreach (var attacker in attackers) {
    int unitBrigadeCount = attacker.brigades.Sum(b => b.count);
    int unitOrgShare = totalAttackerOrgDmg * unitBrigadeCount / Math.Max(1, totalAttackerBrigades);
    int unitStrShare = totalAttackerStrDmg * unitBrigadeCount / Math.Max(1, totalAttackerBrigades);
    DistributeDamageToBrigades(attacker, unitOrgShare, unitStrShare, config);
}
// 守方同理
```

```csharp
// 单师内伤害分摊到 brigades（按 brigade 当前 manpower 占比）
void DistributeDamageToBrigades(UnitState unit, int orgDmg, int strDmg, IConfigRegistry config) {
    int totalManpower = unit.brigades.Sum(b => b.manpower);
    if (totalManpower <= 0) return;

    var toRemove = new List<BrigadeState>();
    foreach (var brigade in unit.brigades) {
        int weight = brigade.manpower * 100 / totalManpower;
        int brigadeOrgDmg = orgDmg * weight / 100;
        int brigadeStrDmg = strDmg * weight / 100;
        brigade.TakeDamage(brigadeOrgDmg, brigadeStrDmg);
        if (brigade.manpower <= 0 || brigade.equipment <= 0) toRemove.Add(brigade);
    }

    foreach (var dead in toRemove) unit.brigades.Remove(dead);

    // 师整体 organization 同步扣（与原 TakeDamage 等价）
    unit.organization = Math.Max(0, unit.organization - orgDmg);

    // 旅变化后重算师属性
    unit.RecalculateFromBrigades(config);
}
```

### BrigadeState.TakeDamage（新增）
```csharp
public void TakeDamage(int orgDmg, int strDmg) {
    // BrigadeState 无 organization 字段（师级才有），只扣 manpower/equipment
    manpower  = Math.Max(0, manpower  - strDmg);
    equipment = Math.Max(0, equipment - strDmg);
}
```

### 地形修正（int 化）
```csharp
private int GetTerrainDefenseMultiplierInt(TerrainType t) => t switch {
    TerrainType.Plain     => 100,
    TerrainType.Forest    => 110,
    TerrainType.Mountain  => 125,
    TerrainType.Hills     => 115,
    TerrainType.Urban     => 130,
    TerrainType.Swamp     => 120,
    TerrainType.River     => 120,
    _ => 100
};
```

### 装甲修正（int 化）
```csharp
private int CalculateArmorModifierInt(List<UnitState> attackers, List<UnitState> defenders) {
    int atkPiercing = attackers.Max(u => u.piercing);
    int defArmor    = defenders.Max(u => u.armor);
    if (defArmor > atkPiercing) return 50;   // 防穿透不足 ×0.5
    if (atkPiercing > defArmor) return 120;  // 防穿透优势 ×1.2
    return 100;                              // 平衡 ×1.0
}
```

### ResolveBattle 旧签名兼容
**保留** 旧 `ResolveBattle(UnitState attacker, UnitState defender, ProvinceState province)` 签名作为公共 API（向后兼容、C9 PlayMode 测试可能调用），内部转为单师 list 委托给 `ResolveMultiBattle(List<UnitState>, List<UnitState>, ProvinceState)`。

## 文件变更清单

### Domain
- `Domain/State/BrigadeState.cs` — 加 `TakeDamage(int, int)` 方法
- `Domain/Unit.cs` (`UnitState`) — 旧 `TakeDamage(int, int)` 方法**保留**但改为：① 调 DistributeDamageToBrigades（如果有 brigades）或 ② 走旧逻辑（如果 brigades 空，兼容旧档）
- `Domain/Province.cs` — 无变（TerrainType 枚举无变）

### Simulation
- `Simulation/BattleResolver.cs`：
  - 删除 `ResolveBattle` 内部 float 公式实现（`CalculateAttack/CalculateDefense/CalculateArmorModifier/GetTerrainDefenseMultiplier/GetSupplyModifier/ApplyRandom` 全部 float → int 版本）
  - 加 `ResolveMultiBattle(List<UnitState>, List<UnitState>, ProvinceState)` 师级团战核心
  - 加 `DistributeDamageToBrigades(UnitState, int, int, IConfigRegistry)` 私有
  - 旧 `ResolveBattle` 签名保留为 wrapper（单师列表委托）
  - `TickBattles` 改调 `ResolveMultiBattle`（既有循环结构不动）
  - `ApplyRandomInt(int baseValue, int variancePct)` 替换 `ApplyRandom(int, float)`
  - 注入 `IConfigRegistry _config`（已有，从 C5 起注入）传给 DistributeDamageToBrigades
- **不动** Application/MovementResolver/OccupationResolver/WarTollResolver/PeaceResolver/AiPeaceOfferResolver/AiRedeploymentResolver/EconomyResolver/PoliticsResolver/AIResolver/UnitProductionResolver/ConstructionResolver/VictoryConditionResolver

### Application
- 无变（BattleResolver 公共签名向后兼容、ActiveBattle 结构 C9c 已多对多）

### Contracts
- `BattleResolvedEvent.cs` — 无变
- `BattleConcludedEvent.cs` — 无变
- `UnitDestroyedEvent.cs` — 无变（旅级战损不发 UnitDestroyed，师消灭才发——保持事件粒度在师级）
- 可选新增 `BrigadeDestroyedEvent`（某旅 manpower≤0 移除时发）—— **本单不做**，避免事件刷屏

### Bootstrap
- 无变

### Data
- `economy.json` — 无变
- `units.json` — 无变
- `divisionTemplates.json` — 无变

### Tests
- `BattleResolverIntegerTests.cs`（新建，专测整数化）：
  - `IntegerArmorModifier_ArmorGreaterThanPiercing_Returns50`
  - `IntegerArmorModifier_PiercingGreaterThanArmor_Returns120`
  - `IntegerTerrainMultiplier_MountainReturns125`
  - `IntegerCombatRatio_ClampedAt10To1000`
  - `IntegerRandom_VarianceWithin20Percent`
- `BattleResolverDivisionBattleTests.cs`（新建，专测旅级战损）：
  - `DistributeDamage_ToMultipleBrigades_WeightedByManpower`
  - `Brigade_ManpowerZero_RemovedFromUnit`
  - `BrigadeRemoved_UnitRecalculatesAttributes`
  - `AllBrigadesRemoved_UnitShattered`
  - `MultiUnitBattle_DamageSharedByBrigadeCount`
- `BattleResolverC3Tests.cs` / `C4Tests.cs` / `C5Tests.cs` / `C9cTests.cs` 全部回归：旧 1v1 / 多对多用例应在整数公式下仍通过（结果可能 ±5% 误差，断言用 InRange 而非 AreEqual）
- `SaveLoadEquivalenceTests.cs` — `SaveLoad_DivisionWithBrigades_Preserved`（C11 已有）回归
- `ConfigValidationTests.cs` — 无追加

## DoD Check List
- [ ] BattleResolver float→int 重构完成，**所有 float 字段/公式全部整数化**（grep `float` 在 BattleResolver.cs 应为 0）
- [ ] ResolveMultiBattle 实现 + 多师团战伤害分摊到旅
- [ ] DistributeDamageToBrigades 内部按 brigade.manpower 权重分摊
- [ ] BrigadeState.TakeDamage 方法实现 + 旅 manpower/equipment ≤ 0 时被移除
- [ ] UnitState 旧 TakeDamage 保留兼容（brigades 空时走旧路径、brigades 非空走 DistributeDamageToBrigades）
- [ ] 旧 ResolveBattle(UnitState, UnitState, ProvinceState) 保留为单师 list wrapper
- [ ] BattleResult 三态字段 + Events 既有签名 0 改动
- [ ] 既有 247 测试 + 本单新增（约 10 个）全绿，**回归测试整数误差 ±5% 内**
- [ ] artifacts/c12-editmode.xml + c12-playmode.xml 归档
- [ ] **Play 截图 3 张**（10+ 单累积 0 截图老问题，本单强制）：
  - `c12-multi-division-battle.png`（两支师对决，详情看到双方旅数）
  - `c12-brigade-loss.png`（战斗多回合后某旅消灭、师属性下降）
  - `c12-armor-vs-soft.png`（装甲师 vs 步兵师战斗，armor 修正生效）
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 0 failed（**不接受"老问题"借口，全失败必修**）
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md / .gitignore 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] grep `float` BattleResolver.cs 应**仅出现在注释**或 0 次
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 严禁
- 改 BattleResult 字段定义（攻胜/守胜/平局 三态保留）
- 改 BattleResolved/BattleConcluded/UnitDestroyed/ProvinceOccupied 事件签名
- 改 ActiveBattle 数据结构（C9c 多对多已稳）
- 改 ResolveBattle / TickBattles / InitiateAttack / ResolveAttackCommand 公共签名
- 改 EconomyResolver / WarTollResolver / PeaceResolver / AiPeaceOfferResolver / OccupationResolver / VictoryConditionResolver / MovementResolver / AiRedeploymentResolver / AIResolver / UnitProductionResolver / ConstructionResolver 任何公式
- 引入兵种克制矩阵（C13 做）
- 加旅级独立属性（buff / debuff）
- 加撤退机制
- 用 commit log / txt 替代 Play 截图

## 歧义处理
- **回归测试整数误差容忍**：C3/C4/C5/C9c 既有用例可能因 float→int 精度损失出现 ±1-5% 结果差异。**写死**：用 `Assert.That(value, Is.InRange(min, max))` 替代 `Assert.AreEqual` 容忍 ±5%。**核心三态判定**（attackerWon/defenderWon/draw）不允许漂移——必须仍按相同初始条件得相同胜方。
- **旅级战损与师级 organization 关系**：本单 UnitState.organization 仍是师级字段（不是旅级）。旅级战损扣 manpower/equipment，师级 organization 由总伤害扣。师 organization=0 = 师溃散 = brigades 仍可能存在但师 shattered。**写死**：IsShattered 判定仍按 organization≤0（与既有逻辑一致）。
- **旅 manpower=0 但 equipment>0 或反之**：本单**任一**为 0 即移除（合理：无人 = 无战力）。
- **DistributeDamageToBrigades 浮点偏差**：整数除法可能让"分摊 100 伤害给 3 旅"得到 33/33/33=99（少 1）。**写死**：少的 1 给第一个 brigade（按 brigades 列表顺序）。
- **多师团战中某师所有旅死光**：师 brigades.Count == 0 → 师整体扣到 organization=0 → IsShattered → TickBattles 收尾 DestroyUnit 移除。**写死**：DistributeDamageToBrigades 内部若 brigades 全死，强制把 unit.organization 设为 0。
- **TerrainType.Coastline / Desert / Jungle 缺失**：现有 BattleResolver float 版只覆盖 Plain/Forest/Mountain/Hills/Urban/Swamp/River。Coastline/Desert/Jungle 走 default 100。**本单写死沿用**——这些地形修正留 C13+。
- **整数化导致某些回归测试可能小数误差超 5%**：若超 5% 但三态判定仍对，记到 PR 描述供 Claude 审查；若三态翻转 = bug 必修。

## 完工后人类 Play 验证清单
1. 训练 "基础步兵师" + "装甲师" 各 1 支
2. 让两师同时进攻同一邻省 → 看 ActiveBattle 详情 "攻 2 师 vs 守 1 师"（C9c 多对多 + C11 师概念 + C12 整数公式协同）
3. 推 N 回合战斗 → 某 brigade 应被消灭（详情栏 brigadeSummary 从 "9 步兵 + 3 炮兵" 变 "8 步兵 + 3 炮兵"）
4. 装甲师攻步兵师 vs 步兵师攻装甲师，结果应明显不同（armor/piercing 修正生效）
5. 整数化后战斗结果在多回合大致与之前 float 版本相似（不应出现"原本必胜变必败"的剧变）

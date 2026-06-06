# C14 — SupplyResolver 完整化 + 切断/包围/解围/夹击

## 背景
C13 把 SupplyResolver 激活做了**补员**，但 `isCutoff` 字段一直默认 false（任何 unit 都视为有补给）。C14 补 BFS 补给链计算，激活切断/包围/解围/夹击四件套——HoI4 风格战略层的核心。

时间尺度：1 回合 = 1 月。所有数值按月节奏。

## 范围

### 四件齐做
| 件 | 描述 |
|---|---|
| **BFS 补给链** | 每回合 Settlement 早段计算每 unit 是否能到本国首都（路径上所有省 controllerCountry == ownerCountry）→ 写入 `isCutoff` |
| **切断衰减** | `isCutoff=true` 的 unit 每回合 manpower -30 点 + equipment -30 点（减法）+ 不补员 |
| **解围 (A 方案)** | 上回合 isCutoff=true、本回合 BFS 重连 → 自动解围：双条立即 +25、recoveryTurnsLeft=0、发 `UnitReliefEvent` |
| **夹击 + 包围** | 邻接敌控省 ≥ 2 = 夹击 → morale -10/回合；全邻接敌控 + isCutoff = 完全包围 → 上述叠加 + morale -20 |
| **disorganized 状态** | 双条任一 ≤ 25% → `isDisorganized=true` → 不能下新命令（攻击/调度都拒） |

### 配置（economy.json）
```json
{
  "cutoffManpowerDecayPerTurn": 30,
  "cutoffEquipmentDecayPerTurn": 30,
  "disorganizedThresholdPct": 25,
  "reliefBonusManpower": 25,
  "reliefBonusEquipment": 25,
  "flankingNeighborThreshold": 2,
  "flankingMoralePenaltyPerTurn": 10,
  "encirclementMoralePenaltyPerTurn": 20
}
```

### 配套数学验算（你之前定的 4 回合死亡窗口）
```
切断状态、起点 manpower=100、equipment=100:
  回合 1 (切断首回合): 100 → 70 / 100 → 70
  回合 2:              70 → 40 /  70 → 40
  回合 3:              40 → 10 /  40 → 10  ← 已 ≤ 25 触发 disorganized
  回合 4:              10 → -20 = 消灭
```
✓ 4 回合救援窗口对齐。

```
解围奖励 +25 双条:
  原 (10, 10) 残部 + 解围 → (35, 35) 恢复战力 + 不再 disorganized
```

## BFS 补给链算法

```csharp
public void UpdateSupplyStatus(WorldState world):
  foreach country in world.countries.Values:
    if (country.capitalProvinceId == null) continue;
    var capitalProvince = world.provinces.GetValueOrDefault(country.capitalProvinceId);
    if (capitalProvince == null || capitalProvince.controllerCountry != country.id) {
      // 首都丢失 = 全国所有 unit 视为切断
      foreach unit owned by country: unit.isCutoff = true;
      continue;
    }

    // BFS 从首都出发，遍历己方控制的连通省份集
    var reachable = new HashSet<string>{ country.capitalProvinceId };
    var queue = new Queue<string>{ country.capitalProvinceId };
    while (queue.Count > 0):
      var pid = queue.Dequeue();
      var prov = world.provinces[pid];
      foreach nb in prov.neighbors:
        if (reachable.Contains(nb)) continue;
        var nbProv = world.provinces.GetValueOrDefault(nb);
        if (nbProv == null || nbProv.controllerCountry != country.id) continue;
        reachable.Add(nb);
        queue.Enqueue(nb);

    // 标记每个 unit
    foreach unit owned by country:
      bool wasCutoff = unit.isCutoff;
      bool isNowCutoff = !reachable.Contains(unit.currentProvinceId);
      unit.isCutoff = isNowCutoff;

      if (wasCutoff && !isNowCutoff) {
        // 上回合切断、本回合解围 → 触发解围奖励
        unit.manpower  = Math.Min(unit.maxManpower,  unit.manpower  + eco.reliefBonusManpower);
        unit.equipment = Math.Min(unit.maxEquipment, unit.equipment + eco.reliefBonusEquipment);
        unit.recoveryTurnsLeft = 0;  // 解围立刻取消修整中
        _events.Publish(new UnitReliefEvent { unitId = unit.id, reliefedAtTurn = world.turnNumber });
      }
```

## 切断衰减（SupplyResolver.ReplenishUnits 扩展）

```csharp
foreach unit:
  // 既有 C13 补员路径（前置 isCutoff 检查）
  if (unit.isCutoff) {
    // 切断 → 不补员，反扣
    unit.manpower  = Math.Max(0, unit.manpower  - eco.cutoffManpowerDecayPerTurn);
    unit.equipment = Math.Max(0, unit.equipment - eco.cutoffEquipmentDecayPerTurn);
    // 旅级同步衰减（按 brigade.manpower 占比分摊）
    DistributeDecayToBrigades(unit, eco.cutoffManpowerDecayPerTurn, eco.cutoffEquipmentDecayPerTurn);
    unit.RecalculateFromBrigades(config);
    // manpower / equipment 归 0 时 → DestroyUnit
    if (unit.manpower <= 0 || unit.equipment <= 0) UnitDestroyer.Destroy(world, unit.id, "starved");
    continue;
  }

  // 既有补员逻辑（C13 不变）
  ...
```

## 夹击 + 包围 morale

```csharp
foreach unit:
  if (unit.currentProvinceId == null) continue;
  var prov = world.provinces[unit.currentProvinceId];
  int enemyNeighbors = 0;
  int totalNeighbors = prov.neighbors?.Length ?? 0;
  foreach nb in prov.neighbors:
    var nbProv = world.provinces.GetValueOrDefault(nb);
    if (nbProv != null && nbProv.controllerCountry != unit.ownerCountry) enemyNeighbors++;

  if (unit.isCutoff && totalNeighbors > 0 && enemyNeighbors == totalNeighbors) {
    // 完全包围 = 切断 + 全邻接敌
    unit.morale = Math.Max(0, unit.morale - eco.encirclementMoralePenaltyPerTurn);  // -20
  } else if (enemyNeighbors >= eco.flankingNeighborThreshold) {
    // 夹击 = ≥2 邻接敌
    unit.morale = Math.Max(0, unit.morale - eco.flankingMoralePenaltyPerTurn);  // -10
  }
```

## disorganized 状态

```csharp
// SupplyResolver.UpdateSupplyStatus 末尾 / 或单独 RecomputeDisorganized 方法
foreach unit:
  int manpowerPct  = unit.maxManpower  > 0 ? unit.manpower  * 100 / unit.maxManpower  : 0;
  int equipmentPct = unit.maxEquipment > 0 ? unit.equipment * 100 / unit.maxEquipment : 0;
  unit.isDisorganized = manpowerPct <= eco.disorganizedThresholdPct
                     || equipmentPct <= eco.disorganizedThresholdPct;
```

**IssueCommand 入口加守卫**：
```csharp
if (cmd.commandType == CommandType.MoveUnit && 
    _world.units.TryGetValue(cmd.unitId, out var unit) && unit.isDisorganized) {
  return CommandResult.Reject("部队混乱中无法行动");
}
```

AI 同理 (AIResolver.TryAttack 跳过 isDisorganized=true 的师)。

## TurnResolver 调用顺序（写死）

```
ExecuteSettlement:
  EconomyResolver.ResolveEconomy
  UnitProductionResolver.ResolveProduction
  ConstructionResolver.ResolveConstruction
  BattleResolver.TickBattles               (C3+ 含 C12 整数化)
  ★ SupplyResolver.UpdateSupplyStatus     (C14 新增 — BFS + isCutoff + 解围奖励)
  ★ SupplyResolver.ApplyMoraleEffects     (C14 新增 — 夹击/包围)
  SupplyResolver.ReplenishUnits            (C13 既有 — 切断改扣血、其他正常补员)
  ★ SupplyResolver.RecomputeDisorganized  (C14 新增 — 写 isDisorganized)
  WarTollResolver.ApplyTurnToll
  VictoryConditionResolver.CheckVictory
```

★ = C14 新增调用。**先算 isCutoff → 再算 morale → 再 replenish/decay → 最后 disorganized 状态**。

## 文件变更清单

### Domain
- `UnitState` (Unit.cs) — 加 `bool isDisorganized;`（默认 false）
- `EconomyConfig.cs` — 加 8 个 C14 字段

### Simulation
- `SupplyResolver.cs`:
  - 加 `UpdateSupplyStatus(world)` —— BFS 补给链 + 写 isCutoff + 解围奖励
  - 加 `ApplyMoraleEffects(world, eco)` —— 夹击/包围 morale
  - 改 `ReplenishUnits(world, eco)` —— 加 isCutoff 分支（切断扣血代替补员）
  - 加 `RecomputeDisorganized(world, eco)` —— 写 isDisorganized
- `TurnResolver.cs` — ExecuteSettlement 加 3 个 Supply* 调用，按上述顺序

### Application
- `GameSessionService.IssueCommand` — MoveUnit 分支加 isDisorganized 拒绝
- `ReadModelBuilder` — UnitView 加 `isCutoff` + `isDisorganized`
- `Persistence/SaveModels.cs` — UnitSaveData 加 `isDisorganized`
- `Mapping/SaveMapper.cs` — 双向 + HashWorld 扩 isDisorganized（isCutoff C13 已扩）

### Contracts
- `UnitView.cs` — 加 `bool isCutoff; bool isDisorganized;`
- `Contracts/Events/UnitReliefEvent.cs`（新）— `{ unitId, reliefedAtTurn }`
- `Contracts/Events/UnitStarvedEvent.cs`（新）— `{ unitId, atTurn }`（切断饿死时发，可选）

### Data
- `economy.json` — 加 8 个 C14 字段

### Presentation
- `MainHudController`:
  - 详情栏选中部队（如该省含部队）显示 `补给: ✓ / 切断 / 包围`
  - 状态栏订阅 `UnitReliefEvent` → `🕊 {unitId} 解围成功，补给恢复`
  - 状态栏订阅 `UnitStarvedEvent` → `💀 {unitId} 因切断补给消亡`
  - 地图省份样式：含 isCutoff 的友方部队 → tile 加 USS class `.province-tile-cutoff`（红色虚线边框）
- `MainHud.uss` — `.province-tile-cutoff { border-style: dashed; border-color: rgba(220, 80, 80, 0.9); }`

### Tests
- 新建 `SupplyResolverBfsTests.cs`：
  - `UpdateSupplyStatus_ConnectedUnit_NotCutoff`
  - `UpdateSupplyStatus_IsolatedUnit_Cutoff`
  - `UpdateSupplyStatus_PathBlockedByEnemyProvince_Cutoff`
  - `UpdateSupplyStatus_CapitalLost_AllUnitsCutoff`
  - `UpdateSupplyStatus_PreviouslyCutoffNowConnected_TriggersRelief`
  - `Relief_AddsBonusManpowerAndEquipment_PublishesEvent`
  - `Relief_CancelsRecoveryTurnsLeft`
- 新建 `SupplyDecayTests.cs`：
  - `ReplenishUnits_CutoffUnit_LosesManpower30`
  - `ReplenishUnits_CutoffUnit_LosesEquipment30`
  - `ReplenishUnits_CutoffUnitManpowerToZero_Destroyed`
  - `ReplenishUnits_FourTurnsFromFullToZero`（4 回合死亡窗口验证）
- 新建 `MoraleEffectsTests.cs`：
  - `ApplyMoraleEffects_TwoEnemyNeighbors_FlankingPenalty10`
  - `ApplyMoraleEffects_OneEnemyNeighbor_NoPenalty`
  - `ApplyMoraleEffects_AllEnemyNeighborsAndCutoff_EncirclementPenalty20`
  - `ApplyMoraleEffects_MoraleClampedAtZero`
- 新建 `DisorganizedTests.cs`：
  - `RecomputeDisorganized_ManpowerBelowThreshold_IsDisorganized`
  - `RecomputeDisorganized_EquipmentBelowThreshold_IsDisorganized`
  - `RecomputeDisorganized_BothAboveThreshold_NotDisorganized`
  - `IssueCommand_MoveUnit_DisorganizedUnit_Rejected`
- `SaveLoadEquivalenceTests.cs` 追加 `SaveLoad_IsCutoffAndDisorganized_Preserved` + HashWorld 扩 isDisorganized
- `ConfigValidationTests.cs` 追加 `Economy_HasC14Fields`

## DoD Check List
- [ ] SupplyResolver 4 个方法实现（UpdateSupplyStatus / ApplyMoraleEffects / ReplenishUnits 改 / RecomputeDisorganized）
- [ ] TurnResolver.ExecuteSettlement 调用顺序按 §写死
- [ ] UnitState +isDisorganized + SaveMapper 双向
- [ ] IssueCommand isDisorganized 拒绝
- [ ] 既有 269 测试 + 本单新增（约 20 个）全绿
- [ ] artifacts/c14-editmode.xml + c14-playmode.xml 归档（**不接受 10+ 中间 log 散落**，本单整理只留最终一对）
- [ ] **Play 截图 3 张强制**（13 单累积 0 张已是组织性顽疾，本单破纪录）：
  - `c14-cutoff-province.png`（被切断省份红虚线边框）
  - `c14-relief-event.png`（推回合解围瞬间状态栏 🕊 + 双条恢复）
  - `c14-encirclement-death.png`（4 回合从满血到饿死的某帧）
- [ ] **★★ commit 完成后立即 `git push origin feature/c5-diplomacy-peace`**（PR #1 自动追加。规则 §PROJECT_STATE §5 第 2 条，C12/C13 已连破 2 次，本单写入 DoD 硬约束、不接受"忘了"借口）
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 0 failed
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md / .gitignore 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] 不动 BattleResolver / EconomyResolver / WarTollResolver / PeaceResolver / AiPeaceOfferResolver / OccupationResolver / VictoryConditionResolver / AIResolver / ConstructionResolver / UnitProductionResolver / AiRedeploymentResolver / MovementResolver / WarRegistry / PoliticsResolver 公式（**仅 SupplyResolver 改 + TurnResolver 调用顺序**）
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 不做（C15+）
- 将军卡 / 军衔 / 集团军 — C15
- 抽卡养成 — C16
- 后勤补给消耗（如部队每回合消耗 food）— C18+
- 港口 / 铁路 / 卡车 — 永远不做（轻量化承诺）
- 海运 / 跨海补给 — D 美术后
- 多省 BFS 优化（C14 起步用全图 BFS，6 省无性能问题）— Phase 4 扩省时再优化

## 严禁
- 改 BattleResolver TickBattles / ResolveMultiBattle（C12 整数化稳定）
- 改 EconomyResolver / WarTollResolver / PeaceResolver 公式
- 改 ActiveBattle 数据结构
- 自动判定夹击效果叠加战斗 buff（夹击只扣 morale，不扣战斗 attack/defense — C18+ 再考虑战场夹击战力修正）
- 用 commit log / txt 替代 Play 截图
- **★ 完成不 push（已连破 2 次，本单零容忍）**

## 歧义处理
- **isCutoff 起始**：游戏开始 World Initializer 后所有 unit 默认 isCutoff=false。第一回合 Settlement 跑 UpdateSupplyStatus 时才真正算
- **首都被占瞬间**：当回合所有 unit 立刻 isCutoff=true（下回合开始衰减）
- **首都被光复**：UpdateSupplyStatus 重新 BFS、能到首都的 unit 自动解围
- **多军同省 + 部分能补给到首都**：所有该省的 unit 都按"该省是否能到首都" 判 isCutoff
- **disorganized 拒命令是否包括 IssueCommand 其他类型**：本单**仅拒 MoveUnit/OfferPeace**（攻击/移动/谈判都禁），不拒 BuildCivilianFactory（建厂不依赖单兵），允许 SetTaxLevel / SetCivilLevel（内政档不依赖单兵）。**写死**
- **解围奖励是否含 morale 重置**：本单**不重置 morale**——morale 在 ApplyMoraleEffects 自然恢复（无 enemy neighbors 时不下降，自然 +1/回合？**C14 不加自然恢复**，留 C15 + 将军技能"激励"加）
- **夹击 + 包围 morale 互斥还是叠加**：写死**互斥**（完全包围只算包围 -20 不再加夹击 -10）
- **isDisorganized 是 OR（双条任一 ≤25%）**：写死 OR（既人力低又装备低中任一发生就混乱）

## 完工后人类 Play 验证清单
1. 玩家深入敌后，攻下一个孤立省 → 推 1 回合 → 该 unit 详情应显示"切断"+ 地图省份红虚线边框
2. 继续推 4 回合不解围 → unit 双条逐步降到 0 → 消亡 + 状态栏 💀
3. 玩家把另一支部队打通到该省（重建补给链）→ 下回合 UpdateSupplyStatus 重算 → 切断 unit 解围 + 状态栏 🕊 + 双条立即 +25
4. 邻接 2+ 敌控省的玩家 unit → 推回合后 morale 应下降 10
5. 完全包围（全邻接敌 + 切断）unit → morale 下降 20
6. unit 双条 ≤ 25% → 详情显"混乱" + 点其下命令拒"部队混乱中无法行动"

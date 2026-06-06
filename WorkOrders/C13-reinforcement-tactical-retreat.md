# C13 — HoI4 双条补员 + 战役等级 + 85% 自动溃退

## 背景
C12 团战整数 + 旅级战损做完后，师战损没有恢复机制——师慢慢被消耗光、战略持续性差。C13 引入 **HoI4 风格双条补员**（绿条人力 / 黄条装备）+ **战役等级**（师独立经验，与军衔分离）+ **85% 人力损失自动溃退**（残部保留机制）。

时间尺度：1 回合 = 1 月。

## 范围

### 三件齐做
| 件 | 描述 |
|---|---|
| **双条补员** | 每回合 Settlement 末尾，师 manpower < max 时从国家 manpower 池按 50% 缺口补；equipment 同理从 equipmentStockpile 补 |
| **战役等级** | UnitState 加 `tacticalExp` (0-100)；战胜+10、败-5；每 25 经验升 1 阶（共 4 阶）；每阶 +5% attack+defense |
| **85% 自动溃退** | 战斗 tick 后 manpower ≤ 15% maxManpower 触发：撤退到后方己方控制省 + 立即补 +20 双条 + morale 重置 30 + `recoveryTurnsLeft=1`（**可移动不可进攻**） |

### 配置（economy.json）
```json
{
  "reinforceRatePct": 50,
  "tacticalExpPerVictory": 10,
  "tacticalExpPerDefeat": -5,
  "tacticalExpLevelStep": 25,
  "tacticalExpAttackBonusPerLevel": 5,
  "tacticalExpDefenseBonusPerLevel": 5,
  "autoRetreatThresholdPct": 15,
  "retreatBonusManpower": 20,
  "retreatBonusEquipment": 20,
  "retreatMoraleReset": 30,
  "retreatRecoveryTurns": 1
}
```

### 战役等级公式
```
level = min(4, tacticalExp / tacticalExpLevelStep)   // 0/25/50/75/100 → level 0/1/2/3/4
attackBonusPct  = level × tacticalExpAttackBonusPerLevel   // 0/5/10/15/20%
defenseBonusPct = level × tacticalExpDefenseBonusPerLevel  // 0/5/10/15/20%
```

战斗时（BattleResolver C12 整数公式）攻方战力额外 ×(100 + attackBonusPct) / 100，守方同理。

### 双条补员公式
```csharp
每回合 Settlement 末尾对每个 unit:
  if (recoveryTurnsLeft > 0) recoveryTurnsLeft--;   // 溃退倒计时
  if (该 unit 在切断状态) continue;                  // C14 才有切断，C13 不阻塞
  
  // 人力补员
  int manpowerNeeded = unit.maxManpower - unit.manpower;
  int manpowerFromPool = manpowerNeeded * eco.reinforceRatePct / 100;
  int actualManpower = Math.Min(manpowerFromPool, country.manpower);  // 国家池不足慢补
  unit.manpower += actualManpower;
  country.manpower -= actualManpower;
  
  // 装备补员（同理走 equipmentStockpile）
  int equipmentNeeded = unit.maxEquipment - unit.equipment;
  int equipmentFromPool = equipmentNeeded * eco.reinforceRatePct / 100;
  int actualEquipment = Math.Min(equipmentFromPool, country.equipmentStockpile);
  unit.equipment += actualEquipment;
  country.equipmentStockpile -= actualEquipment;
  
  // 旅级同步补员（按 brigade.manpower 占比分摊到各 brigade、重算 unit 字段）
  if (actualManpower > 0 || actualEquipment > 0) {
    DistributeReinforcementToBrigades(unit, actualManpower, actualEquipment);
    unit.RecalculateFromBrigades(config);
  }
```

### 自动溃退判定（BattleResolver.TickBattles 收尾扩展）
```csharp
TickBattles 每个 battle 处理后 (在 BattleConcluded 之前):
  对 attackerUnitIds 和 defenderUnitIds 中每个仍存在的 unit:
    if (unit.manpower * 100 <= unit.maxManpower * eco.autoRetreatThresholdPct) {
      // 已被切断不溃退（C14 用 unit.isCutoff 字段判断；C13 此字段默认 false）
      if (unit.isCutoff) continue;  // 切断 → 不溃退、只能死
      
      string retreatProvinceId = FindRetreatProvince(unit, world);
      if (retreatProvinceId == null) {
        // 无可达后方 → 视同消灭（与切断一致）
        UnitDestroyer.Destroy(world, unit.id, "no_retreat_path");
        // 从 attackerUnitIds / defenderUnitIds 移除
        continue;
      }
      
      // 执行溃退
      unit.currentProvinceId = retreatProvinceId;
      unit.manpower += eco.retreatBonusManpower;
      unit.equipment += eco.retreatBonusEquipment;
      unit.morale = eco.retreatMoraleReset;  // 30
      unit.recoveryTurnsLeft = eco.retreatRecoveryTurns;  // 1
      // 从 battle 的 attacker/defender 列表移除（退出战斗）
      battle.attackerUnitIds.Remove(unit.id);
      battle.defenderUnitIds.Remove(unit.id);
      _events.Publish(new UnitRetreatedEvent { unitId = unit.id, retreatProvinceId = retreatProvinceId });
    }
```

### FindRetreatProvince 算法
```csharp
// 找"己方控制 + 邻接当前省 + 距离己方首都最近"的省
// BFS 简化版：直接遍历邻接省
1. 取 unit.currentProvinceId 的 neighbors
2. 过滤：controllerCountry == unit.ownerCountry 的省
3. 若空集 → 返回 null
4. 否则按"BFS 距首都步数升序"取第一个（C13 简化：取列表里 id 升序第一个，C14 引入完整 BFS）
5. 返回该省 id
```

C13 简化：邻接己方控制省按 id 升序首位。C14 替换为"距首都最近"路径计算。

### 溃退期间命令限制
**IssueCommand 入口**加：
```csharp
if (cmd.commandType == CommandType.MoveUnit && 
    _world.units.TryGetValue(cmd.unitId, out var unit) &&
    unit.recoveryTurnsLeft > 0) {
  // 检查目标省是否敌方控制 → 拒进攻、允许撤退转移
  if (_world.provinces.TryGetValue(cmd.targetProvinceId, out var target) 
      && target.controllerCountry != unit.ownerCountry) {
    return CommandResult.Reject("溃退中无法进攻（仅可移动到己方省）");
  }
}
```
- 玩家可以继续把溃退师往后撤（己方控制省间移动）
- 玩家不能让溃退师进攻敌省
- AI 同理走 AIResolver 内部 (TryAttack 跳过 recoveryTurnsLeft > 0 的师)

### 战役经验累积
**BattleResolver.TickBattles** 在 BattleConcluded 时：
```csharp
result.attackerWon → 对所有 attackerUnitIds 中存活的 unit: unit.tacticalExp += eco.tacticalExpPerVictory  // +10
result.defenderWon → 对所有 defenderUnitIds 中存活的 unit: unit.tacticalExp += eco.tacticalExpPerVictory  // +10
                  → 对所有 attackerUnitIds 中存活的 unit: unit.tacticalExp += eco.tacticalExpPerDefeat   // -5（负数）
                  (defenderWon 攻方扣经验)
result.draw       → 两边不动
unit.tacticalExp = Math.Clamp(unit.tacticalExp, 0, 100);
```

战役等级 = `unit.tacticalExp / 25`（0-4 范围，无字段、运行时计算）。

战斗中战力 buff 由 ResolveMultiBattle 计算单师战力时叠加：
```csharp
int singleUnitAttackPower(UnitState unit, EconomyConfig eco) {
    int orgPct = unit.organization * 100 / Math.Max(1, unit.maxOrganization);
    int level = unit.tacticalExp / eco.tacticalExpLevelStep;  // 0-4
    int levelBonus = 100 + level * eco.tacticalExpAttackBonusPerLevel;  // 100-120
    return unit.baseAttack * orgPct * levelBonus / 10000;  // 缩放
}
```

## 文件变更清单

### Domain
- `UnitState`（Unit.cs）— 加 `int tacticalExp;` + `int recoveryTurnsLeft;` + `bool isCutoff` (默认 false, C14 才填)
- `Domain/Config/EconomyConfig.cs` — 加 9 个 C13 字段

### Simulation
- `Simulation/BattleResolver.cs` — TickBattles 收尾扩展：① 战役经验累积 ② 85% 自动溃退判定；ResolveMultiBattle 战力计算加 levelBonus
- `Simulation/SupplyResolver.cs` — **激活既有 stub**：新建 `ReplenishUnits(world, eco)` 每回合 Settlement 末尾调（C13 不动 BFS，C14 才补全）
- `Simulation/TurnResolver.cs` — ExecuteSettlement 末尾调 `_supply.ReplenishUnits(world, eco)`（在 VictoryConditionResolver 之前，避免溃退后判胜负）

### Application
- `GameSessionService.cs` — IssueCommand MoveUnit 分支加溃退期间禁攻击校验
- `ReadModelBuilder.cs` — UnitView 加 `tacticalExp / tacticalLevel / recoveryTurnsLeft`
- `Persistence/SaveModels.cs` — UnitSaveData 加 `tacticalExp / recoveryTurnsLeft / isCutoff`
- `Mapping/SaveMapper.cs` — 双向 + HashWorld 扩 3 字段

### Contracts
- `UnitView.cs` — 加 `int tacticalExp; int tacticalLevel; int recoveryTurnsLeft; bool isRecovering;`
- `Contracts/Events/UnitRetreatedEvent.cs`（新）—— `{ unitId, fromProvinceId, retreatProvinceId, turnNumber }`
- `Contracts/Events/UnitReinforcedEvent.cs`（新）—— `{ unitId, manpowerGained, equipmentGained }`（可选，避免事件刷屏，C13 不发也可）

### Data
- `economy.json` — 加 9 个 C13 字段

### Presentation
- `MainHudController.cs`:
  - HUD 国家行追加 `manpowerPool` 池子（已有 `country.manpower` 字段但未显示）
  - 详情栏选中部队的省份显示 `战役等级: L级 (T/100)`（如 `战役: 2级 (62/100)`）
  - 溃退中部队详情显 `溃退修整中（恢复 N 回合）`
  - 订阅 UnitRetreatedEvent → 状态栏 `🏃 {unitId} 撤退至 {provinceId}`
- `MainHud.uss` — 加 `.unit-recovering { color: rgba(255, 160, 80, 1); }` 橙色文字

### Tests
- 新建 `ReinforcementResolverTests.cs`：
  - `Replenish_FullManpower_NoChange`
  - `Replenish_HalfManpower_FillsHalfOfGap`（50% 缺口补员）
  - `Replenish_PoolEmpty_NoReplenishment`
  - `Replenish_EquipmentSameAsManpower`
  - `Replenish_BrigadeWeighted_AllBrigadesGetShare`
- 新建 `AutoRetreatTests.cs`：
  - `TickBattles_UnitBelow15PercentManpower_RetreatsToAdjacentFriendlyProvince`
  - `TickBattles_UnitBelow15PercentManpower_NoFriendlyNeighbor_Destroyed`
  - `Retreated_RecoveryTurnsLeftDecrementsEachTurn`
  - `Retreated_TryAttackEnemy_Rejects`（IssueCommand 拒）
  - `Retreated_MoveToFriendly_Allowed`
- 新建 `TacticalExpTests.cs`：
  - `BattleVictory_AttackerGainsExp`（+10）
  - `BattleDefeat_AttackerLosesExp`（-5）
  - `TacticalLevel_AtExp50_IsLevel2`
  - `BattleStrength_Level2_Has10PercentBonus`
- `BattleResolverDivisionBattleTests.cs`（C12）— 回归：战力公式 + 战役加成叠加
- `SaveLoadEquivalenceTests.cs` — `SaveLoad_TacticalExpAndRecovery_Preserved`（HashWorld 扩 3 字段）
- `ConfigValidationTests.cs` — `Economy_HasC13Fields`

## DoD Check List
- [ ] SupplyResolver.ReplenishUnits 实现，每回合 Settlement 末尾调用
- [ ] BattleResolver.TickBattles 收尾扩展：经验累积 + 85% 自动溃退判定
- [ ] BattleResolver.ResolveMultiBattle 战力公式加 tacticalLevel buff
- [ ] GameSessionService.IssueCommand 加溃退禁攻击校验
- [ ] UnitState 新 3 字段 + SaveMapper 双向 + HashWorld 扩
- [ ] HUD/详情栏/事件订阅完整
- [ ] 既有 247 测试 + 本单新增（约 13 个）全绿
- [ ] artifacts/c13-editmode.xml + c13-playmode.xml 归档
- [ ] **Play 截图 3 张强制**：
  - `c13-reinforcement.png`（战损师推 N 回合后双条恢复）
  - `c13-tactical-level.png`（多场战斗后某师显 `战役: 2级 (60/100)`）
  - `c13-auto-retreat.png`（战斗中某师 manpower 10/100 → 撤退到邻接己方省、状态显"溃退修整中"）
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 0 failed
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] 不动 EconomyResolver / MovementResolver / OccupationResolver / WarTollResolver / PeaceResolver / AiPeaceOfferResolver / VictoryConditionResolver / AIResolver / ConstructionResolver / UnitProductionResolver / AiRedeploymentResolver / PoliticsResolver / WarRegistry 公式
- [ ] BattleResolver float→int (C12) 不退化
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 不做（C14+）
- BFS 补给链 / isCutoff 真激活（C13 字段保留默认 false）→ C14
- 切断 -30 衰减 / disorganized 状态 / 解围 +25 → C14
- 将军卡 / 军衔系统 / 集团军 → C15
- 抽卡养成 → C16
- 旅级独立属性（个旅独立 morale）→ C20+
- 师内补员优先级（先补哪个旅）→ 按 manpower 权重平均，无优先级

## 严禁
- 改 BattleResolver float→int 公式（C12 整数化结果不退化）
- 改 EconomyResolver / WarTollResolver / PeaceResolver 既有公式
- 改 ActiveBattle 数据结构（C9c/C11/C12 多对多+旅级已稳）
- 改既有 BattleResult / 既有 Event 签名
- 跳过指派测试 / 用 commit log 代替截图

## 歧义处理
- **补员顺序**：先补 manpower 还是 equipment？**写死**：并行（两个独立循环互不影响）
- **多支师同时溃退到同省**：允许（同省最多 5 师 C15 才生效），C13 不限
- **溃退后立即被再次攻击**：允许（无保护期）。下回合 recoveryTurnsLeft 倒数完后正常作战
- **战役经验上限**：100。胜了不再涨。败可降到 0 不为负
- **tacticalLevel buff 是否叠加军衔 buff**：C15 才有军衔，C13 仅战役 buff 生效（5/10/15/20%）
- **DistributeReinforcementToBrigades 余数**：整数除法多余的 1-2 点给第一个 brigade（与 C12 战损分摊一致）
- **国家 manpower / equipmentStockpile 池为 0**：补员仍跑、但 actual 为 0、师双条不涨。**写死**：不报错、不发事件
- **isCutoff 字段语义**：C13 保留默认 false。C14 由 SupplyResolver 写入。**当前 C13 任何 unit 都视为"有补给"参与补员**

## 完工后人类 Play 验证清单
1. 推 10 回合不操作 → 玩家国 manpower / equipmentStockpile 池子应自然增长（初始游戏全师满编无消耗）
2. 主动训练 1 师 → 立即扣 manpower 1200 + equipment 300（C11 数值）
3. 让玩家师攻一场 → 战损 → 下回合双条应自动恢复 50% 缺口
4. 战斗胜负后看玩家师详情应显 `战役: 1级 (10/100)` 或类似
5. 故意让师战到 manpower 10/100（85%+ 损失）→ tick 后自动撤退到邻接己方省 + 状态显"溃退修整中"
6. 溃退师下回合点己方邻省 → 允许移动；点敌方邻省 → 拒"溃退中无法进攻"
7. 溃退后 1 回合 recoveryTurnsLeft 归零 → 师恢复正常可指挥

# C9c — 多兵种联合战斗 + GameOver UI 反馈

## 背景
人类真玩 2 局反馈：**"战斗 1v1 排队枪毙 / 不能多路进攻 / 不能控制驻扎几支部队"**。

C3 写死"每省一战 + id 最小 [0] 主战 + 其他不参战 + 攻胜清场"——这是骨架决策。现在升级为"多对多团战"。

同时人类真玩出现 **GameOver 触发但 UI 无反馈**问题（玩家不知道何时输的），本单一并修。

## 范围

### 双修
| 修法 | 描述 |
|------|------|
| **多攻方加入同省战斗** | 移除 BattleResolver.InitiateAttack 的"目标省已有战斗"拒绝；允许多个 attacker 加入同一 ActiveBattle |
| **多守方联合防守** | ActiveBattle.defenderUnitId（单字符串） → defenderUnitIds（List）；TickBattles 战力 = 双方所有部队 sum |
| **战力公式** | `sum(unit.baseAttack × organization / max(1, maxOrganization))` 整数运算；伤害按各部队战力占比分摊 |
| **占领清场** | 攻胜后清场该省"非攻方阵营所有部队"（含未直接参战的 reserve）—— 与 C3 决策一致 |
| **GameOver UI 大字** | MainHudController 订阅 GameOverEvent → 状态栏大字红色显示"💀 失败 / 🎉 胜利"+ 推进按钮 disabled |

### 经批准的小重构（规则 9 例外，Claude 设计）
**ActiveBattle 数据结构升级**：
```csharp
// 原
public sealed class ActiveBattle {
    public string id;
    public string attackerUnitId;
    public string defenderUnitId;
    public string provinceId;
    public int turnsElapsed;
}

// 新
public sealed class ActiveBattle {
    public string id;                            // = provinceId（一省一战）
    public List<string> attackerUnitIds = new(); // 多攻方
    public List<string> defenderUnitIds = new(); // 多守方
    public string provinceId;
    public string attackerOwnerCountry;          // 攻方阵营（首位 attacker 的 owner）
    public int turnsElapsed;
}
```

理由：单字段 → 多字段是**纯加法重构**（保持现有读路径都能转换），符合规则 9"局部实现优化"边界。**id 改为 = provinceId**（一省最多一个 ActiveBattle）—— 这是关键简化，避免多 ActiveBattle 在同省冲突。

### 战斗规则（写死）
```
每 tick:
  attackPower = sum(unit.baseAttack × unit.organization × 100 / max(1, unit.maxOrganization)) for unit in attackerUnitIds
  defendPower = sum(unit.baseDefense × unit.organization × 100 / max(1, unit.maxOrganization)) for unit in defenderUnitIds × terrain倍率

  combatRatio = attackPower / max(1, defendPower)

  对每个攻方 unit: TakeDamage(orgDmg = 10/ratio, strDmg = 5/ratio) × random(±20%)
  对每个守方 unit: TakeDamage(orgDmg = 10*ratio, strDmg = 5*ratio) × random(±20%)

  清理 shattered (org ≤ 0)：通过 UnitDestroyer.Destroy 移除 + 从 attackerUnitIds/defenderUnitIds 移除

  收尾判断:
    attackerUnitIds 空 + defenderUnitIds 非空 → DefenderWin（守胜）
    attackerUnitIds 非空 + defenderUnitIds 空 → AttackerWin（攻胜）→ 清场该省所有非攻方部队 + 攻方首支进省 + controllerCountry 改
    双方都空 → Draw
    双方都有 → 继续下回合
```

**伤害"按各部队战力占比分摊"**：暂不做（C10+ 平均化即可），统一所有 attacker / defender 受相同 orgDmg/strDmg。简化但能跑通团战。

### InitiateAttack 改动
```
原 line 103-108: 检查目标省已有战斗 → 拒
新:
  if (省已有 ActiveBattle):
    若该 battle 的 attackerOwnerCountry == attacker.ownerCountry:
      → attackerUnitIds.Add(attacker.id)  // 加入己方队伍
      → return Accept
    else:
      → return Reject "敌方已对该省发起战斗"  // 不同阵营不能挤同一战
  else:
    创建新 ActiveBattle，attackerUnitIds=[attacker.id]，defenderUnitIds=该省所有非攻方部队 ids
    return Accept
```

## 配置（economy.json）
**零新增数值**——战斗系数沿用 BattleResolver 既有 float 常量（10/5/0.2 等）。

## 文件变更清单

### Domain
- `ActiveBattle.cs` — 字段改：attackerUnitId/defenderUnitId 单 → List；加 attackerOwnerCountry
- `WorldState.cs` — 无变（activeBattles 仍 List<ActiveBattle>）

### Simulation
- `BattleResolver.cs`：
  - `InitiateAttack` — 多攻方加入逻辑（见 §InitiateAttack 改动）
  - `TickBattles` — 改用 List 遍历双方部队、战力 sum、损伤循环
  - `ResolveBattle(UnitState, UnitState, ProvinceState)` — **保留**（向后兼容），但 TickBattles 不再调用；新增 `ResolveMultiBattle(List<UnitState> attackers, List<UnitState> defenders, ProvinceState province)` 返回 BattleResult
  - 既有 float 公式 `CalculateAttack / CalculateDefense / CalculateArmorModifier / GetTerrainDefenseMultiplier / ApplyRandom` 0 改动
- `AIResolver.cs` — `TryAttack` 已有"目标省已有战斗 → 跳过"逻辑可保留（AI 不主动加入团战，简化），无变

### Application
- `SaveModels.cs` — `ActiveBattleSaveData` 字段改 List<string>
- `SaveMapper.cs` — 双向 + HashWorld 扩 attackerUnitIds + defenderUnitIds（按 id 升序写）
- `ReadModelBuilder.cs` — `ActiveBattleView` 字段同步改 List；BuildActiveBattleView 拿 sum org 显示

### Contracts
- `ActiveBattleView.cs` — 字段改：`string attackerUnitId / defenderUnitId` → `List<string> attackerUnitIds / defenderUnitIds`；`attackerOrg / defenderOrg` 改成 sum
- `BattleInitiatedEvent.cs` / `BattleConcludedEvent.cs` — 加 `List<string> attackerUnitIds / defenderUnitIds`（保留旧字段兼容）

### Presentation
- `MainHudController.cs`:
  - 订阅 `GameOverEvent` — 状态栏挂 USS class `.status-game-over` + 文字"💀 失败！首都已失守" 或 "🎉 胜利！占领所有敌方首都"
  - `Advance` 按钮 disabled when GameOver
  - 详情栏 hasActiveBattle 时显示双方部队数：`战斗中: 攻 N 支 vs 守 M 支 - X 回合`
- `MainHud.uss` — `.status-game-over { color: rgba(255, 80, 80, 1); font-size: 22px; -unity-font-style: bold; }`

### Tests
- `BattleResolverC9cTests.cs` 新建：
  - `InitiateAttack_TargetEmptyProvinceNoExistingBattle_OccupiesInstant`
  - `InitiateAttack_TargetHasDefenders_CreatesBattleWithAllDefenders`
  - `InitiateAttack_SameProvinceSameAttackerSide_AddsToExistingAttackerList`
  - `InitiateAttack_SameProvinceDifferentAttackerSide_Rejects`
  - `TickBattles_MultiVsMulti_SumPowerCalculated`
  - `TickBattles_AllAttackersShattered_DefenderWins`
  - `TickBattles_AllDefendersShattered_AttackerWins_OccupiesProvince`
  - `TickBattles_BothSidesEmpty_Draw`
  - `TickBattles_AttackerWin_ClearsAllNonAttackerUnitsInProvince`
- `SaveLoadEquivalenceTests.cs` 追加：
  - `SaveLoad_MultiUnitBattle_Preserved`
- `MainHudControllerTests.cs` 追加：
  - `GameOverEvent_DisplaysVictoryText`
  - `GameOverEvent_DisplaysDefeatText`
  - `GameOverEvent_DisablesAdvanceButton`
- `ConfigValidationTests.cs` — 无追加

## 不做（C10+）
- 伤害按各部队战力占比分摊（C10 精细化）
- 多兵种地形组合修正
- 攻方部队从多省同步进攻（玩家可手动一支一支加入，无需"批量进攻"UI）
- 战斗中部队撤退
- AI 主动加入团战（C10+ AI 战术升级）
- 围攻：玩家围困不直接战斗
- BattleResolver float→int 重构（技术债保留）

## DoD Check List
- [ ] ActiveBattle 数据结构改 List 字段、attackerOwnerCountry 字段加入
- [ ] InitiateAttack 多攻方加入逻辑、同省同阵营 Add、同省异阵营 Reject
- [ ] TickBattles 改 ResolveMultiBattle、sum 战力公式、双方损伤循环
- [ ] 既有 ResolveBattle / CalculateAttack / CalculateDefense / ApplyRandom / GetTerrainDefenseMultiplier 0 改动
- [ ] SaveModels / SaveMapper / HashWorld 扩 List 字段
- [ ] ReadModelBuilder / ActiveBattleView 字段改 List
- [ ] MainHudController 订阅 GameOverEvent 大字反馈 + Advance disabled
- [ ] 测试覆盖：9 个 BattleResolverC9c + 1 SaveLoad + 3 MainHud
- [ ] artifacts/c9c-editmode.xml + c9c-playmode.xml 归档
- [ ] Play 截图 3 张：
  - `c9c-multi-attacker-joined.png`（玩家两支部队都在攻同一省、详情栏显示"攻 2 支 vs 守 1 支"）
  - `c9c-multi-defender-battle.png`（同省多守方一起被攻、tick 后双方都损伤）
  - `c9c-gameover-defeat-bigtext.png`（GameOver 触发瞬间大红字"💀 失败"）
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 0 failed
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] 不动 EconomyResolver / PoliticsResolver / WarTollResolver / PeaceResolver / AiPeaceOfferResolver / OccupationResolver / VictoryConditionResolver / AIResolver / MovementResolver / ConstructionResolver / UnitProductionResolver / AiRedeploymentResolver / WarRegistry 既有公式
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 严禁
- 改 BattleResolver float 战斗公式（技术债保留 C10+ 收）
- 加 attacker 主动撤退按钮（C10+）
- 加 AI 主动加入团战（C10+ AI 战术）
- 改 ActiveBattle.id 之外的关联键设计（一省一战，不允许同省并存多 ActiveBattle）
- 跳过指派测试

## 歧义处理
- **TickBattles 遍历内 attacker/defender 列表更新时机**：先全部计算战力（拷贝快照）→ 再逐个 TakeDamage → 最后统一移除 shattered。**写死**：避免遍历中改 list 报错
- **AI vs AI 同省**：如 A 攻 C 的省（创建 battle）+ B 也攻 C 的省 → B 看到 attackerOwnerCountry==A 不是 B → Reject "敌方已发起战斗"。**写死**：AI 之间不能挤同一战，C10+ 再考虑三方混战
- **多攻方时哪个 attacker 进省占领**：attackerWin 时取 attackerUnitIds 第 1 个 = 按 add 顺序首位（保持入队顺序，不强制排序——玩家直觉"第一个发起的部队代表"）
- **MainHudController 大字位置**：覆盖 status-label，不新建 UI 节点
- **GameOver 触发后旧的 status-label 文字（被拒文案）会被大字覆盖**：是设计意图——GameOver 后只显示终局状态
- **多攻方战斗的 BattleInitiatedEvent 触发时机**：仅在创建新 ActiveBattle 时发布一次。后续 Add attacker 不再发 BattleInitiatedEvent（避免事件刷屏）
- **若 ActiveBattle.id == provinceId 与既有 attackerUnitId_vs_defenderUnitId 命名冲突**：本单**改 id 规则**，存档兼容性靠 schemaVersion 检测（C2 技术债 C-2 范围，本单不实现迁移）

## 完工后人类 Play 验证清单
1. 训 2 支步兵在赤原 → 一支攻 wind_plain → 详情栏出 "战斗中: 攻 1 支 vs 守 1 支"
2. 第二支步兵也攻 wind_plain → "战斗中: 攻 2 支 vs 守 1 支"（多攻方加入生效）
3. 推回合 → 战斗 tick → 双方损伤
4. 攻方胜 → 进省占领 + 状态栏"占领 wind_plain"
5. 失守自己首都 → 状态栏大红字"💀 失败！" + 推进按钮变灰（GameOver UI 反馈生效）
6. 占领所有敌方首都 → 大绿字"🎉 胜利！"

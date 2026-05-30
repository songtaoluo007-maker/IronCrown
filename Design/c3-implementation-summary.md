# C3 战斗与占领系统 — 实现摘要

## 分支：feature/c3-battle-occupation
## 基线：main (C2b merged)
## 提交数：4 commits
## 文件变更：30 files changed, +1198 / -72

---

## 新增文件（10 个源码 + 7 个 meta）

| 文件 | 层 | 用途 |
|---|---|---|
| `Domain/State/ActiveBattle.cs` | Domain | 战斗数据结构（id/attacker/defender/province/turnsElapsed） |
| `Contracts/ReadModels/ActiveBattleView.cs` | Contracts | 战斗只读DTO（含双方 org 值） |
| `Contracts/Events/BattleInitiatedEvent.cs` | Contracts | 战斗发起事件 |
| `Contracts/Events/BattleConcludedEvent.cs` | Contracts | 战斗结束事件（Attacker/Defender/Draw） |
| `Contracts/Events/ProvinceOccupiedEvent.cs` | Contracts | 空城占领事件 |
| `Contracts/Events/UnitDestroyedEvent.cs` | Contracts | 部队消灭事件（battle/occupation） |
| `Tests/.../BattleResolverC3Tests.cs` | Tests | 12 个战斗单元测试 |

## 修改文件（关键变更）

| 文件 | 变更 |
|---|---|
| `BattleResolver.cs` | +202行：InitiateAttack（7步验证+空城即占）、TickBattles（1v1 tick+占领+清场）、DestroyUnit |
| `TurnResolver.cs` | Settlement 尾段调用 TickBattles |
| `GameSessionService.cs` | 注入 BattleResolver；MoveUnit 按 controllerCountry 分流（己方→MovementResolver，敌方→BattleResolver）；战斗锁定检查 |
| `ReadModelBuilder.cs` | 按 controllerCountry 取色；新增 controllerCountry/isOccupied/hasActiveBattle/isInBattle；activeBattles 列表 |
| `SaveModels.cs` | ActiveBattleSaveData + GameState.activeBattles |
| `SaveMapper.cs` | activeBattles 双向持久化 |
| `MainHudController.cs` | 攻击目标高亮（红）、战斗标记、BattleInitiated/Concluded/ProvinceOccupied 事件订阅、garrison cycling 修复 |
| `MainHud.uss` | province-tile-attack-target / province-tile-in-battle / province-battle-badge |
| `ProvinceView.cs` | controllerCountry / isOccupied / hasActiveBattle |
| `UnitView.cs` | isInBattle |
| `WorldView.cs` | activeBattles |
| `WorldState.cs` | List<ActiveBattle> activeBattles |

## 测试覆盖

| 测试类 | 数量 | 覆盖 |
|---|---|---|
| BattleResolverC3Tests | 12 | InitiateAttack(6) + TickBattles(5) + 排序(1) |
| GameSessionServiceTests (+C3) | 3 | 攻击创建战斗 / 战斗锁定 / 占领信息 |
| ReadModelBuilderTests (+C3) | 4 | controllerColor / hasActiveBattle / isInBattle / activeBattles |
| SaveLoadEquivalenceTests (+C3) | 1 | ActiveBattle 存档往返 |
| **合计新增** | **20** | |

## 规则守卫

- 规则 3（确定性排序）：activeBattles / battleUnitIds / battleProvinceIds 全部 id 升序
- 规则 4（UI 不引用 Domain/Sim）：Presentation 只通过 GameSessionService + WorldView 交互
- 规则 5（DI 注入）：BattleResolver 已在 GameLifetimeScope 注册，TurnResolver/GameSessionService 通过构造函数注入
- 规则 8（小步提交）：4 commits，每个独立可编译
- 规则 9（BattleResolver 既有公式分文未动）：ResolveBattle 签名和 float 公式完全保留

## P2 修复

- P2-4：garrison cycling 改为 controllerCountry == playerCountryId + ownerCountry 过滤
- P2-5：InitiateAttack 拒绝目标省已有 ActiveBattle（防止静默消失）
- P2-6：TickBattles_MultiTick_EventuallyShatters — 真多 tick 累积伤害测试

## 已知技术债（P3）

- BattleResolver float 公式 → int 重构（deferred）
- world.worldTension ToRuntime 设 0（T4 遗留）
- ReadModelBuilder.BuildProvinceView O(P×U) 多次遍历 → 优化

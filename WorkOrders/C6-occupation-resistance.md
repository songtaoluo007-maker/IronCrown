# C6 — 占领抵抗 (Occupation Resistance)

## 背景
ProvinceState 从 C1 起就有 `resistance` 和 `compliance` 字段，但从未被任何 Resolver 使用。C6 激活它们。

## 范围

### 核心机制
| 机制 | 描述 | 数值 |
|------|------|------|
| 占领瞬间 | resistance 设为 | 50 |
| 有驻军衰减 | 占领方在该省有 units → resistance 每回合 | -2 |
| 无驻军增长 | 占领方在该省无 units → resistance 每回合 | +1 |
| 反抗事件触发 | resistance ≥ 30 时，每回合 | 10% 概率 |
| 有驻军反抗 | 扣驻军 manpower/equipment | 各 5 |
| 无驻军起义 | controllerCountry 回归 ownerCountry | 省份解放 |

### 配置项 (economy.json)
```json
{
  "resistanceOnCapture": 50,
  "resistanceDecayWithGarrison": -2,
  "resistanceGrowWithoutGarrison": 1,
  "resistanceUprisingThreshold": 30,
  "resistanceUprisingChancePct": 10,
  "resistanceGarrisonDamageManpower": 5,
  "resistanceGarrisonDamageEquipment": 5
}
```

## 文件变更清单

### Domain (零新概念)
- `EconomyConfig.cs` — 加 7 个 C6 字段
- `Province.cs` — 零改动（resistance/compliance 已存在）

### Contracts
- 新建 `ResistanceUprisingEvent.cs` — 起义事件

### Simulation
- `BattleResolver.cs` — 占领时 `resistance = 50`（2 处：InitiateAttack + TickBattles）
- 新建 `OccupationResolver.cs` — 核心：驻军判定 + resistance 增减 + 起义事件
- `TurnResolver.cs` — 注入 OccupationResolver，Settlement 阶段调用

### Bootstrap
- `GameLifetimeScope.cs` — 注册 OccupationResolver

### Data
- `economy.json` — 加 7 个 C6 配置值

### Tests
- `OccupationResolverTests.cs` — 新建
- `BattleResolverC6Tests.cs` — resistance=50 验证

### Presentation (可选)
- `MainHudController.cs` — 省份详情显示 resistance/compliance

## DoD Check List
- [ ] AGENTS.md / README.md / ARCHITECTURE.md / CHANGELOG.md / .gitignore 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] 不动 PoliticsResolver / EconomyResolver / AIResolver / MovementResolver / ConstructionResolver / VictoryConditionResolver / WarTollResolver / PeaceResolver 既有公式
- [ ] `GameSessionService` 构造签名 grep 全工程调用全部匹配
- [ ] `BattleResolver` 构造签名可选参数向后兼容
- [ ] `TurnResolver` 构造签名可选参数向后兼容
- [ ] resistance/compliance 字段零新增（C1 已存在）
- [ ] 测试覆盖：占领 resistance=50 + 有驻军衰减 + 无驻军增长 + 起义触发 + 有驻军反抗 + 无驻军起义
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

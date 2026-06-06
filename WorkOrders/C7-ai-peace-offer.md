# C7 — AI 主动求和 (AI Peace Offer)

## 背景
C5 实现了 PeaceResolver，但只有玩家能主动提议停战。AI 永远被动接受/拒绝，导致：
- AI vs AI 战争打到一方崩溃
- 玩家必须追到敌国亡国才能终战

C7 让 AI 在疲惫时主动向玩家求和。AI vs AI 谈和留 C8+。

## 范围

### 核心机制
| 机制 | 描述 | 数值 |
|------|------|------|
| 触发条件 | AI 与玩家交战，warExhaustion ≥ | 40 |
| 弱势条件 | AI 国力 ≤ 玩家的 | 80% |
| 提议频率 | 被拒后冷却 | 5 回合 |
| 提议过期 | 玩家未响应自动过期 | 10 国家回合 |
| 接受处理 | 复用 C5 PeaceResolver 流程 | 双方 warExhaustion 减半 |
| 拒绝处理 | 记录冷却，战争继续 | — |

### 配置项 (economy.json)
```json
{
  "aiPeaceOfferExhaustionThreshold": 40,
  "aiPeaceOfferPowerRatioPct": 80,
  "aiPeaceOfferCooldownTurns": 5,
  "aiPeaceOfferExpiryTurns": 10
}
```

### 国力公式（复用 C5）
`factories × 10 + units × 20 + capitalVP / 10`

## 文件变更清单

### Domain
- `EconomyConfig.cs` — 加 4 个 C7 字段
- `CountryState.cs` — 加 `peaceOfferCooldown`（int, 剩余冷却回合）+ `pendingPeaceOfferFrom`（string, null = 无待处理提议）

### Contracts
- 新建 `AiPeaceOfferedEvent.cs` — AI 求和事件（发起方、目标、过期回合）
- `CommandType.cs` — 加 `AcceptPeace` / `RejectPeace`

### Simulation
- 新建 `AiPeaceOfferResolver.cs` — 核心：每回合 Settlement 检查所有 AI 国家，满足条件则向玩家发提议
- `PeaceResolver.cs` — 扩展：处理 AcceptPeace（复用 OfferPeace 逻辑）+ RejectPeace（设冷却）
- `TurnResolver.cs` — 注入 AiPeaceOfferResolver，Settlement 阶段在 WarToll 之后调用

### Application
- `GameSessionService.cs` — 加 AcceptPeace / RejectPeace 命令分发
- `SaveMapper.cs` — 双向映射 peaceOfferCooldown + pendingPeaceOfferFrom
- `SaveModels.cs` — CountrySaveData 加对应字段
- `ReadModelBuilder.cs` — CountryView 加 peaceOfferCooldown + pendingPeaceOfferFrom

### Bootstrap
- `GameLifetimeScope.cs` — 注册 AiPeaceOfferResolver

### Data
- `economy.json` — 加 4 个 C7 配置值

### Presentation
- `MainHudController.cs` — 收到 AI 求和弹窗（接受/拒绝按钮）+ 冷却显示
- `MainHud.uxml` / `MainHud.uss` — 求和弹窗 UI

### Tests
- `AiPeaceOfferResolverTests.cs` — 新建（触发/弱势/冷却/过期/AI vs AI 跳过）
- `PeaceResolverC7Tests.cs` — AcceptPeace + RejectPeace + 冷却生效

## 不做（C8+）
- AI vs AI 谈和（AI vs AI 打到一方崩是正常战略游戏行为）
- 领土割让/赔款谈判
- 玩家主动向 AI 求和的 UI（已有 C5 OfferPeace）
- 多轮谈判/反提议

## DoD Check List
- [ ] AGENTS.md / README.md / ARCHITECTURE.md / CHANGELOG.md / .gitignore 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] 不动 PoliticsResolver / EconomyResolver / AIResolver / MovementResolver / ConstructionResolver / VictoryConditionResolver / WarTollResolver / OccupationResolver 既有公式
- [ ] PeaceResolver 既有 OfferPeace 逻辑不受影响
- [ ] `GameSessionService` 构造签名 grep 全工程调用全部匹配
- [ ] `TurnResolver` 构造签名可选参数向后兼容
- [ ] 测试覆盖：AI 触发求和 + 弱势条件 + 冷却机制 + 过期机制 + AcceptPeace + RejectPeace + AI vs AI 跳过
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

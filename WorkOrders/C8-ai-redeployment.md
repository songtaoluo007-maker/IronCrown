# C8 — AI 调防 (AI Redeployment)

## 背景
C4 让 AI 会进攻，但 AI 不会调防——内陆有富裕部队、前线弱守时无动于衷。C8 让 AI 自动从内陆调一支驻军到前线弱守省份。

## 范围

### 核心机制
| 机制 | 描述 | 数值 |
|------|------|------|
| 威胁判定 | 目标省：邻接敌控省含敌方部队 AND 自己守军战力 ≤ 邻敌 × | 80% |
| 源省条件 | 所有邻省己方控制 + 驻军 ≥ | 2 支（保留 1 自卫） |
| 调防部队 | 源省按 unit.id 升序 [0]，movesLeft ≥ 1，不在战斗中 | — |
| 路径 | 源省必须直接邻接目标（一次一步，C9+ 才做多步） | — |
| 每国上限 | 每回合最多调防 | 1 次 |
| 移动复用 | 必须调用 MovementResolver.TryMove | 不允许 AI 自写管线 |

### 配置项 (economy.json)
```json
{
  "aiRedeployVulnerableRatioPct": 80,
  "aiMaxRedeploysPerTurn": 1
}
```

### 战力计算
`unit.organization + unit.morale + unit.experience * 10`

## 文件变更清单

### Domain
- `EconomyConfig.cs` — 加 2 个 C8 字段

### Simulation
- 新建 `AiRedeploymentResolver.cs` — 核心：每回合 Military 阶段检查 AI 国家
- `AIResolver.cs` — 注入 AiRedeploymentResolver，在 AI 逻辑后调用
- `TurnResolver.cs` — 注入 AiRedeploymentResolver（备用）

### Application
- `GameSessionService.cs` — 无新命令（AI 自动行为）

### Bootstrap
- `GameLifetimeScope.cs` — 注册 AiRedeploymentResolver

### Data
- `economy.json` — 加 2 个 C8 配置值

### Tests
- `AiRedeploymentResolverTests.cs` — 新建

## 不做（C9+）
- 多步路径调防
- AI vs AI 调防（仅 AI 对玩家防线调防）
- AI 撤退/回撤

## DoD Check List
- [ ] AGENTS.md / README.md / ARCHITECTURE.md / CHANGELOG.md / .gitignore 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动（git checkout main -- 还原）
- [ ] 不动 PoliticsResolver / EconomyResolver / ConstructionResolver / UnitProductionResolver / VictoryConditionResolver / BattleResolver / WarTollResolver / OccupationResolver / PeaceResolver / AiPeaceOfferResolver 既有公式
- [ ] MovementResolver.TryMove 是唯一移动管线入口（规则 8）
- [ ] `AIResolver` 构造签名 grep 全工程调用全部匹配
- [ ] `TurnResolver` 构造签名可选参数向后兼容
- [ ] 测试 0 failed
- [ ] artifacts: c8-editmode.xml + c8-playmode.xml 都要导出
- [ ] 截图: 至少 2 张 png（c8-redeploy-before / c8-redeploy-after）
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

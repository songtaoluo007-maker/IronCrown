# C13-fix — 师 organization/morale 初始化遗漏

## 背景
人类玩 C13 终态发现：新训师/初始师显示 `组织: 0/50 (0%)` + `士气: 0/100` —— 应该是满编 organization = maxOrganization = 50（步兵师 9 步兵×60 + 3 炮兵×40 平均）+ morale = 50（infantry 模板默认）。

**根因**：C11 引入 `RecalculateFromBrigades` 时，**只算了 max 字段，没初始化 current**。`UnitFactory.CreateFromDivisionTemplate` 也漏了 current organization/morale 初始化。

C13 269/269 + 7/7 测试全绿没捕获——测试用例没覆盖"新师默认 organization/morale = max"断言。

副作用：organization=0 = HoI 标准溃散态、不应能行动，但 `MovementResolver.TryMove` 没校验 organization > 0 → 玩家居然能移动这种师。

## 范围

### 双修
| 修法 | 改动 |
|------|------|
| **UnitFactory.CreateFromDivisionTemplate** | 创建师时设 `unit.organization = unit.maxOrganization` + `unit.morale = 50`（既有 infantry 模板默认 morale = 50） |
| **RecalculateFromBrigades 语义修正** | **只重算 max 字段**（maxManpower / maxEquipment / maxOrganization / baseAttack 等），**不动 current**（manpower / equipment / organization / morale）。运行时 current 字段是战损/补员状态，重算时保留 |
| **可选 MovementResolver 加 organization 校验** | `if (unit.organization <= 0) reject "部队溃散无法行动"` —— **本 fix 不做**，留 C14 的 disorganized 校验统一处理 |

### 数据 / 测试
- `economy.json` 无变
- 加回归测试 `UnitFactoryTests`：
  - `CreateFromDivisionTemplate_InitialOrganizationEqualsMax`
  - `CreateFromDivisionTemplate_InitialMoraleIs50`
- 加回归测试 `UnitStateTests`：
  - `RecalculateFromBrigades_DoesNotResetCurrentOrganization`（先设 unit.organization=30、调 Recalculate、断言仍 30）
  - `RecalculateFromBrigades_DoesNotResetCurrentMorale`
  - `RecalculateFromBrigades_UpdatesMaxOrganizationOnly`
- 加回归测试 `WorldInitializer`：
  - `CreateNewGame_InitialDivisions_HaveOrganizationAtMax`（初始 6 国 6 师全部 organization=maxOrganization）

## 文件变更清单
- `Domain/UnitFactory.cs` — 创建时初始化 current organization/morale
- `Domain/Unit.cs` (`UnitState.RecalculateFromBrigades`) — 仅重算 max 字段，不动 current
- `Assets/Tests/EditMode/...UnitFactoryTests.cs` — 加 2 测试
- `Assets/Tests/EditMode/...UnitStateTests.cs`（新建或追加）— 加 3 测试
- `Assets/Tests/EditMode/...WorldInitializerTests.cs`（如有）— 加 1 测试

## DoD Check List
- [ ] UnitFactory.CreateFromDivisionTemplate 设 current organization = max
- [ ] RecalculateFromBrigades 不再清零 current 字段（含 manpower / equipment / organization / morale）
- [ ] 既有 269 EditMode + 7 PlayMode 测试全绿（无回归）
- [ ] 新增 6 测试全绿
- [ ] **★ commit 完成后立即 `git push origin feature/c5-diplomacy-peace`**（PR #1 自动追加，C12/C13 已连破 2 次 push 规则，本 fix 零容忍）
- [ ] artifacts/c13-fix-editmode.xml 归档（**只一对**、不接受 5+ 中间 log）
- [ ] **Play 截图 1 张** `c13-fix-org-init.png`：新训师组织 organization = 50/50（与之前 0/50 对比）
- [ ] Unity Console 0 error
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md / ProjectSettings / Packages 0 改动
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 严禁
- 改 BattleResolver / SupplyResolver / EconomyResolver / 任何 Resolver 既有公式
- 加 MovementResolver organization 校验（留 C14 disorganized 统一）
- 顺手做 C14 任何内容
- 不 push

## 完工后人类 Play 验证
1. 新游戏开始 → 玩家初始师详情 → `组织: 50/50` + `士气: 50/100`（不再 0/50 + 0/100）
2. 训练 1 师 → 完工后该师 `组织: 50/50` + `士气: 50/100`
3. 战斗后该师 organization 下降到 30 → 推 Settlement → Settlement 后 organization 应仍是 30（不被 RecalculateFromBrigades 清零）

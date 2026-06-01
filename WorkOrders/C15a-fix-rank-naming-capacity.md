# C15a-fix — 军衔命名 + maxDivisions 公式回归原设计

## 背景
人类玩 C15a 终态发现两个偏离原设计：
1. **军衔命名违设计**：OpenClaw 自作主张改 `["尉官","校官","将官","元帅","大元帅"]` → 应该是 **`["少将","中将","上将","大将","元帅"]`**（人类明确拍板）
2. **maxDivisions 公式错位**：OpenClaw 写 `maxDivisions = 5 + rank`（最低 5 师、最高 9 师），违反"5 师 = 集团军 = 元帅级"天花板设计 + 与同省 5 师容量冲突

人类决策（A 方案）：**严格回归 Claude 原设计**——少→中→上→大→帅，maxDivisions = rank + 1（1/2/3/4/5）。

## 范围

### 双修
| 修法 | 改动 |
|------|------|
| **军衔命名回归** | `CommanderState.RankNames = { "少将", "中将", "上将", "大将", "元帅" }` |
| **maxDivisions 公式回归** | `maxDivisions = rank + 1`（少将=1 / 中将=2 / 上将=3 / 大将=4 / 元帅=5） |
| **招募初始化** | 新将军 rank=0 时 maxDivisions=1（少将 1 师上限） |

### 配置（economy.json）
无变 —— maxDivisionsPerProvince=5 保持（与元帅满 5 师 = 集团军天花板对齐）。

### 公式
```csharp
// CommanderState.cs
public static readonly string[] RankNames = { "少将", "中将", "上将", "大将", "元帅" };

public bool TryPromote() {
    if (!CanPromote) return false;
    rank++;
    maxDivisions = rank + 1;  // 从 5+rank 改为 rank+1
    return true;
}

// 招募逻辑（CommanderResolver 或 GameSessionService）创建新 CommanderState 时
new CommanderState {
    rank = 0,
    maxDivisions = 1,  // 少将 = 1 师上限（不是 5）
    victories = 0,
    encirclements = 0,
    ...
};
```

## 文件变更清单

### Domain
- `Domain/State/CommanderState.cs`:
  - line 35 `RankNames` 改 `{ "少将", "中将", "上将", "大将", "元帅" }`
  - line 54 `maxDivisions = 5 + rank` → `maxDivisions = rank + 1`

### Simulation / Application
- 招募新将军处（找 `new CommanderState`）改初始 `maxDivisions = 1`（应在 CommanderResolver 或 GameSessionService.IssueCommand RecruitGeneral 分支）

### Tests
- 既有 CommanderState / CommanderResolver / 任命 / 晋升 测试更新断言：
  - `RankName` 期望从"尉官"改"少将"等
  - `TryPromote_FromMajor_GetsLtGenRankAndMaxDivisions2`：rank 1 → maxDivisions=2
  - `TryPromote_FromGeneralToMarshal_MaxDivisions5`：rank 4 → maxDivisions=5
  - `AssignDivision_MajorRank_OnlyOneAllowed`：少将麾下 1 师后第 2 个 reject "麾下已满"
  - `RecruitGeneral_NewCommander_MaxDivisionsIsOne`：初始 maxDivisions=1（不是 5）
- 现有 5 师麾下相关测试调整（让上限测试在元帅级触发，不是尉官）

## DoD Check List
- [ ] CommanderState.RankNames 改 ["少将", "中将", "上将", "大将", "元帅"]
- [ ] CommanderState.TryPromote 内 maxDivisions = rank + 1
- [ ] 招募逻辑新将军初始 maxDivisions = 1
- [ ] 既有测试断言更新（RankName + maxDivisions 数值）
- [ ] 全工程 grep "尉官\|校官\|将官\|大元帅" 应只剩**注释中说明历史变更**或彻底无（首选无）
- [ ] 既有 269+ EditMode + 7 PlayMode 全绿
- [ ] **★ commit 完成后立即 `git push origin feature/c5-diplomacy-peace`**（PR #1 自动追加。C15a 5 commit 修构造签名已经够多，C15a-fix 必须一次过）
- [ ] artifacts/c15a-fix-editmode.xml 归档（只一对）
- [ ] **Play 截图 1 张** `c15a-fix-rank-names.png`：招募新将军后显示"[少将] 攻+0% 防+0% 指挥1师"（不是"[尉官] 指挥5师"）
- [ ] Unity Console 0 error
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md / ProjectSettings / Packages 0 改动
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 严禁
- 改 BattleResolver / SupplyResolver / WarTollResolver / PeaceResolver 等任何 Resolver 公式
- 改 maxDivisionsPerProvince=5（同省容量保持）
- 改任何战斗 buff 数值（RankAttackBonusPct = rank * 5 保持）
- 顺手做 C15b 任何内容（C15b 是 12 张原创卡 + 历史差异化技能、本 fix 不碰）
- 不 push

## 完工后人类 Play 验证
1. 招募新将军 → 显示"测试将军 [**少将**] 攻+0% 防+0% 指挥**1**师"
2. 任命 1 师到该将军 → 详情栏"统帅: 测试将军（少将）"
3. 任命第 2 师到该少将 → 拒"麾下已满"
4. 推回合积累 5 战役胜 → 晋升中将 → 显示"[中将] 攻+5% 防+5% 指挥**2**师"
5. 累积到 75 胜 → 元帅 → 显示"[**元帅**] 攻+20% 防+20% 指挥**5**师"
6. 元帅麾下 5 师集结到同省 → OK（同省 5 师容量 = 元帅天花板对齐）

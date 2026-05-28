# 工作单 T4 — 确定性与存读档闭环（Determinism & Save/Load）

| 项 | 值 |
|---|---|
| 工作单号 | T4（路线图：确定性 PRNG + 存读档闭环；落地已锁定的"整数优先 + 自定义种子 PRNG"决策） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查 | Claude（规则 13） |
| 分支 | **`feature/t4-determinism-saveload`（务必新建独立分支，勿再堆在 t2 分支上）** |
| 前置 | T3 已完成 + 本单 Phase 0 收尾通过 |
| 角色边界 | 规则 12：只实现、不做架构/数值/玩法决策。本单已写死全部算法与字段。遇未覆盖点停下标 `[需 Claude 决策]` |

## 0. 目标

让"相同种子 + 相同操作序列 ⇒ 相同结果"可验证（落地确定性决策），并使**存档可确定性续跑**（保存 RNG 状态 + 回合 + 阶段，读档后与不中断运行结果一致）。

**本单只动确定性/存档机制，严禁改任何游戏数值/公式（规则 9/14）。** 战斗 float 公式本单**不**整数化（后续独立任务）。

## Phase 0 — 前置必办（T3 收尾，未过不得开 T4）

1. **新建分支** `feature/t4-determinism-saveload`（从已含 T3 的提交切出）。今后**一单一分支一 PR**。
2. **确认 C1 已回退**：`EconomyResolver.cs` 税收行现为 `(int)(country.taxIncome * stabilityMod)`（截断，Claude 已手修，**勿再改回 Math.Round**）。补一个**非整除输入**的回归用例（如 `stability=70` → `stabilityMod=0.85`、`taxIncome=95` → 期望 `(int)(95*0.85)=80`），锁死截断行为。
3. **删墓碑**：删除 `Assets/Scripts/Domain/Abstractions/IEventPublisher.cs`（已是空注释文件，真接口在 Contracts）及其 `.meta`。
4. **仓库卫生**：删除根目录散落的 `*.log`（`final*.log`/`t3-p*.log`/`compiletest.log`/`notest.log` 等）与 `artifacts/*.log`；把 `*.log`、`artifacts/`、`Temp/`、`UserSettings/` 等补进 `.gitignore`。
5. **查不明目录**：确认 `Assets/MobileDependencyResolver/`、`Assets/Resources/` 来源（疑似 `com.unity.ads`/`com.unity.purchasing` 拉入的 Play 解析器）。若非本项目所需，标 `[需 Claude 决策]` 不要擅自删包；若是生成物则 gitignore。
6. **跑全套测试**确认仍 44+ 全绿（C1 回退 + 新增用例后），编译 0 error。

## 1. 架构决策（已写死）

1. **PRNG = SplitMix64**（确定性、跨平台、纯整数运算）。`RandomService` 内部由 `System.Random` 换成下方算法；**保留构造签名 `RandomService(int seed)` 与现有 `IRandom` 方法**。
2. **可序列化 RNG 状态**：`IRandom` 增加 `ulong State { get; }` 与 `void RestoreState(ulong state)`，用于存档精确续跑（保存的是**当前内部状态**，不是原始 seed）。
3. **存档新增字段**：`GameState` 增加 `int seed`、`ulong rngState`、`string phase`（`GamePhase` 名称）。
4. **时钟可恢复**：`ITurnClock`/`GameClock` 增加 `void Restore(int turn, GamePhase phase, int maxTurns = 60)`。
5. **战斗 float 不动**：`BattleResolver`、`EconomyResolver` 等现有 float 公式保持原样。`IRandom.NextDouble/RangeDouble` 保留（战斗仍在用）。

## 2. SplitMix64 实现规格（照此实现，勿改常量）

```
// 内部状态：ulong _state；原始种子：int _seed
// Reset(seed): _seed = seed; _state = unchecked((ulong)seed);
// Reset():     _state = unchecked((ulong)_seed);
// State => _state;  RestoreState(s) => _state = s;

private ulong NextRaw()           // SplitMix64
{
    unchecked
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
```
派生方法（确定、可复现）：
- `int Next(int maxExclusive)`：`maxExclusive<=0` 抛 `ArgumentOutOfRangeException`；否则 `return (int)(NextRaw() % (ulong)maxExclusive);`（取模偏差对本游戏可接受，记录于注释）。
- `int Range(int minInclusive, int maxExclusive)`：`return minInclusive + Next(maxExclusive - minInclusive);`
- `bool Roll(int percentChance)`：`return Next(100) < percentChance;`（`Roll(0)` 恒 false、`Roll(100)` 恒 true）。
- `double NextDouble()`：`return (NextRaw() >> 11) * (1.0 / (1UL << 53));`（53 位，[0,1)）。
- `double RangeDouble(double min,double max)`：`return min + NextDouble() * (max - min);`
- `int Seed => _seed;`

> 现有 `RandomServiceTests` 是性质测试（同种子同序列、范围、Roll 边界），换算法后仍应全绿；若某条因断定具体数值而失败，停下标 `[需 Claude 决策]`（不要为迁就测试改算法常量）。

## 3. Phases

### P1 — PRNG 替换 + 状态接口
- `Domain/Abstractions/IRandom.cs`：加 `ulong State { get; }`、`void RestoreState(ulong state)`。
- `Domain/Random/RandomService.cs`：按 §2 用 SplitMix64 重写内部；实现 `State`/`RestoreState`；保留全部现有方法与构造签名。
- `Domain.Tests`：新增 `State`/`RestoreState` 往返用例（取数若干次 → 记 `State` → 再取若干 → `RestoreState` 回放 → 序列一致）。

### P2 — 存档字段 + 映射
- `Application/Persistence/SaveModels.cs`：`GameState` 加 `public int seed; public ulong rngState; public string phase;`。
- `Application/Mapping/SaveMapper.cs`：`ToSave(WorldState world, int seed, ulong rngState, GamePhase phase)`（**改签名**，把三者写入 `GameState`；现 `ToSave` 丢弃形参的问题一并修掉）。`ToRuntime` 仍只重建 `WorldState`（seed/rngState/phase 由会话层在 Load 时取用）。

### P3 — 时钟恢复 + 会话接线
- `Domain/Time/GameClock.cs`（+`ITurnClock`）：加 `Restore(int turn, GamePhase phase, int maxTurns = 60)`（设置 `CurrentTurn`/`CurrentPhase`/`MaxTurns`，`IsPaused=false`）。
- `Application/Session/GameSessionService.cs`：
  - `NewGame(int? seed)`：`_initialSeed = seed ?? _initialSeed;`（给个默认常量如 `12345`）→ `_rng.Reset(_initialSeed)` → 其余不变。
  - `Save(slot)`：`SaveMapper.ToSave(_world, _rng.Seed, _rng.State, _clock.CurrentPhase)`。
  - `Load(slot)`：`_world = ToRuntime(save)` → `_rng.Reset(save.seed); _rng.RestoreState(save.rngState);` → `_clock.Restore(save.turnNumber, System.Enum.Parse<GamePhase>(save.phase));`

### P4 — 确定性测试（核心交付）
新增 `Application.Tests`（或新建 `IronCrown.Determinism.Tests`，按 T3 后的测试 asmdef 规范：引用 Domain/Simulation/Application + TestRunner、nunit、`UNITY_INCLUDE_TESTS`）：
- **状态哈希工具**（测试内即可）：`Hash(WorldState)` = 对 `countries`/`provinces`/`units` 按 id 升序，将关键整数字段拼接后求稳定哈希（如 FNV-1a / `string.GetHashCode` 不可用——用自实现 FNV-1a 保跨运行稳定）。
- **同种子同结果**：用固定 seed 构建小世界（可注入 stub `IConfigRegistry` 或手搭 2 国），跑 N=5 回合（`AdvancePhase` 直到推进若干回合），记 `Hash`；重置重跑 → 哈希相等。
- **存档续跑等价**：A=连续跑 4 回合的 `Hash`；B=跑 2 回合 → `Save` → 新会话 `Load` → 再跑 2 回合的 `Hash`；断言 `A == B`。
- **RNG 状态往返**：见 P1。

### P5 — 收尾
- `CHANGELOG.md`（**UTF-8**）追加 `[T4]` 条目（关联规则 6,7；并记录"实现确定性决策、闭合存档"）。
- batchmode 编译 0 error + 全套 EditMode 全绿，导出 `artifacts/editmode-results.xml` 附 PR。开 PR 指派 Claude 审查。

## 4. 文件清单

| 动作 | 路径 |
|---|---|
| 改 | `Domain/Abstractions/IRandom.cs`（+State/+RestoreState）、`Domain/Random/RandomService.cs`（SplitMix64） |
| 改 | `Application/Persistence/SaveModels.cs`（+seed/rngState/phase）、`Application/Mapping/SaveMapper.cs`（ToSave 签名+写入） |
| 改 | `Domain/Time/GameClock.cs` + `Domain/Abstractions/ITurnClock.cs`（+Restore）、`Application/Session/GameSessionService.cs`（接线） |
| 改 | `Simulation/Tests/EconomyResolverTests.cs`（加非整除回归用例，Phase 0） |
| 删 | `Domain/Abstractions/IEventPublisher.cs`（墓碑，Phase 0） |
| 新增 | 确定性测试类（哈希工具 + 同种子 + 存档续跑等价） |
| 改 | `.gitignore`（+`*.log`/`artifacts/`/`Temp/`/`UserSettings/`），清理散落 log（Phase 0） |

## 5. 验收门禁（DoD）

- [ ] Phase 0 全过（独立分支、C1 回退确认 + 非整除回归用例、删墓碑、仓库清理、全绿）。
- [ ] `RandomService` 为 SplitMix64（常量与 §2 一致）；`IRandom` 有 `State`/`RestoreState`；现有性质测试仍绿。
- [ ] `GameState` 含 `seed`/`rngState`/`phase`；`SaveMapper.ToSave` 实际写入（不再丢弃）。
- [ ] `GameClock.Restore` + 会话 Save/Load 正确恢复 RNG 状态、回合、阶段。
- [ ] **同种子同结果** 与 **存档续跑等价** 两个确定性测试通过。
- [ ] batchmode 0 error；EditMode 全绿（附 results）。
- [ ] **未改任何游戏数值/公式**（规则 9/14）；战斗 float 未整数化（不在本单范围）。
- [ ] 改动在 `feature/t4-determinism-saveload`，PR 待审；`CHANGELOG.md`（UTF-8）已更新（规则 7,10）。

## 6. 歧义处理
遇本单未指定细节、或需定数值/玩法/算法常量之外的设计 → 停下标 `[需 Claude 决策]`/`[需人类定值]`，写进 PR 描述，继续其它独立步骤。**严禁**为迁就测试擅改公式或 PRNG 常量。

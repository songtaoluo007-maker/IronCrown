# P3a — 确定性回放系统 + 黄金回放回归测试（Phase 3 前置基础设施）

## 地位 / 依赖
**Phase 3 前置基础设施,独立于 P2,可并行**(建议 OpenClaw 在 P2 地图大单之间插空做)。**不依赖 P2.6 验证**——确定性是已锁定的核心资产(`Design/PRODUCT_DIRECTION.md` §6 / `ARCHITECTURE.md` 附录 D)。

## 背景 / 为什么现在做
确定性(整数 + SplitMix64,遍历按 id 升序,随机走注入 `IRandom`)是**异步 PvP / 回放 / 反作弊 / 观战**的地基。但目前"确定性"只是隐性属性、缺乏自动守护(三轮收口才发现 GachaResolver 一度用过 `Guid`)。本单:① 建回放系统(记录命令流 → 同种子重放,产出相同世界);② 建**黄金回放回归测试**,把确定性变成 CI 每次拦的硬约束。这是 P3 PvP 的真正前置,且越早建越能在 P2 改动中持续守护确定性。

## 范围
1. **`ReplayData`(新 `Application/Replay/ReplayData.cs`,DTO)**:
   - `int seed`、`string initialConfigId/version`(起始世界来源)、`List<TurnCommands> turns`(每回合的 `GameCommand` 序列,按发出顺序)。
2. **`ReplayRecorder`(新)**:旁路记录——`GameSessionService.IssueCommand` 每次把命令追加到当前回合;回合推进时切下一回合。**可开关**(默认录制,不影响性能/确定性)。
3. **`ReplayPlayer`(新)**:给定 `ReplayData` → 用同 `seed` 初始化世界(经现有 `WorldInitializer`)→ 按记录的命令序列依次 `IssueCommand` + 推进回合 → 产出最终 `WorldState`。
4. **record→replay 等价验证**:玩一局(发命令+推回合)→ 记录 → 用 ReplayPlayer 重放 → `HashWorld(original) == HashWorld(replayed)`。
5. **黄金回放回归测试**:固定一段脚本化 `ReplayData`(命令流,见数值)存为 fixture + 锁定基线 hash;测试跑 replay 断言 `hash == 基线`。**CI 守护**——任何破坏确定性的改动(如误用 `Guid`/浮点/无序遍历)会让它红。

## 数值 / 设计（Claude 代拟）
- **黄金脚本**(固定命令流,6 省当前经济循环):选国 `empire_north` → 建 2 民用厂 + 1 军用厂 → 造 1 步兵 → 推 5 回合 → 调税率档。种子固定 `20260608`。
- **基线刷新协议**:P2 玩法/地图落地会改变世界结构 → 黄金 hash 会变。这是**预期的回归基线维护**:每当蓄意改玩法,重新生成基线 + 在 PR 注明"基线因 X 变更刷新"(由 Claude 确认,防止把"真的破坏确定性"误当"正常变更"放过)。
- 回放**纯旁路**:绝不改玩法/数值/确定性逻辑。

## 文件清单
- 新 `Application/Replay/ReplayData.cs`(+ `TurnCommands` DTO)
- 新 `Application/Replay/ReplayRecorder.cs`
- 新 `Application/Replay/ReplayPlayer.cs`
- `Application/Session/GameSessionService.cs`:接入 recorder(旁路,可注入开关)
- 新 `Assets/Tests/EditMode/IronCrown.Application.Tests/ReplayTests.cs`
- fixture:黄金 `ReplayData`(JSON 或代码内构造)+ 基线 hash 常量

## 测试（ReplayTests）
- `RecordReplay_SameWorld`:玩一局 → 录 → 放 → `HashWorld` 等价。
- `GoldenReplay_MatchesBaseline`:黄金脚本 replay → hash == 锁定基线(确定性回归防线)。
- `Replay_SameSeed_TwiceIdentical`:同 ReplayData 放两次,结果逐字段一致。
- 不影响既有:`SaveLoadEquivalence`/`Determinism` 测试仍绿(回放旁路)。

## DoD
- [ ] ReplayData + Recorder + Player;record→replay `HashWorld` 等价
- [ ] 黄金回放测试 + 锁定基线;确定性破坏会让它红(可手动注入 Guid 验证一次它确实变红,再还原)
- [ ] 回放旁路**不改**玩法/数值/确定性;既有测试全绿
- [ ] **CI lint+test 必绿** + `artifacts/p3a-editmode.xml`(本轮)
- [ ] commit 后 push;CHANGELOG/PROJECT_STATE/Design 0 改动

## 严禁 / 不做
- 改任何 Resolver 公式 / 确定性逻辑 / 遍历顺序 / RNG。
- 让回放影响玩法或确定性(必须纯旁路)。
- 做 PvP 撮合 / 服务端 / 观战 UI(那是 P3 后续,需设计 + P2.6 验证)。
- 编辑治理/Design 文件;commit UserSettings。
- 报完成不附 artifact。

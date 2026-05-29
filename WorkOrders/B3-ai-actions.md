# 工作单 B3 — AI 行动（让世界有对手）

| 项 | 值 |
|---|---|
| 工作单号 | B3（B 阶段收官：AI 国家自主经济决策） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数值 | Claude（规则 13 + 经授权代拟 AI 阈值，规则 14 人类终审） |
| 分支 | `feature/b3-ai-actions` |
| 前置 | B1/B1.5/B2 完成（建厂/税率民生/地图可用） |
| 角色边界 | 规则 12：只实现本单。**勿改既有经济/政治数值与公式**；AI 阈值按配置。遇未覆盖点停 `[需 Claude 决策]` |

## 0. 目标与范围
让**非玩家国**每回合自主做简单经济决策（建工厂），使世界"活"起来、玩家有对抗感。**只做经济 AI（建厂），刻意最小**：不做军事/外交/调税 AI（无军队、AI 调税收益不明显），后续再扩。规则 AI（阈值），**无随机、纯确定性**。

## Phase 0
- 新建分支 `feature/b3-ai-actions`；起点全套测试全绿。
- 不直接编辑 `CHANGELOG.md`（写 PR 描述）；UTF-8。
- **顺手修 B2 小瑕疵**：`MainHud.uss` 的 `.map-area` 高度调够 3 行（约 360px），使最下排省份方块（珊瑚湾）不与详情栏重叠。

## 1. 数值（Claude 已写入 economy.json，勿改）
`economy.json` global 行已加：
- `aiBuildCapitalThreshold`: `60` —— AI 国 capital 高于此值才考虑建厂（留生产缓冲）
- `aiMaxCivilianFactories`: `20` —— AI 民用厂上限
- `aiMaxMilitaryFactories`: `15` —— AI 军用厂上限

## 2. 架构决策（写死）
1. **小重构（经批准，规则 9 例外）：建造执行逻辑收敛到 Simulation**。当前 `GameSessionService.IssueCommand`（Application）里直接"扣 capital + EnqueueBuild"属玩法逻辑、应在 Simulation（规则 3）。本单把它下移为 `ConstructionResolver.TryBuild(country, kind, eco) -> bool`（含资源校验+扣费+入队），玩家命令(`IssueCommand`)与 AI(`AIResolver`)都调用它，消除重复、统一路径。
2. **AI 与玩家同构**：都通过 `ConstructionResolver.TryBuild` 改世界，保证可测、确定。
3. **玩家国不被 AI 接管**：运行时世界需知道谁是玩家 → `WorldState` 加 `string playerCountryId`；`GameSessionService.NewGame/Load` 设置；`AIResolver` 跳过它。
4. **确定性**：AI 遍历国家按 id 升序（`TurnResolver` 已 OrderBy）；AI 决策为纯阈值规则，无随机。

## 3. 实现规格

### 3.1 ConstructionResolver — 新增 TryBuild（执行逻辑下移）
```
public bool TryBuild(CountryState c, string kind, EconomyConfig eco) {
    int cost = kind == "civilian" ? eco.civilianFactoryBuildCost : eco.militaryFactoryBuildCost;
    if (c.GetResource("capital") < cost) return false;
    c.ModifyResource("capital", -cost);
    EnqueueBuild(c, kind, eco);   // 复用现有
    return true;
}
```

### 3.2 GameSessionService.IssueCommand — 改调 TryBuild
- `BuildCivilianFactory`/`BuildMilitaryFactory` 两个 case 改为：玩家/资源校验后调 `_construction.TryBuild(country, "civilian"/"military", eco)`；返回值 false 时 `Reject("资本不足")`。**行为与现状一致**（玩家校验仍在 IssueCommand）。

### 3.3 WorldState + 会话
- `WorldState` 加 `public string playerCountryId;`
- `GameSessionService.NewGame`：设 `_world.playerCountryId = _playerCountryId;`（在选国确定后）。`SetPlayerCountry` 也同步 `_world.playerCountryId`。`Load` 后同步。
- `SaveMapper`：`GameState` 已有 `playerCountryId`；`ToRuntime` 时把它写入 `world.playerCountryId`（保证读档后 AI 仍跳过玩家国）。

### 3.4 AIResolver — 经济决策（替换现有空壳桩）
- 构造注入 `IConfigRegistry`（当前无参，改注入）。
- `MakeDecisions(CountryState country, WorldState world)`：
  ```
  if (country.id == world.playerCountryId) return;   // 跳过玩家
  var eco = _config.Get<EconomyConfig>("global"); if (eco==null) return;
  // 规则：capital 充足且民用厂未达上限 → 建民用厂；否则尝试军用厂
  if (country.GetResource("capital") >= eco.aiBuildCapitalThreshold) {
      if (country.civilianFactories < eco.aiMaxCivilianFactories)
          _construction.TryBuild(country, "civilian", eco);
      else if (country.militaryFactories < eco.aiMaxMilitaryFactories)
          _construction.TryBuild(country, "military", eco);
  }
  ```
- `AIResolver` 注入 `ConstructionResolver`（容器已注册）。
- **保留现有 AIStrategy/AICampaign 等桩**（不删，未来军事 AI 用），仅实现 `MakeDecisions` 的经济部分；战役/战术 TODO 保留。

### 3.5 接线
- `AIResolver` 构造签名变（+IConfigRegistry +ConstructionResolver）→ 更新 `GameLifetimeScope` 注册 + `TurnResolver` 注入不变（已持有 ai/construction）+ **所有 `new AIResolver()` 测试调用点改为新签名**。
- AI 决策时机：`TurnResolver.ExecuteMilitary` 现已调 `_ai.MakeDecisions`——经济建厂决策放这里可接受（AI 在自己回合统一决策）。建造在结算阶段 `ResolveConstruction` 推进（已有）。确认顺序：内政(玩家+AI产出) → 军事(AI决策建厂入队) → 结算(推进建造)。**注意**：AI 在军事阶段 TryBuild 扣费+入队，同回合结算阶段推进——一致。

## 4. 测试（规则 6）
- **EditMode `AIResolver`**：构造带真实 EconomyConfig + ConstructionResolver；给一个非玩家国 capital=200 → `MakeDecisions` 后 capital 减少、`constructionQueue` +1；capital<阈值 → 不建；`civilianFactories>=上限` → 转建军用。
- **EditMode 玩家豁免**：`world.playerCountryId="X"`，对 X 调 `MakeDecisions` → 无变化。
- **EditMode `ConstructionResolver.TryBuild`**：capital 够→扣费+入队+true；不够→false 不扣。
- **EditMode `IssueCommand` 回归**：玩家建厂仍正确（走 TryBuild）。
- **续跑等价**：`SaveLoadEquivalenceTests` 含 AI——多国跑数回合（AI 自主建厂）→ 存→读→续跑，世界哈希等价（验证 AI 决策确定性 + playerCountryId 存档）。
- **PlayMode 冒烟**：连续推进 ~5 回合后，至少一个 AI 国 `civilianFactories` 增加（经地图点击该国省份详情或 WorldView 验证）。保留渲染前提断言。

## 5. 文件清单
| 动作 | 路径 |
|---|---|
| 改 | `Simulation/ConstructionResolver.cs`(+TryBuild)、`Simulation/AIResolver.cs`(注入+经济决策)、`Application/Session/GameSessionService.cs`(IssueCommand 调 TryBuild + 设 playerCountryId) |
| 改 | `Domain/State/WorldState.cs`(+playerCountryId)、`Application/Mapping/SaveMapper.cs`(ToRuntime 写 playerCountryId) |
| 改 | `Bootstrap/GameLifetimeScope.cs`(AIResolver 注入)、所有 `new AIResolver()` 调用点 |
| 改 | `MainHud.uss`(.map-area 高度修重叠) |
| 改/增 | 上述测试 |

## 6. 验收门禁（DoD）
- [ ] Phase 0：独立分支、起点全绿、UTF-8、地图重叠已修。
- [ ] 建造执行逻辑在 `ConstructionResolver.TryBuild`；玩家命令与 AI 共用；`IssueCommand` 行为不变（规则 3）。
- [ ] `WorldState.playerCountryId` 设置正确；**AI 跳过玩家国**；AI 决策全取自配置（零硬编码，规则 5）、纯阈值无随机（确定性）。
- [ ] Play `Main`：连续推进数回合，**AI 国工厂数自主增长**（点其省份/详情可见）；玩家国不被 AI 操作（**截图或日志为证**）。
- [ ] 新增/回归 EditMode + 续跑等价 + PlayMode 全绿；batchmode 0 error；导出**本次新** `artifacts/*.xml`。
- [ ] 未做军事/外交/调税 AI（不越范围）；未改既有经济/政治数值公式；PR 在 `feature/b3-ai-actions`；changelog 写 PR 描述。

## 7. 歧义处理
AI 决策时机/优先级、playerCountryId 同步点若本单未唯一确定 → 选最小确定实现并 PR 说明；涉及 AI 行为数值/玩法 → `[需人类定值]`/`[需 Claude 决策]`。**严禁**给 AI 引入随机或未配置的魔法数。

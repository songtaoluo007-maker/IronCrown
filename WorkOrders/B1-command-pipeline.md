# 工作单 B1 — 命令管线 + 选国 + 建造工厂（可玩性地基）

| 项 | 值 |
|---|---|
| 工作单号 | B1（可玩性第一步：让玩家能改变世界） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数值 | Claude（规则 13 + 经授权代拟建造数值，规则 14 人类终审） |
| 分支 | `feature/b1-command-pipeline`（独立分支） |
| 前置 | MVP 切片已达成（EditMode 69/69 + PlayMode 5/5 全绿） |
| 角色边界 | 规则 12：只实现本单。**勿改既有经济数值/公式**；命令/选国按本单结构。遇未覆盖点停 `[需 Claude 决策]`/`[需人类定值]` |

## 0. 目标与刻意收窄
立**命令管线骨架** + **新游戏选 1 国** + **唯一一个命令"建造工厂"**端到端打通：玩家在 HUD 选国 → 点"建民用厂/军用厂" → 命令经 `GameSessionService` 校验+入队 → 结算阶段推进建造进度 → 完工 `+1` 工厂 → HUD 刷新。
**调生产/税收民生留 B1.5、科技树等人类单独设计——本单不做**（范围越小越稳）。

## Phase 0
- 新建分支 `feature/b1-command-pipeline`；先跑全套测试确认 74 全绿（EditMode 69 + PlayMode 5）。
- 不直接编辑 `CHANGELOG.md`（changelog 写 PR 描述，Claude 合入）。
- UTF-8：所有新建/修改文件以 UTF-8（无 BOM）保存（`EncodingGuardTests` 会校验）。

## 1. 架构决策（已写死）

1. **命令 = Contracts 里的只读 DTO**（规则 4：UI 只依赖 Contracts）。命名空间 `IronCrown.Contracts`，仅含基元/字符串。
2. **命令入口 = `GameSessionService`**（UI 唯一合法入口）。MVP **不引入命令总线/反射分发**——用显式方法 `IssueCommand(...)` + 一个 `switch`，够用且最简。
3. **命令校验与执行分离**：`GameSessionService` 负责校验（资源够不够、是不是玩家国）；实际状态变更走 **Simulation 层**新增的 `ConstructionResolver`（规则 3：玩法逻辑在 Simulation）。
4. **建造为多回合**：下令即**扣资源 + 入在建队列**；每回合**结算阶段**推进进度，满则 `+1` 对应工厂、出队。取消命令**不退资源**（MVP 简化，避免经济漏洞）。
5. **玩家国**：`GameSessionService` 持 `string playerCountryId`；`NewGame(seed, playerCountryId)`。命令只允许作用于玩家国（AI 国 B3 再说）。
6. **数值全从 Config**（规则 5）：建造成本/工期写入 `economy.json` 的 `global` 行（Claude 已加，见 §2）。

## 2. 数值（Claude 已写入 economy.json，勿改）
`economy.json` 的 `global` 行**新增**字段（本单随附，OpenClaw 勿改数值）：
- `civilianFactoryBuildCost`: 资本 **30**（一次性，下令即扣）
- `militaryFactoryBuildCost`: 资本 **40**
- `factoryBuildTurns`: **3**（回合）
> 解释：MVP 用"资本"作通用建造货币（已有资源），不引入"建设产能/民用工厂占用"等复杂度（后续可加）。

## 3. 实现规格

### 3.1 Contracts — 命令 DTO（`Contracts/Commands/`）
```
namespace IronCrown.Contracts {
    public enum CommandType { BuildCivilianFactory, BuildMilitaryFactory }
    public sealed class GameCommand {
        public string commandType;   // CommandType.ToString()（Contracts 不引用枚举可用 string；用枚举亦可，二选一保持一致）
        public string countryId;     // 目标国（MVP 必须 == playerCountryId）
        // MVP 无额外参数；后续命令在此扩展
    }
    public sealed class CommandResult {
        public bool accepted;
        public string reason;        // 被拒原因（资源不足/非玩家国/未知命令），accepted=true 时为空
    }
}
```

### 3.2 Domain — 在建队列状态
- `Domain/State/Country.cs`（`CountryState`）新增：
  ```
  public System.Collections.Generic.List<ConstructionOrder> constructionQueue = new();
  ```
- 新建 `Domain/State/ConstructionOrder.cs`：
  ```
  namespace IronCrown.Domain {
      public sealed class ConstructionOrder {
          public string factoryKind;   // "civilian" | "military"
          public int turnsRemaining;
      }
  }
  ```

### 3.3 Simulation — `ConstructionResolver`（玩法逻辑，规则 3）
- 新建 `Simulation/ConstructionResolver.cs`，构造注入 `IConfigRegistry`、`IEventPublisher`。
- `void EnqueueBuild(CountryState c, string factoryKind, EconomyConfig eco)`：扣资本（按 kind 取 cost）、`constructionQueue.Add(new ConstructionOrder{factoryKind, turnsRemaining=eco.factoryBuildTurns})`。**调用方已校验资源充足**。
- `void ResolveConstruction(CountryState c, WorldState world)`：遍历 `constructionQueue`（按加入顺序，确定性），每项 `turnsRemaining--`；==0 则 `civilianFactories++`/`militaryFactories++` 并标记移除；最后移除已完成项。
- 接入回合流水线：`TurnResolver.ExecuteSettlement` 中，对每国（已按 id 升序）在 `_economy.ResolveEconomy` 之后调 `_construction.ResolveConstruction(country, world)`。`TurnResolver` 构造注入 `ConstructionResolver`；`GameLifetimeScope` 注册。

### 3.4 Application — 命令入口
- `GameSessionService`：
  - 字段 `private string _playerCountryId;`
  - `NewGame(int? seed = null, string playerCountryId = null)`：建好世界后，`_playerCountryId = playerCountryId ?? <countries 按 id 升序第一个>;`
  - 新增 `public string PlayerCountryId => _playerCountryId;`
  - 新增：
    ```
    public CommandResult IssueCommand(GameCommand cmd) {
        if (_world == null) return reject("no world");
        if (cmd.countryId != _playerCountryId) return reject("非玩家国");
        var c = _world.countries[cmd.countryId];
        var eco = _config.Get<EconomyConfig>("global");
        switch (parse cmd.commandType) {
          case BuildCivilianFactory:
             if (c.GetResource("capital") < eco.civilianFactoryBuildCost) return reject("资本不足");
             c.ModifyResource("capital", -eco.civilianFactoryBuildCost);
             _construction.EnqueueBuild(c, "civilian", eco);
             return accept;
          case BuildMilitaryFactory: 同理用 militaryFactoryBuildCost / "military";
          default: return reject("未知命令");
        }
    }
    ```
  - `GameSessionService` 构造注入 `ConstructionResolver`（用于 `EnqueueBuild`）。
- `EconomyConfig`（`Domain/Config/EconomyConfig.cs`）新增三字段：`civilianFactoryBuildCost`/`militaryFactoryBuildCost`/`factoryBuildTurns`。
- `ConfigValidationTests`：补断言这三字段 ≥ 0、`factoryBuildTurns` ≥ 1。

### 3.5 Contracts ReadModel — 暴露在建/玩家国
- `CountryView` 新增 `int constructionQueueCount;`（在建项数）。`ReadModelBuilder.BuildCountryView` 填 `c.constructionQueue.Count`。
- `WorldView` 新增 `string playerCountryId;`。`ReadModelBuilder.BuildWorldView` 增参 `playerCountryId` 或由 `GameSessionService.GetWorldView()` 填入。

### 3.6 Presentation — 选国 + 建造按钮
- **选国**：HUD 顶部加一个下拉/按钮组列出 6 国；选中即 `GameSessionService.SetPlayerCountry(id)`（或重开 `NewGame(seed, id)`——二选一，推荐加轻量 `SetPlayerCountry` 只改 `_playerCountryId` 不重置世界）。当前玩家国高亮。
- **建造按钮**：HUD 加「建民用厂」「建军用厂」两个按钮，点击 → `_session.IssueCommand(new GameCommand{commandType=..., countryId=session.PlayerCountryId})` → 据 `CommandResult.accepted` 刷新或提示原因（被拒时把 `reason` 显示在一个 `status-label`）。
- 国家行：玩家国那行标注「(玩家)」并显示「在建: N」。
- `MainHudController` 新增方法 `BuildCivilian()`/`BuildMilitary()`/`SelectCountry(id)`（与按钮回调共用，便于测试，沿用 A 阶段 `Advance()` 的可测模式）。

## 4. 测试（规则 6）
- **EditMode `ConstructionResolver` 单测**：下令建民用厂→扣资本=cost、队列+1；推进 `factoryBuildTurns` 回合后 `civilianFactories+1`、队列清空；不足回合不完工。数值取自配置。
- **EditMode `GameSessionService` 命令测**（用 TestConfigRegistry stub）：资本充足→`accepted`；不足→`accepted=false`+reason；对非玩家国→拒绝。
- **EditMode 确定性回归**：`SaveLoadEquivalenceTests` 扩展——`constructionQueue` 需纳入存档（见 §5）后，续跑等价仍成立。
- **PlayMode 冒烟扩展**（沿用 `Controller.Advance()` 可测模式）：选国→`Controller.BuildCivilian()`→断言该国 `capital` 减少且「在建」+1；推进 `factoryBuildTurns` 个完整回合→`civilianFactories` +1。

## 5. 存档完整性（规则：续跑确定性不能破）
- `CountrySaveData` 新增 `constructionQueue`（list of {factoryKind, turnsRemaining}）+ `usedCivilianFactories`/`usedMilitaryFactories`（若 ResolveProduction 用到）。`SaveMapper` 双向映射。`GameState` 增 `playerCountryId`。
- 验收：`SaveLoadEquivalenceTests` 含"下建造令→存→读→续跑"仍等价。

## 6. 文件清单
| 动作 | 路径 |
|---|---|
| 新增 | `Contracts/Commands/GameCommand.cs`、`CommandResult.cs`（+CommandType） |
| 新增 | `Domain/State/ConstructionOrder.cs`、`Simulation/ConstructionResolver.cs` |
| 改 | `Domain/State/Country.cs`(+queue)、`Domain/Config/EconomyConfig.cs`(+3字段)、`economy.json`(Claude 已加) |
| 改 | `Application/Session/GameSessionService.cs`(选国+IssueCommand+注入)、`Application/Queries/ReadModelBuilder.cs`、`Contracts/ReadModels/{WorldView,CountryView}.cs` |
| 改 | `Simulation/TurnResolver.cs`(注入+结算调用)、`Bootstrap/GameLifetimeScope.cs`(注册 ConstructionResolver) |
| 改 | `Application/Persistence/SaveModels.cs` + `Mapping/SaveMapper.cs`(在建队列+playerCountryId) |
| 改 | `Presentation/MainHudController.cs` + `MainHud.uxml/uss`(选国+建造按钮+status-label) |
| 改/增 | 上述各测试 |

## 7. 验收门禁（DoD）
- [ ] Phase 0：独立分支、起点 74 全绿、UTF-8。
- [ ] 命令 DTO 在 Contracts；`GameSessionService.IssueCommand` 校验（资源/玩家国）正确；执行走 `ConstructionResolver`（规则 3/4/5 守住）。
- [ ] Play `Main`：选国→点「建民用厂」→资本扣减、「在建」+1→推进 3 回合→民用厂 +1，HUD 全程刷新（**截图为证**）。
- [ ] 新增 EditMode 单测 + PlayMode 冒烟全绿；`SaveLoadEquivalenceTests`（含在建队列）仍绿。
- [ ] batchmode 0 error；导出**本次新** `artifacts/editmode-results.xml` + `playmode-results.xml`。
- [ ] 未改既有经济数值/公式；未引入命令总线/科技/调生产（不越范围，规则 9）。
- [ ] PR 在 `feature/b1-command-pipeline`；changelog 写 PR 描述。

## 8. 歧义处理
选国 UI 形式、`SetPlayerCountry` vs 重开、命令 `commandType` 用枚举还是字符串等若本单未唯一确定 → 选最小实现并在 PR 说明；涉及数值/玩法取舍 → `[需人类定值]`/`[需 Claude 决策]`。

# Phase1-closeout — Phase 1 收口（合入 main 前的必修堵口）

## 背景
Claude 对 `feature/c5-diplomacy-peace`（C5→C17，57 commit / 196 文件 / +14709/-542）做 Phase 1 闭合审查，**打回**。核心代码大体可用，但存在 **3 个上线红线 + 证据/卫生缺口**，合入 main 前必须一次过收口。本单只做收口，**不加任何新玩法/新数值**。

执行方：OpenClaw。分支：继续 `feature/c5-diplomacy-peace`（PR #1）。
治理文件（CHANGELOG / PROJECT_STATE / PROJECT_RULES / ARCHITECTURE / PR 描述）由 **Claude 维护，OpenClaw 一律不碰**。

> 行号为定位参考，以实际文件为准。歧义处理：若任一改动触发"需改公式/数值/接口契约之外的连锁"，立刻停下标 `[需 Claude 决策]`，不擅自扩面。

---

## 范围总表
| 编号 | 项 | 优先级 | 性质 |
|---|---|---|---|
| **F1** | SaveMapper 持久化将领 + 抽卡状态 | 🔴 P0（上线红线） | 存档完整性 |
| **F2** | HashWorld 纳入 F1 新字段 + 续跑等价测试 | 🔴 P0（配套） | 测试防线 |
| **F3** | GachaResolver 去 `Guid`、确定性 id | 🔴 P0 | 确定性 |
| **F4** | C15a-fix 真落地（军衔名 + maxDivisions 公式） | 🔴 P0 | 跳过的指派 |
| **F5** | ShopResolver 事件 `atTurn` 接真实回合 | 🟡 P2 | 小项 |
| **F6** | 仓库卫生：删 log + 还原 ProjectSettings/Packages | 🟡 P2 | 卫生 |
| **F7** | 运行证据：最终 EditMode/PlayMode XML + 5 张 Play 截图 | 🟡 P2 | 证据 |

---

## F1 — SaveMapper 持久化将领 + 抽卡状态（🔴 上线红线）

**问题**：`SaveMapper.cs` 对 `world.commanders`、`country.gachaTickets/gachaPityCounter`、`unit.commanderId` 零读零写 → 存读档后玩家所有将领/券/星级/任命清零。

### F1.1 新增 DTO（`Application/Persistence/SaveModels.cs`）
新增类：
```csharp
[Serializable]
public class CommanderSaveData
{
    public string id;
    public string name;
    public string ownerCountry;
    public string generalCardId;
    public int rank;
    public int victories;
    public int encirclements;
    public int baseAttack;
    public int baseDefense;
    public int maxDivisions;
    public int starLevel;
    public bool isActive;
}
```
`GameState` 加字段：`public CommanderSaveData[] commanders;`
`CountrySaveData` 加字段：`public int gachaTickets;` `public int gachaPityCounter;`
`UnitSaveData` 加字段：`public string commanderId;`

> `commanderIds` **不单独存**——与 `unitIds` 同模式，ToRuntime 从 commanders 按 owner 重建（保持规则 3 单一真相）。

### F1.2 `SaveMapper.ToSave`
- `CountrySaveData` 初始化里加：`gachaTickets = c.gachaTickets, gachaPityCounter = c.gachaPityCounter,`
- `UnitSaveData` 初始化里加：`commanderId = u.commanderId,`
- 在 `units` 块之后、`activeBattles` 之前，新增：
```csharp
state.commanders = world.commanders.Values
    .OrderBy(c => c.id, System.StringComparer.Ordinal)
    .Select(c => new CommanderSaveData
    {
        id = c.id, name = c.name, ownerCountry = c.ownerCountry,
        generalCardId = c.generalCardId, rank = c.rank,
        victories = c.victories, encirclements = c.encirclements,
        baseAttack = c.baseAttack, baseDefense = c.baseDefense,
        maxDivisions = c.maxDivisions, starLevel = c.starLevel, isActive = c.isActive
    }).ToArray();
```

### F1.3 `SaveMapper.ToRuntime`
- `CountryState` 初始化里加：`gachaTickets = cd.gachaTickets, gachaPityCounter = cd.gachaPityCounter,`
- `UnitState` 初始化里加：`commanderId = ud.commanderId,`
- 在 units 重建之后、`重建 country.unitIds` 那段附近，新增 commanders 重建 + commanderIds 重建：
```csharp
// 将领（C15a/C16）
if (save.commanders != null)
{
    foreach (var cd in save.commanders)
    {
        world.commanders[cd.id] = new CommanderState
        {
            id = cd.id, name = cd.name, ownerCountry = cd.ownerCountry,
            generalCardId = cd.generalCardId, rank = cd.rank,
            victories = cd.victories, encirclements = cd.encirclements,
            baseAttack = cd.baseAttack, baseDefense = cd.baseDefense,
            maxDivisions = cd.maxDivisions, starLevel = cd.starLevel, isActive = cd.isActive
        };
    }
}
// 重建 commanderIds（不读存档，从 commanders 按 owner 升序重建）
foreach (var c in world.countries.Values) c.commanderIds.Clear();
foreach (var cmdr in world.commanders.Values.OrderBy(c => c.id, System.StringComparer.Ordinal))
    if (world.countries.TryGetValue(cmdr.ownerCountry, out var owner))
        owner.commanderIds.Add(cmdr.id);
```

> 旧存档无 `commanders` 字段 → Newtonsoft 容错读为 null → 跳过，无将领，符合预期（C-2 技术债延续既定决策：未发布、不做迁移管线）。

---

## F2 — HashWorld 纳入新字段 + 续跑等价测试（🔴 配套）

**问题**：若 `SaveLoadEquivalenceTests.HashWorld` 不含将领/抽卡字段，F1 的续跑等价就是盲区（C1 的老毛病重演）。

### F2.1 扩 HashWorld
在 `SaveLoadEquivalenceTests` 的 `HashWorld`（或等价快照函数）中追加：
- 每个 country（按 id 升序）：`gachaTickets`、`gachaPityCounter`
- 每个 commander（`world.commanders` 按 id 升序）：`id|ownerCountry|generalCardId|rank|victories|encirclements|baseAttack|baseDefense|maxDivisions|starLevel|isActive`
- 每个 unit 追加：`commanderId`

### F2.2 新增续跑等价测试
`SaveLoadEquivalenceTests` 新增：
- `SaveLoad_CommanderAndGacha_SurvivesRoundTrip`：建世界 → 给玩家 `gachaTickets` → 抽卡造将领 → `AssignDivision` 任命 1 师 → 再抽同卡升星 → `ToSave` → `ToRuntime` → `HashWorld(before) == HashWorld(after)`。
- `SaveLoad_RunForward_WithCommanders_Equivalent`：抽卡+任命后，跑 2 回合→存→读→再跑 2 == 直跑 4（HashWorld 相等）。

---

## F3 — GachaResolver 确定性 id（🔴 上线红线）

**问题**：`GachaResolver.CreateCommanderFromCard`（约 L189）用 `Guid.NewGuid()` 生成 commander id → 违反确定性（同种子两次抽卡 id 不同），且与 `CommanderResolver` 的 `_nextCommanderId++` 双轨。**附加隐患**：`CommanderResolver._nextCommanderId` 读档后重置为 1，会与存档已有 id 撞号。

### F3.1 `CommanderResolver` 提供确定性 id 生成
删除实例字段 `_nextCommanderId`。新增：
```csharp
/// <summary>确定性生成 commander id：扫描该国现有最大序号 +1（读档后仍唯一）</summary>
public string GenerateCommanderId(WorldState world, string countryId)
{
    int max = 0;
    foreach (var c in world.commanders.Values)
    {
        if (c.ownerCountry != countryId) continue;
        var parts = c.id.Split('_');
        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int n))
            max = System.Math.Max(max, n);
    }
    return $"cmdr_{countryId}_{max + 1}";
}
```

### F3.2 `RecruitCommander` 改签名 + 用新生成器
- 签名改：`public CommanderState RecruitCommander(CountryState country, string configId, WorldState world)`
- 内部 id 改：`string id = GenerateCommanderId(world, country.id);`
- **关键**：当前 `RecruitCommander` 创建后只 `country.commanderIds.Add(id)`，**没把 commander 放进 `world.commanders`**（潜伏 bug：招募的将领进不了世界字典 → 战斗/存档都找不到）。本单一并修：`world.commanders[id] = commander;`
- 调用点 `GameSessionService.cs` L226：`_commander.RecruitCommander(country, cmd.configId, _world)`

### F3.3 `GachaResolver.CreateCommanderFromCard` 去 Guid
- 改 id 行：`string id = _commander.GenerateCommanderId(world, country.id);`
- 移除 `using System;` 若仅为 Guid 所用（保留若他处需要）。
- 构造函数 `_commander` 必须非 null：DI 注册保证；本单**确认 `GameLifetimeScope` 中 `CommanderResolver` 为单例**，`GachaResolver` 与 `GameSessionService` 注入的是**同一实例**（否则 id 生成与招募不一致）。

### F3.4 确定性测试
`GachaResolverTests` 新增 `DrawCard_SameSeed_ProducesSameCommanderIds`：两个独立 world 同种子各抽 20 次（券充足），逐一断言两边新建 commander.id 序列完全相等。

---

## F4 — C15a-fix 真落地（🔴 跳过的明确指派）

**问题**：`WorkOrders/C15a-fix-rank-naming-capacity.md` 已签发但**从未执行**——全仓仍是"尉官/校官/将官/大元帅"，`maxDivisions = 5 + rank`。

### F4.1 `Domain/State/CommanderState.cs`
- L17 注释改：`// === 军衔（0=少将 → 1=中将 → 2=上将 → 3=大将 → 4=元帅） ===`
- L27 注释改：`// 麾下师上限（= rank + 1，少将1 → 元帅5）`
- L39：`public static readonly string[] RankNames = { "少将", "中将", "上将", "大将", "元帅" };`
- L58 `TryPromote` 内：`maxDivisions = rank + 1;`（删 `5 + rank`）

### F4.2 `Simulation/CommanderResolver.cs`
- L57 fallback：`maxDivisions = cfg.baseMaxDivisions > 0 ? cfg.baseMaxDivisions : 1`（`5`→`1`）

### F4.3 `Contracts/ReadModels/CommanderView.cs`
- L14 注释改为 `"少将"/"中将"/"上将"/"大将"/"元帅"`

### F4.4 测试断言更新
- `CommanderResolverC15aTests.cs`：所有 `"大元帅"` 期望 → 对应新名（rank 4 = `"元帅"`）；maxDivisions 期望：rank0→1, rank1→2, rank2→3, rank3→4, rank4→5。
- 全仓 grep `尉官\|校官\|将官\|大元帅` 应**彻底无**（含注释，注释也已改新名）。

---

## F5 — ShopResolver 事件 `atTurn`（🟡 小项）

**问题**：`ShopResolver` 三处 `atTurn = 0` 写死占位，事件回放无法分辨购买回合。

- `BuyBundle` / `BuySsrTicket` / `BuySpecificCardTicket` 各加参数 `int currentTurn`，`ShopPurchasedEvent.atTurn = currentTurn`。
- 调用点 `GameSessionService.cs` L272/286/303：末参传 `_world.turnNumber`。
- `ShopResolverTests` 相应补传 turn（断言事件 atTurn == 传入值）。

---

## F6 — 仓库卫生（🟡）

### F6.1 删散落 log
- 删本地 `compile_errors.log`（被 .gitignore，不入库，物理删除即可）。
- 确认仓库根无其它散落 `*.log`。

### F6.2 还原 ProjectSettings / Packages
`feature` 分支带着 C9a 期遗留的 `ProjectSettings/ProjectSettings.asset`（13 行）+ `Packages/manifest.json`（缩进 + 模块增删）改动，违各单"0 改动"约定。
```
git checkout origin/main -- ProjectSettings/ProjectSettings.asset Packages/manifest.json Packages/packages-lock.json
```
**还原后必须重新 batchmode 编译 + 跑 EditMode 验证 0 error / 全绿**，证明无真实依赖被还掉。
- 若编译失败（说明确有依赖）→ 立刻停下标 `[需 Claude 决策]`，附失败日志，**不擅自保留改动蒙混**。

> 注：`.gitignore` 已忽略 `ProjectSettings/` 之外路径，但这两文件是历史跟踪文件，需显式还原。Unity 重开可能再次改写它们——若还原后 Unity 一打开又脏，记录现象但不反复 commit。

---

## F7 — 运行证据（🟡，硬性）

**问题**：物理 artifacts 目录最新止于 `c13-ui2`（5/31 23:59）；C14/C15a/C15a-fix/C15b/C16/C17 + 本单全部修改无任何 EditMode/PlayMode 留存；Play 截图 0 张。

### F7.1 最终测试 XML（命名严守，只一对）
- `artifacts/phase1-closeout-editmode.xml`
- `artifacts/phase1-closeout-playmode.xml`
- 因 `.gitignore` 忽略 `artifacts/`，用 **`git add -f`** 强制纳入这两个文件。
- 时间戳必须**本轮**；EditMode 测试数 = 既有 269+ ∪ 本单新增（≥ 既有，不得倒退）；**PlayMode 真跑、非空**。

### F7.2 Play 截图 5 张（`Design/screenshots/`，PNG）
1. `phase1-rank-names.png` — 招募/抽到将领显示 `[少将] … 指挥1师`（验 F4：不是"尉官指挥5师/6师"）
2. `phase1-gacha-panel.png` — 抽卡面板抽到一张卡（稀有度颜色 + 名 + 技能）
3. `phase1-collection.png` — "我的将领"收藏页列表
4. `phase1-shop.png` — 商城面板 + 一次购买
5. `phase1-saveload.png` — **存档→读档后**将领列表 + gachaTickets + 星级仍在（验 F1：读档不清零）

---

## DoD Check List
- [ ] F1：SaveModels 加 `CommanderSaveData` + 3 处字段；ToSave/ToRuntime 双向；commanderIds 重建
- [ ] F2：HashWorld 含将领/抽卡/commanderId；2 个续跑等价测试通过
- [ ] F3：删 `_nextCommanderId`；`GenerateCommanderId` 落地；GachaResolver 去 Guid；RecruitCommander 加 world 参数 + 放进 world.commanders；DI 单例确认；确定性 id 测试通过
- [ ] F4：RankNames=少→帅；maxDivisions=rank+1；fallback=1；CommanderView 注释；全仓无 `尉官/校官/将官/大元帅`
- [ ] F5：ShopResolver 三方法接 currentTurn；事件 atTurn 真实
- [ ] F6：删 compile_errors.log；ProjectSettings/Packages 还原到 main 且编译+EditMode 验证通过
- [ ] F7：`phase1-closeout-{editmode,playmode}.xml`（`git add -f`，本轮时间戳）+ 5 张 Play 截图
- [ ] 既有 269+ EditMode + 7 PlayMode + 本单新增 **全绿**
- [ ] Unity Console 0 error
- [ ] **★ commit 完成后立即 `git push origin feature/c5-diplomacy-peace`**（PR #1 自动追加）
- [ ] PR 描述里附 changelog 文本（供 Claude 合入 CHANGELOG），但**不直接编辑 CHANGELOG.md**
- [ ] CHANGELOG.md / PROJECT_STATE.md / PROJECT_RULES.md / ARCHITECTURE.md **0 改动**（Claude 维护）

## 严禁
- 改任何战斗/补给/经济/外交 Resolver 的**公式或数值**（BattleResolver/SupplyResolver/WarTollResolver/PeaceResolver/EconomyResolver 等）——本单是收口，不碰平衡。
- 引入任何新平衡数值进 `economy.json` 或其它 config（`currentTurn` 不是平衡值）。
- 改军衔 buff 数值（`RankAttackBonusPct = rank * 5` 保持）、`maxDivisionsPerProvince = 5` 保持。
- 顺手做 Phase 2 任何内容（国策/决议/外交/贸易）。
- 直接编辑 CHANGELOG / PROJECT_STATE / PROJECT_RULES / ARCHITECTURE。
- 不 push / 堆 commit 不推。

## 完工后人类 Play 验证
1. 招募/抽到将领 → 显示 `[少将] 攻+0% 防+0% 指挥1师`（不是尉官/5师）。
2. 抽卡 → 面板正常 → 收藏页能看到该将领。
3. 任命 1 师给少将 → 第 2 师拒"麾下已满"。
4. 攒券 → 商城买 SSR 券 → 立得一张 SSR。
5. **存档 → 退出 → 读档** → 将领、星级、gachaTickets、师的任命**全部还在**（F1 验收核心）。
6. 推回合积累胜场 → 晋升中将 → `指挥2师`；一路到元帅 → `指挥5师`。

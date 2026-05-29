# 工作单 C1 — 领土与驻军地基（军事阶段第一步）

| 项 | 值 |
|---|---|
| 工作单号 | C1（军事阶段地基：邻接 + 初始部队 + 地图驻军展示） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数据 | Claude（规则 13 + 经授权代拟邻接/初始部队，规则 14 人类终审） |
| 分支 | `feature/c1-territory-garrison` |
| 前置 | B 阶段完成（112 全绿） |
| 角色边界 | 规则 12：只实现本单。**勿改既有经济/政治/AI 数值与公式**；邻接/部队按本单数据。遇未覆盖点停 `[需 Claude 决策]` |

## 0. 目标与范围
为军事系统立地基：① 省份**邻接关系**（谁挨着谁，移动/战斗的前提）；② 各国**初始部队**驻扎首都；③ 地图省份方块**显示驻军**、点省看邻接与驻军。**不做移动/战斗/占领**（C2/C3）。仍用现有 6 省（不扩省份数）。

## Phase 0
- 新建分支；起点全套测试全绿；不直接编辑 CHANGELOG（写 PR）；UTF-8。

## 1. 数据（OpenClaw 填入 JSON，勿改）

### 1.1 省份邻接 → `provinces.json` 每省加 `neighbors`（string[]，对称）
| province | neighbors |
|---|---|
| `iron_city` | `["wind_plain","high_peak"]` |
| `wind_plain` | `["iron_city","red_plain"]` |
| `liberty_port` | `["high_peak"]` |
| `high_peak` | `["iron_city","coral_bay","liberty_port","red_plain"]` |
| `red_plain` | `["wind_plain","high_peak"]` |
| `coral_bay` | `["high_peak"]` |
> `high_peak`（中部高峰）是枢纽（4 邻）；连通性满足 C2/C3 验证（如 liberty_port→high_peak→red_plain）。

### 1.2 初始部队（规则：每国 1 支步兵驻首都）
- WorldInitializer 为每国创建 1 支 `infantry`（用 `units.json` 的 `infantry` 模板），`ownerCountry`=该国、`currentProvinceId`=该国 `capitalProvinceId`，满编（manpower/equipment/organization = 模板最大值）。
- 部队 id 规则：`{countryId}_inf_1`（确定、唯一）。

## 2. 架构决策（写死）
1. 邻接是**静态省份数据**（来自 config）。
2. 初始部队由 `WorldInitializer` 按规则创建（数值取 `units.json` infantry 模板，规则 5）。
3. **存档完整性**：`ProvinceState.neighbors` 等静态字段读档后不能丢。本单 `ProvinceSaveData` 加 `neighbors`（连同确认 `gridX/gridY` 已在存档），`SaveMapper` 双向。units 已在存档（C1 初始部队会被存读）。
4. 规则 4：地图/详情只读 ReadModel；规则 3：无玩法逻辑泄漏到 UI。

## 3. 实现规格

### 3.1 Domain
- `ProvinceConfig` + `ProvinceState` 加 `public string[] neighbors;`
- `WorldInitializer.CreateNewGame`：映射 `neighbors`；并在省份/国家建好后，为每国创建初始步兵（查 `IConfigRegistry.Get<UnitConfig>("infantry")`，按模板 new `UnitState`，加入 `world.units`）。

### 3.2 Contracts — ProvinceView 扩展
- `ProvinceView` 加 `public string[] neighbors;` `public int garrisonCount;`（驻该省的部队数）。
- （可选）`public int garrisonManpower;` 驻军总人力，供详情显示。

### 3.3 Application
- `ReadModelBuilder.BuildWorldView`：构建 `ProvinceView` 时，`garrisonCount` = `world.units.Values.Count(u => u.currentProvinceId == province.id)`（有序、确定）；填 `neighbors`。
- `SaveMapper`：`ProvinceSaveData` 加 `neighbors`，`ToSave`/`ToRuntime` 双向；确认 `gridX/gridY` 也在双向映射（若 B2 漏了一并补——读档后地图位置+邻接必须完整）。

### 3.4 Presentation — 地图显示驻军 + 详情邻接
- 省份方块：右上角小标记显示驻军数（如 `⚔N`，N>0 时显示）。
- 选中省详情栏：追加「邻接: 省A, 省B」+「驻军: N 支」。
- 纯展示，复用现有 `Render()`；无新交互（移动是 C2）。

## 4. 测试（规则 6）
- **配置校验**：`neighbors` **对称性**（A∈B.neighbors ⟺ B∈A.neighbors）；每个 neighbor id 存在于 provinces；无自引用。
- **EditMode WorldInitializer**：新游戏后 `world.units.Count == 6`（每国 1 步兵）、每支驻在本国首都、属性=infantry 模板满编。
- **EditMode ReadModelBuilder**：`ProvinceView.garrisonCount` 正确（首都=1、非首都=0）、`neighbors` 正确。
- **续跑等价**：`SaveLoadEquivalenceTests` 覆盖——含部队 + neighbors，存→读→续跑世界哈希等价（验证 neighbors/gridX/gridY/units 存档完整）。
- **PlayMode 冒烟**：地图省份方块显示驻军标记；点首都省→详情含「驻军: 1」+ 邻接列表。

## 5. 文件清单
| 动作 | 路径 |
|---|---|
| 改（填数据） | `provinces.json`(+neighbors×6) |
| 改 | `Domain/Config/ProvinceConfig.cs`、`Domain/State/Province.cs`(+neighbors)、`Application/Setup/WorldInitializer.cs`(neighbors 映射+初始部队) |
| 改 | `Contracts/ReadModels/ProvinceView.cs`(+neighbors,+garrisonCount)、`Application/Queries/ReadModelBuilder.cs` |
| 改 | `Application/Persistence/SaveModels.cs`(ProvinceSaveData+neighbors)、`Mapping/SaveMapper.cs`(neighbors+确认 gridX/gridY 双向) |
| 改 | `Presentation/MainHudController.cs` + `MainHud.uss`(驻军标记+详情邻接) |
| 改/增 | 上述测试 + `ConfigValidationTests`(邻接对称) |

## 6. 验收门禁（DoD）
- [ ] Phase 0：独立分支、起点全绿、UTF-8。
- [ ] 邻接对称校验测试绿；6 省邻接按数据表；初始 6 支部队驻各国首都。
- [ ] Play `Main`：地图省份显示驻军标记；点省→详情显示邻接 + 驻军（**截图为证**）。
- [ ] 续跑等价测试覆盖部队+邻接+位置，全绿（读档后地图/邻接/驻军不丢）。
- [ ] 规则 4 守住（Presentation 不引用 Domain/Simulation）；数据全取自 config（规则 5）；未做移动/战斗（不越范围）；未改既有数值公式。
- [ ] batchmode 0 error；导出**本次新** `artifacts/*.xml`；PR 在 `feature/c1-territory-garrison`；changelog 写 PR 描述。

## 7. 歧义处理
驻军标记样式/详情格式等视觉 → 最小清晰实现，截图为准；邻接/部队数据或玩法 → `[需 Claude 决策]`/`[需人类定值]`。**严禁**给初始部队编造未配置数值（一律取 units.json 模板）。

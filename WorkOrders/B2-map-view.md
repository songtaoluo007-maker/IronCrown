# 工作单 B2 — 2D 省份地图（交互骨架）

| 项 | 值 |
|---|---|
| 工作单号 | B2（文字列表 → 可点击空间地图） |
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查/数据 | Claude（规则 13 + 经授权代拟地图位置/配色，规则 14 人类终审） |
| 分支 | `feature/b2-map-view` |
| 前置 | B1 + B1.5 完成（HUD 可见可操作、税率/民生生效） |
| 角色边界 | 规则 12：只实现本单。**勿改既有经济/政治数值与公式**；地图位置/配色按本单数据表填。遇未覆盖点停 `[需 Claude 决策]`/`[需人类定值]` |

## 0. 目标与范围（架构师判断）
把 6 国从**文字列表**变成**可点击的 2D 方块地图**：省份方块按方位摆放、按归属国配色、点击省份→高亮选中→侧栏显示该省详情。
**这是交互骨架，不是美术**：色块粗糙可接受，真正美术是 C 阶段。**不做**：邻接/领土争夺/占领/寻路（军事系统 B3 之后）、更多省份（内容填充后续）。地图位置/配色是 Claude 初版占位，人类 Play 后可调（规则 14）。

## Phase 0
- 新建分支 `feature/b2-map-view`；起点全套测试全绿。
- 不直接编辑 `CHANGELOG.md`（写 PR 描述）；UTF-8 保存。

## 1. 数据规格（OpenClaw 填入 JSON，勿改数值）

### 1.1 省份位置 → `provinces.json` 每省加 `gridX`/`gridY`（3×3 粗略方位，X 西→东 0-2，Y 北→南 0-2）
| province id | 所属国 | gridX | gridY |
|---|---|---|---|
| `iron_city` | empire_north（北） | 1 | 0 |
| `wind_plain` | steppe_junta（东北） | 2 | 0 |
| `liberty_port` | republic_west（西） | 0 | 1 |
| `high_peak` | federation_central（中） | 1 | 1 |
| `red_plain` | alliance_east（东） | 2 | 1 |
| `coral_bay` | kingdom_south（南） | 1 | 2 |

### 1.2 国家配色 → `countries.json` 每国加 `mapColor`（hex 字符串，原创）
| country id | mapColor | 色 |
|---|---|---|
| `empire_north` | `#8C5AAA` | 紫 |
| `republic_west` | `#4682C8` | 蓝 |
| `alliance_east` | `#C84646` | 红 |
| `kingdom_south` | `#C8A03C` | 金 |
| `federation_central` | `#50AAAA` | 青 |
| `steppe_junta` | `#828C46` | 橄榄 |

## 2. 架构决策（写死）
1. **地图用 UI Toolkit 画**（不引入 Tilemap/Sprite/新渲染体系，复用现有 `UIDocument`/HUD）：地图区是一个容器，省份为其中按 `gridX/gridY × cellSize` **绝对定位**的 `VisualElement` 方块。
2. **选中省份是会话状态**：`GameSessionService` 持 `string _selectedProvinceId`，`SelectProvince(id)` 设置。
3. **规则 4 不破**：地图视图只读 `Contracts` 的 ReadModel + 调 `GameSessionService`，不引用 Domain/Simulation。
4. **hex 颜色解析在 Presentation**（`UnityEngine.ColorUtility.TryParseHtmlString`）；ReadModel 里 `mapColor` 保持 string（Contracts 零依赖）。

## 3. 实现规格

### 3.1 Domain / Config
- `ProvinceConfig` 加 `public int gridX;` `public int gridY;`
- `CountryConfig` 加 `public string mapColor;`
- 运行时：`ProvinceState` 加 `gridX`/`gridY`（从 config 映射，WorldInitializer 填）；国家配色不必入运行时 State（ReadModel 直接从 config 或国家映射取——见 3.3）。
- `WorldInitializer.CreateNewGame`：映射 `gridX`/`gridY` 到 `ProvinceState`。

### 3.2 Contracts — ProvinceView + WorldView 扩展
```
public sealed class ProvinceView {
    public string id, name, ownerCountry, ownerColor;   // ownerColor = 该国 mapColor
    public string terrain;
    public int gridX, gridY;
    public int infrastructure, population, victoryPoint;
    public bool isCapital;
    public string[] resourceOutput;
}
```
- `WorldView` 加 `List<ProvinceView> provinces;` 和 `string selectedProvinceId;`

### 3.3 Application
- `ReadModelBuilder`：构建 `provinces`（按 id 升序），`ownerColor` = 对应 `CountryConfig.mapColor`（经 `IConfigRegistry` 查；为此 `BuildWorldView` 需要能访问 config——可传入 `IConfigRegistry` 或预先构建 countryId→color 字典）。`selectedProvinceId` 由 `GameSessionService` 填。
- `GameSessionService`：加 `_selectedProvinceId`、`void SelectProvince(string id)`（校验省存在）、`GetWorldView()` 填 `provinces` + `selectedProvinceId`。

### 3.4 Presentation — 地图视图 + 详情栏
- `MainHud.uxml`：在内政栏下方加 `VisualElement#map-area`（地图容器）+ `VisualElement#province-detail`（选中省详情栏）。国家列表可保留在底部或移入可滚动侧区（布局自定，保证不重叠）。
- `MainHudController.Render()`：
  - 渲染地图：清空 `map-area`，为每个 `ProvinceView` 创建一个方块 `VisualElement`：`style.position = Absolute`、`left = gridX * cellSize`、`top = gridY * cellSize`（`cellSize` 如 110px，方块 100×100），`backgroundColor` = 解析 `ownerColor`，内含省名 Label（深/浅自适应或统一白字加描边）。选中省加高亮边框（`borderWidth`+亮色）。
  - 方块 `RegisterCallback<ClickEvent>(_ => SelectProvince(id))`（用字段存回调或局部变量捕获 id，注意闭包）。
  - 渲染详情栏：若 `selectedProvinceId` 非空，`province-detail` 显示该省 name/owner/terrain/基建/人口/资源产出/胜利点；否则提示"点击地图省份查看"。
- `MainHudController` 加 `public void SelectProvince(string id)`（调 `_session.SelectProvince(id)` + `Render()`，沿用可测模式）。
- 修 B1.5 遗留：`Unbind()` 的 `UnregisterCallback` 仍可能用新 lambda（若 B1.5 已修则忽略）。

### 3.5 USS
- `.map-area`：相对定位容器，`height` 够放 3 行（如 340px），`margin` 适当。
- `.province-tile`：方块样式（圆角/边框/居中文字/白字）。`.province-tile-selected`：高亮边框。
- `.province-detail`：详情栏样式（浅色文字，沿用 B1.5 修复的可读配色）。

## 4. 测试（规则 6）
- **EditMode `ReadModelBuilder`**：`BuildWorldView` 的 `provinces.Count==6`、按 id 升序、`ownerColor` 正确对应国家、`gridX/gridY` 来自配置。
- **EditMode `GameSessionService`**：`SelectProvince("iron_city")` 后 `GetWorldView().selectedProvinceId=="iron_city"`；选不存在的省被忽略。
- **配置校验**：每省 `gridX/gridY` 在 [0,2]；每国 `mapColor` 非空且 `ColorUtility.TryParseHtmlString` 可解析（此断言放 Presentation/PlayMode 或 EditMode 用正则校验 `^#[0-9A-Fa-f]{6}$`）。
- **PlayMode 冒烟扩展**：地图区子元素（省份方块）数 == 6；模拟 `Controller.SelectProvince("iron_city")` → 详情栏文本非空/含"铁都"。保留渲染前提断言。

## 5. 文件清单
| 动作 | 路径 |
|---|---|
| 改（填数据） | `countries.json`(+mapColor×6)、`provinces.json`(+gridX/gridY×6) |
| 改 | `Domain/Config/{ProvinceConfig,CountryConfig}.cs`、`Domain/State/Province.cs`(+grid)、`Application/Setup/WorldInitializer.cs` |
| 新增 | `Contracts/ReadModels/ProvinceView.cs` |
| 改 | `Contracts/ReadModels/WorldView.cs`(+provinces,+selected)、`Application/Queries/ReadModelBuilder.cs`、`Application/Session/GameSessionService.cs` |
| 改 | `Presentation/MainHudController.cs` + `MainHud.uxml` + `MainHud.uss`（地图区+详情栏） |
| 改/增 | 上述测试 + `ConfigValidationTests`(grid/mapColor) |

## 6. 验收门禁（DoD）
- [ ] Phase 0：独立分支、起点全绿、UTF-8。
- [ ] Play `Main`：中部出现 **6 个彩色省份方块**（按方位摆、按国配色）；**点击某省→该省高亮 + 详情栏显示其信息**（**截图为证**）。
- [ ] 地图视图只依赖 `Application`+`Contracts`（规则 4 未破，Presentation 仍不引用 Domain/Simulation）。
- [ ] 省位置/配色全取自配置（零硬编码，规则 5）；未改既有经济/政治数值公式。
- [ ] 新增 EditMode + PlayMode 测试全绿；配置校验含 grid/mapColor；batchmode 0 error；导出**本次新** `artifacts/*.xml`。
- [ ] PR 在 `feature/b2-map-view`；changelog 写 PR 描述。

## 7. 歧义处理
地图布局/方块尺寸/详情栏位置/国家列表去留等视觉取舍 → 选"点省份能选中看详情"的最小清晰实现，**以 Play 截图为准**供人类验收；涉及数据/玩法 → `[需 Claude 决策]`/`[需人类定值]`。

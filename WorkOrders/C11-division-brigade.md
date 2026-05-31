# C11 — 师级地基：旅组合 + 师模板数据驱动

## 背景
当前 `UnitState` = 一支单兵种部队（infantry 一支兵），HoI 风格"师 = 多旅组合"概念未建立。C11 升级**数据结构**（不动战斗逻辑、不动 float 公式）——为 C12 师级团战 + 多兵种克制铺底。

**关键认知**：`UnitConfig` (infantry/artillery/light_tank/...) 当前理解为"兵种"，C11 后**重新理解为"旅模板"**——10 种旅类型早已在 units.json 定义。师 = 多旅组合。

## 范围

### 核心数据结构升级
| 升级前 | 升级后 |
|---|---|
| 1 `UnitState` = 1 兵种实例 | 1 `UnitState` = 1 师 = `List<BrigadeState>` |
| `UnitState.baseAttack/baseDefense/...` 静态 = template 值 | `UnitState.baseAttack/...` 改由 `RecalculateFromBrigades()` 计算 = sum of brigades |
| `UnitConfig` = 兵种模板 | **保留**，重新理解为"旅模板"（infantry/artillery/...）|
| 训练命令 `cmd.unitType="infantry"` | `cmd.divisionTemplateId="infantry_division_basic"`（引用师模板）|
| 初始部队 = 每国 1 infantry | 每国 1 "infantry_division_basic" 师 = 9 infantry 旅 + 3 artillery 旅 + 1 engineer 旅 |

### 新数据驱动文件
`StreamingAssets/Configs/Json/divisionTemplates.json`:
```json
{
  "schemaVersion": 1,
  "items": [
    {
      "id": "infantry_division_basic",
      "name": "基础步兵师",
      "brigades": [
        { "brigadeType": "infantry",  "count": 9 },
        { "brigadeType": "artillery", "count": 3 }
      ],
      "trainingTurns": 2,
      "trainingCost": { "steel": 30, "food": 60, "capital": 15 },
      "trainingManpowerCost": 1200,
      "trainingEquipmentCost": 300
    },
    {
      "id": "armor_division",
      "name": "装甲师",
      "brigades": [
        { "brigadeType": "light_tank", "count": 6 },
        { "brigadeType": "mech_inf",   "count": 3 }
      ],
      "trainingTurns": 4,
      "trainingCost": { "steel": 120, "oil": 50, "rareMetal": 20, "capital": 50 },
      "trainingManpowerCost": 900,
      "trainingEquipmentCost": 800
    },
    {
      "id": "cavalry_division",
      "name": "骑兵师",
      "brigades": [
        { "brigadeType": "cavalry", "count": 6 }
      ],
      "trainingTurns": 2,
      "trainingCost": { "food": 90, "capital": 18 },
      "trainingManpowerCost": 600,
      "trainingEquipmentCost": 150
    }
  ]
}
```

C11 只开放 3 个模板（步兵师/装甲师/骑兵师），其他兵种 C12+ 解锁。

### 师属性合成公式
```csharp
RecalculateFromBrigades():
  baseAttack    = sum(brigade.config.attack    × brigade.count) for brigade in brigades
  baseDefense   = sum(brigade.config.defense   × brigade.count)
  baseBreakthrough = sum(brigade.config.breakthrough × brigade.count)
  armor         = max(brigade.config.armor)    // 取最强装甲
  piercing      = max(brigade.config.piercing) // 取最强穿透
  speed         = min(brigade.config.speed)    // 师速 = 最慢旅
  maxManpower   = sum(brigade.config.hp × brigade.count)
  maxEquipment  = sum(brigade.config.hp × brigade.count)  // 同 manpower
  maxOrganization = avg(brigade.config.organization)
  supplyConsumption = sum(brigade.config.supplyConsumption × brigade.count)
```

师战损（C12 才用）：某旅 manpower=0 → 从 brigades 列表移除 + RecalculateFromBrigades。**C11 不实现旅级战损**——保持现有 `unit.TakeDamage` 师整体扣血。

## 文件变更清单

### Domain
- 新建 `Domain/State/BrigadeState.cs`：
  ```csharp
  public sealed class BrigadeState {
      public string brigadeType;  // = UnitConfig.id (infantry/artillery/...)
      public int count;           // 旅数量
      public int manpower;        // 当前总人力 = count × config.hp 初始
      public int equipment;       // 当前总装备 = count × config.hp 初始
  }
  ```
- `Domain/Unit.cs` (`UnitState`)：
  - 加 `List<BrigadeState> brigades = new();`
  - 加 `string divisionTemplateId;`（来自 DivisionTemplate.id）
  - 加方法 `RecalculateFromBrigades(IConfigRegistry config)` —— 按 §师属性合成公式 重算 base 字段
  - 既有 baseAttack/baseDefense/.../maxManpower/.../speed 字段**保留**（由 RecalculateFromBrigades 写入），其他 Resolver 沿用读这些字段不动
- 新建 `Domain/Config/DivisionTemplate.cs`：
  ```csharp
  public sealed class DivisionTemplate {
      public string id;
      public string name;
      public BrigadeEntry[] brigades;
      public int trainingTurns;
      public Dictionary<string, int> trainingCost;
      public int trainingManpowerCost;
      public int trainingEquipmentCost;
  }
  public sealed class BrigadeEntry {
      public string brigadeType;
      public int count;
  }
  ```

### Application
- `UnitFactory.cs` —— 改 `CreateFromTemplate` 签名：
  ```csharp
  // 旧
  CreateFromTemplate(string id, string unitType, string owner, string provinceId, UnitConfig template)
  // 新
  CreateFromDivisionTemplate(string id, DivisionTemplate template, string owner, string provinceId, IConfigRegistry config)
  ```
  内部：
  1. 创建 UnitState 实例（divisionTemplateId = template.id）
  2. 遍历 template.brigades → 创建 BrigadeState（manpower=count×brigadeConfig.hp、equipment 同）
  3. unit.brigades.Add 全部
  4. 调 unit.RecalculateFromBrigades(config) 计算师属性
- `WorldInitializer.cs` —— 每国初始部队改为 `CreateFromDivisionTemplate("infantry_division_basic", ...)`（替代原 `CreateFromTemplate("infantry", ...)`）
- `UnitProductionResolver.cs`：
  - AllowedTypes 改为 DivisionTemplate id（"infantry_division_basic" / "armor_division" / "cavalry_division"）
  - TryEnqueue 参数 `string unitType` → `string divisionTemplateId`
  - 查 DivisionTemplate（不是 UnitConfig）取 cost / manpower / equipment 消耗
  - ResolveProduction 完工时调 `CreateFromDivisionTemplate`
- `SaveModels.cs` — `UnitSaveData` 加 `divisionTemplateId` + `BrigadeSaveData[] brigades`；新建 `BrigadeSaveData { brigadeType, count, manpower, equipment }`
- `SaveMapper.cs` — 双向 brigades + 读档后调 RecalculateFromBrigades；HashWorld 扩 brigades（按 brigadeType 升序写）

### Simulation
- 无变（BattleResolver / MovementResolver / OccupationResolver / ... 仍读 UnitState.baseAttack 等字段，由 RecalculateFromBrigades 写好）
- **C12 才改 BattleResolver 旅级战损 + float→int**

### Contracts
- `GameCommand.cs` — `unitType` 字段语义改为"divisionTemplateId"（不重命名字段，避免存档兼容性破坏）
- `UnitView.cs` — 加 `divisionTemplateName`（显示用，从 DivisionTemplate.name 拿）+ `brigadeSummary`（如 "9 步兵 + 3 炮兵"）
- 既有事件（UnitProducedEvent / UnitDestroyedEvent）保留 unitId/unitType 字段，**unitType 改为存 divisionTemplateId**

### Bootstrap
- `GameLifetimeScope.cs` — IConfigRepository 加载 DivisionTemplate（与 UnitConfig 并列）
- `ConfigRegistry.LoadAll` 自动注册（数据驱动模式）

### Data
- 新建 `Assets/StreamingAssets/Configs/Json/divisionTemplates.json` —— 3 个模板（见 §核心数据驱动文件）
- `economy.json` 无变

### Presentation
- `MainHudController.cs`:
  - "训练步兵" 按钮文案改为"训练步兵师"
  - 玩家若开放多个 division 模板，按钮组里 3 选 1（C11 起步：infantry_division_basic 默认选中，UI 只显示 1 个按钮）
  - 详情栏 / 国家行可选显示 `{N 师}` 替代 `{N 支}`
- `MainHud.uxml` — 训练按钮 name 改 `train-infantry-division-btn`（兼容旧 `train-infantry-btn` 也可保留）

### Tests
- 新建 `BrigadeStateTests.cs`：
  - `RecalculateFromBrigades_SingleInfantryBrigade_MatchesConfig`
  - `RecalculateFromBrigades_MultipleBrigades_SumsCorrectly`
  - `RecalculateFromBrigades_SpeedTakesMin`
  - `RecalculateFromBrigades_ArmorTakesMax`
- 新建 `DivisionTemplateLoadTests.cs`：
  - `LoadDivisionTemplates_ThreeBuiltIn_AllParse`
  - `DivisionTemplate_InfantryBasic_HasCorrectBrigadeMix`
- `UnitFactoryTests.cs` 新增：
  - `CreateFromDivisionTemplate_PopulatesBrigadesAndRecalculates`
- `UnitProductionResolverTests.cs` 全部更新：
  - 旧 `unitType="infantry"` → `divisionTemplateId="infantry_division_basic"`
  - 新增 `TryEnqueue_UnknownDivisionTemplate_Rejects`
  - 新增 `TryEnqueue_ArmorDivision_Costs120Steel`（多兵种成本验证）
- `SaveLoadEquivalenceTests.cs` 追加：
  - `SaveLoad_DivisionWithBrigades_Preserved` + HashWorld 扩 brigades
- `ConfigValidationTests.cs` 追加：
  - `DivisionTemplates_AllReferencedBrigadeTypesExistInUnits`（divisionTemplates.json 引用的 brigadeType 必须在 units.json 存在）

## DoD Check List
- [ ] BrigadeState + DivisionTemplate + brigadeEntry 三个 Domain 类齐全
- [ ] UnitState 加 brigades + divisionTemplateId + RecalculateFromBrigades 方法
- [ ] WorldInitializer 改用 CreateFromDivisionTemplate
- [ ] UnitProductionResolver 改用 divisionTemplateId、3 模板都能造
- [ ] divisionTemplates.json 3 模板齐全 + ConfigRegistry 自动加载
- [ ] SaveMapper 双向 brigades + ToRuntime 末尾对每个 unit 调 RecalculateFromBrigades
- [ ] HashWorld 扩 brigades 字段（按 brigadeType 升序）
- [ ] **BattleResolver / MovementResolver / OccupationResolver / VictoryConditionResolver / AIResolver / ... 任何 Simulation Resolver 0 改动**（C12 才动）
- [ ] EconomyResolver / PoliticsResolver / WarTollResolver / PeaceResolver / AiPeaceOfferResolver / AiRedeploymentResolver / ConstructionResolver 0 改动
- [ ] 既有 248 测试 + 本单新增（约 12 个）全绿
- [ ] artifacts/c11-editmode.xml + c11-playmode.xml 归档
- [ ] **Design/screenshots/c11-division-trained.png**（训练步兵师后看到师存在地图、HUD 显示"1 师"）+ `c11-armor-vs-infantry-cost.png`（点装甲师按钮看到 cost 120 steel/50 oil 远高于步兵师 30/60/15）—— **C11 强制截图，10 单 0 截图老毛病本单杜绝**
- [ ] Unity Console 0 error
- [ ] batchmode 0 error 且 0 failed
- [ ] PROJECT_RULES.md / ARCHITECTURE.md / PROJECT_STATE.md / CHANGELOG.md / .gitignore 0 改动
- [ ] ProjectSettings/ + Packages/ 0 改动
- [ ] PR 描述含 `## DoD Check List` 逐项打勾

## 不做（C12+）
- 旅级战损（某旅 manpower=0 → 移除 + 师属性重算）—— **C12 做**，本单师整体扣血保持
- BattleResolver float→int 重构 —— **C12 做**
- 兵种克制（步>炮>坦>步循环）—— **C13 做**
- 自定义师编制 UI（玩家拖拽旅组成自己的师）—— **C20+ 做**
- 装甲师 / 骑兵师真用 oil/rareMetal 资源链（C11 仅训练消耗，移动/战斗时不再扣）—— C13 维护费做
- C9c 截图未补的"多攻方加入同省"+"停战和平期"验证——C11 工作单不接管，由你 Play 时一并验证

## 严禁
- 改 BattleResolver / MovementResolver / OccupationResolver 既有 ResolveBattle / TickBattles / InitiateAttack / TryMove / ApplyTurnResistance 公式
- 改 EconomyResolver treasury→capital / militaryFactoryEquipmentOutput / 任何既有公式
- 加自定义师编制 UI
- 删 UnitConfig（仍作旅模板用）
- 删 UnitState 既有字段（baseAttack 等仍存，仅改写时机）
- 跳过指派测试 / 用 commit log 代替截图

## 歧义处理
- **既有存档与新存档不兼容**：本单首次破坏存档兼容性（UnitSaveData 新增 brigades 字段）。SaveMapper.ToRuntime 检测 brigades==null（旧存档）时**自动 fallback**：根据 unit.unitType（C2a 起一直 = "infantry"）创建单 brigade BrigadeState(brigadeType="infantry", count=1)，让旧存档可读但师属性退化为原 1 兵种。**写死**：旧存档读为"infantry_division_legacy"（不在 divisionTemplates.json 但 ToRuntime 后能算属性）。
- **BrigadeState.count 是否随战损减少**：本单**不做**（C12 旅级战损）。count 在创建后不变，战损改 brigade.manpower 而非 count。
- **DivisionTemplate.trainingCost 与 UnitConfig.cost 关系**：DivisionTemplate.trainingCost 是新概念（师级训练成本），UnitConfig.cost（旅级成本）保留不用（旅不能单独训练，只能作为师的一部分）。**写死**：UnitProductionResolver 只看 DivisionTemplate.trainingCost。
- **divisionTemplateId 命名冲突**：与 UnitConfig.id 是否重名？建议 DivisionTemplate.id 用后缀 "_division_basic" 区分。
- **多个旅同 brigadeType**：DivisionTemplate.brigades 允许 `{"infantry", 9}` 但**不允许两条 `{"infantry", 3}` + `{"infantry", 6}``（重复键）。校验测试 `DivisionTemplates_NoDuplicateBrigadeTypes`。
- **C11 师属性合成是否考虑兵种克制**：本单**不做**（C13 做）。armor=max / piercing=max 仅简单提取最强值，无兵种克制矩阵。

## 完工后人类 Play 验证清单
1. 选玩家国 → 详情栏看到初始部队 = "基础步兵师"（9 步兵 + 3 炮兵 brigade）+ 师属性 = sum
2. 训练按钮文案 = "训练步兵师" / cost 显示
3. C12 之前**装甲师/骑兵师按钮暂不开放**——本单只 1 个训练按钮（步兵师）即可，3 模板都能训通过命令但 UI 只暴露 1 个
4. 推 N 回合训成 → 师入驻首都 → 师属性 = 9 步兵旅 + 3 炮兵旅 sum
5. 战斗仍按现状（C12 才改师级团战）
6. 存档 → 读 → 师 brigades 完整持久

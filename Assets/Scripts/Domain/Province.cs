// ============================================================================
// Domain/Province.cs — 省份数据模型
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>省份运行时状态</summary>
    public class ProvinceState
    {
        // === 基础 ===
        public string id;
        public string name;
        public TerrainType terrain;

        // === 归属 ===
        public string ownerCountry;      // 主权国
        public string controllerCountry; // 控制者（可能被占领）

        // === 人口 ===
        public int population;
        public int manpower;             // 可征召人力

        // === 基础设施 ===
        public int infrastructure;       // 基础设施等级 0-5
        public int railwayLevel;         // 铁路等级 0-3
        public int portLevel;            // 港口等级 0-3
        public int airBaseLevel;         // 空军基地等级 0-3

        // === 工业 ===
        public int industrySlots;        // 工厂槽位
        public int builtCivilianFactories;
        public int builtMilitaryFactories;

        // === 资源 ===
        public string[] resourceOutput;  // 该省产出的资源类型

        // === 补给 ===
        public int supplyCapacity;       // 补给容量

        // === 占领相关 ===
        public int resistance;           // 抵抗度 0-100
        public int compliance;           // 服从度 0-100

        // === 战略价值 ===
        public int victoryPoint;         // 胜利点数
        public bool isCapital;

        // === 方法 ===

        /// <summary>计算补给容量</summary>
        public int CalculateSupplyCapacity()
        {
            int baseSupply = infrastructure * 10;
            int railwayBonus = railwayLevel * 15;
            int portBonus = portLevel * 10;
            return baseSupply + railwayBonus + portBonus;
        }

        /// <summary>是否为沿海省份</summary>
        public bool IsCoastal => portLevel > 0;

        /// <summary>是否被敌方占领</summary>
        public bool IsOccupied => ownerCountry != controllerCountry;
    }

    /// <summary>地形类型</summary>
    public enum TerrainType
    {
        Plain,     // 平原 — 无修正
        Forest,    // 森林 — 防御+10%, 移动+1消耗
        Mountain,  // 山地 — 防御+25%, 移动+2消耗
        Hills,     // 丘陵 — 防御+15%, 移动+1消耗
        Desert,    // 沙漠 — 补给-30%, 移动+1消耗
        Swamp,     // 沼泽 — 防御+20%, 移动+2消耗, 补给-20%
        Urban,     // 城市 — 防御+30%, 补给+20%
        Jungle,    // 丛林 — 防御+15%, 移动+2消耗, 补给-15%
        Coastline, // 海岸 — 两栖修正
        River      // 河流 — 防御+20%, 渡河惩罚
    }
}

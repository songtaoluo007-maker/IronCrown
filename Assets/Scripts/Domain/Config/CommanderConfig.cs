// ============================================================================
// Domain/Config/CommanderConfig.cs — 将领配置（C15a 扩展）
// EU4 风格将军卡 + 5 阶军衔
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>将领配置（静态数据，从 JSON 加载）</summary>
    [System.Serializable]
    public class CommanderConfig
    {
        public string id;
        public string name;
        public string title;            // 头衔（如 "铁壁将军"）
        public int baseAttack;          // 基础攻击加成
        public int baseDefense;         // 基础防御加成
        public string[] traits;         // 特性标签

        // === C15a: 招募成本 ===
        public int recruitCapitalCost;  // capital 消耗（默认 100）
        public int recruitManpowerCost; // manpower 消耗（默认 500）

        // === C15a: 初始指挥容量 ===
        public int baseMaxDivisions;    // 基础指挥师数（默认 5）

        // === 兼容旧字段 ===
        public int logisticsBonus;
        public int moraleBonus;
    }
}

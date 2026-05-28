// ============================================================================
// Domain/Config/CommanderConfig.cs — 将领配置
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>将领配置</summary>
    [System.Serializable]
    public class CommanderConfig
    {
        public string id;
        public string name;
        public string title;
        public int attackBonus;
        public int defenseBonus;
        public int logisticsBonus;
        public int moraleBonus;
        public string[] traits;          // 特性标签
    }
}

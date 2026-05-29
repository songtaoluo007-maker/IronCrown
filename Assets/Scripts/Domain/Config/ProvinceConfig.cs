// ============================================================================
// Domain/Config/ProvinceConfig.cs — 省份配置
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>省份配置（静态数据，从 JSON 加载）</summary>
    [System.Serializable]
    public class ProvinceConfig
    {
        public string id;
        public string name;
        public string terrain;
        public string ownerCountry;
        public bool isCapital;
        public int population;
        public int manpower;
        public int infrastructure;
        public int railwayLevel;
        public int portLevel;
        public int airBaseLevel;
        public int industrySlots;
        public string[] resourceOutput;
        public int victoryPoint;
        public int gridX;
        public int gridY;
    }
}

// ============================================================================
// Domain/Config/EconomyConfig.cs — 经济配置 DTO
// 数据来源: StreamingAssets/Configs/Json/economy.json
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>经济全局配置（单例行 id="global"）</summary>
    [System.Serializable]
    public class EconomyConfig
    {
        public string id;
        public int provinceBaseOutputPerResource;
        public int provinceInfraOutputBonus;
        public int militaryFactoryEquipmentOutput;
        public int equipmentSteelCost;
        public int equipmentCapitalCost;
        public int civilianFactoryUpkeep;
        public int militaryFactoryUpkeep;
        public int dockyardUpkeep;
    }
}

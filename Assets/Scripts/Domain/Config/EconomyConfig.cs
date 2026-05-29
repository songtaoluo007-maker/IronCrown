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

        // === 建造成本 ===
        public int civilianFactoryBuildCost;
        public int militaryFactoryBuildCost;
        public int factoryBuildTurns;

        // === 内政档位 ===
        public int[] taxRatePercents;       // [70,100,130] 税收倍率
        public int[] taxStabilityDeltas;    // [1,0,-2] 每回合稳定修正
        public int[] civilExpensePercents;  // [50,100,150] 民生支出倍率
        public int[] civilStabilityDeltas;  // [-2,0,2] 每回合稳定修正
    }
}

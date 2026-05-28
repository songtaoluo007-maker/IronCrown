// ============================================================================
// Domain/Config/TechConfig.cs — 科技配置
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>科技配置</summary>
    [System.Serializable]
    public class TechConfig
    {
        public string id;
        public string name;
        public string description;
        public string category;          // industrial/land/naval/air/political
        public int researchCost;
        public string[] prerequisites;   // 前置科技
        public TechEffect effects;
    }

    /// <summary>科技效果</summary>
    [System.Serializable]
    public class TechEffect
    {
        public float factoryOutputBonus;
        public float researchSpeedBonus;
        public float attackBonus;
        public float defenseBonus;
        public float supplyBonus;
        public float manpowerBonus;
        public int extraCivilianFactories;
        public int extraMilitaryFactories;
    }
}

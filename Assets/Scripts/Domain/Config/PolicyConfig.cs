// ============================================================================
// Domain/Config/PolicyConfig.cs — 政策配置
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    /// <summary>政策配置</summary>
    [System.Serializable]
    public class PolicyConfig
    {
        public string id;
        public string name;
        public string description;
        public string category;          // economic/military/political/social
        public Dictionary<string, int> requirements;
        public PolicyEffect effects;
    }

    /// <summary>政策效果</summary>
    [System.Serializable]
    public class PolicyEffect
    {
        public float militaryFactoryOutput;
        public float civilianFactoryOutput;
        public float consumerGoodsRatio;
        public int stabilityModifier;
        public int warSupportModifier;
        public float conscriptionBonus;
        public float tradeBonus;
        public int researchSpeedModifier;
    }
}

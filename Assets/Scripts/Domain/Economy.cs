// ============================================================================
// Domain/Economy.cs — 经济系统数据模型
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    /// <summary>资源配置定义</summary>
    [System.Serializable]
    public class ResourceConfig
    {
        public string id;
        public string name;
        public string description;
        public int basePrice;            // 基础贸易价格
    }

    /// <summary>工厂类型</summary>
    public enum FactoryType
    {
        Civilian,  // 民用 — 建设/贸易/修复
        Military,  // 军用 — 生产装备
        Dockyard   // 造船 — 生产舰船
    }

    /// <summary>生产线状态</summary>
    [System.Serializable]
    public class ProductionLineState
    {
        public string id;
        public string equipmentType;     // 生产的装备类型
        public FactoryType factoryType;
        public int allocatedFactories;   // 投入工厂数
        public int progress;             // 当前进度
        public int costPerUnit;          // 每单位消耗
        public int efficiency;           // 生产效率 0-100
        public int stockpile;            // 库存数量

        /// <summary>每回合产量</summary>
        public int OutputPerTurn => allocatedFactories * efficiency / 100;
    }

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

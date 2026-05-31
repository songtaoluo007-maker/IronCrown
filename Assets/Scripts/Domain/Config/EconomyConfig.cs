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
        public int provinceBaseFoodOutput;     // 每省每回合基础粮食产出
        public int provinceInfraOutputBonus;
        public int militaryFactoryEquipmentOutput;
        public int equipmentSteelCost;
        public int equipmentCapitalCost;
        public int civilianFactoryUpkeep;
        public int militaryFactoryUpkeep;
        public int dockyardUpkeep;

        // === 建造成本 ===
        public int civilianFactoryBuildCost;
        public int civilianFactoryCapitalOutput; // 每个民用厂每回合产出 capital
        public int militaryFactoryBuildCost;
        public int factoryBuildTurns;

        // === 内政档位 ===
        public int[] taxRatePercents;       // [70,100,130] 税收倍率
        public int[] taxStabilityDeltas;    // [1,0,-2] 每回合稳定修正
        public int[] civilExpensePercents;  // [50,100,150] 民生支出倍率
        public int[] civilStabilityDeltas;  // [-2,0,2] 每回合稳定修正

        // === 造兵 ===
        public int unitProductionTurns;

        // === AI 阈值 ===
        public int aiBuildCapitalThreshold;
        public int aiMaxCivilianFactories;
        public int aiMaxMilitaryFactories;

        // === AI 军事 ===
        public int aiAttackPowerRatio;      // 百分比，120 = 攻方需 120% 守方才进攻
        public int aiMaxAttacksPerTurn;     // 每国每回合最多进攻次数

        // === 战争代价 (C5) ===
        public int warStabilityPenaltyPerTurn;       // AtWar 每回合 stability -N
        public int warExhaustionPerTurn;             // AtWar 每回合 warExhaustion +N
        public int warSupportPenaltyPerLoss;         // 战败 warSupport -N
        public int warSupportBonusPerVictory;        // 战胜 warSupport +N
        public int warSupportPenaltyPerCapitalLoss;  // 首都被占 warSupport -N（一次性）
        public int aiPeaceAcceptExhaustionThreshold; // AI 进入"愿意停战"基础线
        public int aiPeaceAcceptPowerRatioPct;       // AI 实力 ≤ 对方 N% 时下调阈值

        // === AI 主动求和 (C7) ===
        public int aiPeaceOfferExhaustionThreshold;  // AI 主动求和 warExhaustion 阈值
        public int aiPeaceOfferPowerRatioPct;          // AI 国力 ≤ 玩家 N% 时触发
        public int aiPeaceOfferCooldownTurns;           // 被拒后冷却回合数
        public int aiPeaceOfferExpiryTurns;              // 提议过期回合数

        // === AI 调防 (C8) ===
        public int aiRedeployVulnerableRatioPct;        // 守军战力 ≤ 邻敌 N% 时触发调防
        public int aiMaxRedeploysPerTurn;                // 每国每回合最多调防次数

        // === 占领抵抗 (C6) ===
        public int resistanceOnCapture;               // 占领瞬间 resistance 值
        public int resistanceDecayWithGarrison;        // 有驻军时 resistance 每回合变化（负数=衰减）
        public int resistanceGrowWithoutGarrison;      // 无驻军时 resistance 每回合变化（正数=增长）
        public int resistanceUprisingThreshold;        // 触发反抗事件的 resistance 阈值
        public int resistanceUprisingChancePct;        // 反抗事件触发概率（百分比）
        public int resistanceGarrisonDamageManpower;   // 有驻军反抗时扣驻军 manpower
        public int resistanceGarrisonDamageEquipment;  // 有驻军反抗时扣驻军 equipment
    }
}

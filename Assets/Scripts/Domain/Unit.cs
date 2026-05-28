// ============================================================================
// Domain/Unit.cs — 军事单位运行时数据模型
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>部队运行时状态</summary>
    public class UnitState
    {
        // === 基础 ===
        public string id;
        public string unitType;          // 步兵/炮兵/坦克等
        public string ownerCountry;
        public string currentProvinceId;

        // === 人员与装备 ===
        public int manpower;             // 当前人力
        public int maxManpower;          // 最大人力
        public int equipment;            // 当前装备
        public int maxEquipment;         // 最大装备

        // === 战斗属性 ===
        public int organization;         // 组织度 0-maxOrg
        public int maxOrganization;
        public int morale;               // 士气 0-100
        public int experience;           // 经验等级 0-3

        // === 基础战斗值（受科技/将领/地形修正） ===
        public int baseAttack;
        public int baseDefense;
        public int baseBreakthrough;
        public int armor;
        public int piercing;

        // === 移动 ===
        public int speed;                // 每回合移动格数
        public int movesLeft;            // 本回合剩余移动力

        // === 补给 ===
        public int supplyConsumption;    // 每回合补给需求

        // === 指挥官 ===
        public string commanderId;

        // === 状态 ===
        public bool isEntrenched;        // 是否驻守加成
        public int entrenchmentBonus;

        // === 方法 ===

        /// <summary>组织度是否已溃散</summary>
        public bool IsShattered => organization <= 0;

        /// <summary>是否满编</summary>
        public bool IsFullStrength => manpower >= maxManpower && equipment >= maxEquipment;

        /// <summary>补员</summary>
        public void Reinforce(int manpowerAmount, int equipmentAmount)
        {
            manpower = System.Math.Min(manpower + manpowerAmount, maxManpower);
            equipment = System.Math.Min(equipment + equipmentAmount, maxEquipment);
        }

        /// <summary>受伤害</summary>
        public void TakeDamage(int orgDamage, int strDamage)
        {
            organization = System.Math.Max(0, organization - orgDamage);
            manpower = System.Math.Max(0, manpower - strDamage);
            equipment = System.Math.Max(0, equipment - strDamage);
        }

        /// <summary>恢复组织度</summary>
        public void RecoverOrganization(int amount)
        {
            organization = System.Math.Min(organization + amount, maxOrganization);
        }
    }
}

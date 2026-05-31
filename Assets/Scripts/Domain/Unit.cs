// ============================================================================
// Domain/Unit.cs — 军事单位运行时数据模型
// C11: 师 = 多旅组合
// ============================================================================

using System.Collections.Generic;
using System.Linq;

namespace IronCrown.Domain
{
    /// <summary>部队运行时状态（C11: 1 UnitState = 1 师 = 多旅组合）</summary>
    public class UnitState
    {
        // === 基础 ===
        public string id;
        public string unitType;          // 保留兼容，C11 后存 divisionTemplateId
        public string divisionTemplateId; // C11: 师模板 ID
        public string ownerCountry;
        public string currentProvinceId;

        // === C11: 旅组成 ===
        public List<BrigadeState> brigades = new();

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

        // === C11: 师属性合成 ===

        /// <summary>从旅组成重算师属性（创建时调用，C12 旅级战损后也调用）</summary>
        public void RecalculateFromBrigades(IConfigRegistry config)
        {
            if (brigades == null || brigades.Count == 0) return;

            int totalAttack = 0, totalDefense = 0, totalBreakthrough = 0;
            int maxArmor = 0, maxPiercing = 0;
            int minSpeed = int.MaxValue;
            int totalMaxManpower = 0, totalMaxEquipment = 0;
            int totalOrg = 0, orgCount = 0;
            int totalSupply = 0;

            foreach (var b in brigades)
            {
                var cfg = config.Get<UnitConfig>(b.brigadeType);
                if (cfg == null) continue;

                totalAttack += cfg.attack * b.count;
                totalDefense += cfg.defense * b.count;
                totalBreakthrough += cfg.breakthrough * b.count;
                if (cfg.armor > maxArmor) maxArmor = cfg.armor;
                if (cfg.piercing > maxPiercing) maxPiercing = cfg.piercing;
                if (cfg.speed < minSpeed) minSpeed = cfg.speed;
                totalMaxManpower += cfg.hp * b.count;
                totalMaxEquipment += cfg.hp * b.count;
                totalOrg += cfg.organization;
                orgCount++;
                totalSupply += cfg.supplyConsumption * b.count;
            }

            baseAttack = totalAttack;
            baseDefense = totalDefense;
            baseBreakthrough = totalBreakthrough;
            armor = maxArmor;
            piercing = maxPiercing;
            speed = minSpeed == int.MaxValue ? 1 : minSpeed;
            maxManpower = totalMaxManpower;
            maxEquipment = totalMaxEquipment;
            maxOrganization = orgCount > 0 ? totalOrg / orgCount : 60;
            supplyConsumption = totalSupply;
        }
    }
}

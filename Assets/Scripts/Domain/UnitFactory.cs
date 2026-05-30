// ============================================================================
// Domain/UnitFactory.cs — 部队创建工厂（共享逻辑）
// 从 WorldInitializer 初始部队创建块抽取，供 WorldInitializer + UnitProductionResolver 共用
// ============================================================================

namespace IronCrown.Domain
{
    public static class UnitFactory
    {
        /// <summary>从配置模板创建满编部队</summary>
        public static UnitState CreateFromTemplate(string id, string unitType, string ownerCountry, string provinceId, UnitConfig template)
        {
            return new UnitState
            {
                id = id,
                unitType = unitType,
                ownerCountry = ownerCountry,
                currentProvinceId = provinceId,
                manpower = template.hp,
                maxManpower = template.hp,
                equipment = template.hp,
                maxEquipment = template.hp,
                organization = template.organization,
                maxOrganization = template.organization,
                morale = 50,
                experience = 0,
                baseAttack = template.attack,
                baseDefense = template.defense,
                baseBreakthrough = template.breakthrough,
                armor = template.armor,
                piercing = template.piercing,
                speed = template.speed,
                movesLeft = template.speed,
                supplyConsumption = template.supplyConsumption
            };
        }
    }
}

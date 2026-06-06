// ============================================================================
// Domain/UnitFactory.cs — 部队创建工厂（共享逻辑）
// C11: 新增 CreateFromDivisionTemplate（师 = 多旅组合）
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    public static class UnitFactory
    {
        /// <summary>从配置模板创建满编部队（旧接口，保留兼容）</summary>
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

        /// <summary>C11: 从师模板创建师（多旅组合）</summary>
        public static UnitState CreateFromDivisionTemplate(
            string id, DivisionTemplate divTemplate, string ownerCountry, string provinceId, IConfigRegistry config)
        {
            var unit = new UnitState
            {
                id = id,
                unitType = divTemplate.id,
                divisionTemplateId = divTemplate.id,
                ownerCountry = ownerCountry,
                currentProvinceId = provinceId,
                morale = 50,
                experience = 0
            };

            // 创建旅组成
            foreach (var entry in divTemplate.brigades)
            {
                var brigadeCfg = config.Get<UnitConfig>(entry.brigadeType);
                if (brigadeCfg == null) continue;

                unit.brigades.Add(new BrigadeState
                {
                    brigadeType = entry.brigadeType,
                    count = entry.count,
                    manpower = brigadeCfg.hp * entry.count,
                    equipment = brigadeCfg.hp * entry.count
                });
            }

            // 从旅组成重算师属性
            unit.RecalculateFromBrigades(config);
            unit.manpower = unit.maxManpower;
            unit.equipment = unit.maxEquipment;
            unit.organization = unit.maxOrganization;
            unit.movesLeft = unit.speed;

            return unit;
        }
    }
}

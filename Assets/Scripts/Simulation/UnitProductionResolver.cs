// ============================================================================
// Simulation/UnitProductionResolver.cs — 造兵结算器
// C11: 改用 DivisionTemplate（师级训练）
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class UnitProductionResolver
    {
        /// <summary>尝试入队训练（C11: 用 divisionTemplateId，兼容旧 UnitConfig）</summary>
        public CommandResult TryEnqueue(CountryState c, string divisionTemplateId, IConfigRegistry config, EconomyConfig eco)
        {
            // C11: 查师模板
            var divTemplate = config.Get<DivisionTemplate>(divisionTemplateId);

            // Fallback: 旧 UnitConfig 模式（无 DivisionTemplate 时）
            UnitConfig unitConfig = null;
            if (divTemplate == null)
            {
                unitConfig = config.Get<UnitConfig>(divisionTemplateId);
                if (unitConfig == null)
                    return CommandResult.Reject($"未找到师模板: {divisionTemplateId}");

                // 用 UnitConfig 构造等效校验
                if (unitConfig.cost != null && !c.HasResources(unitConfig.cost))
                    return CommandResult.Reject("资源不足");
                if (unitConfig.equipmentTrainingCost > 0 && c.equipmentStockpile < unitConfig.equipmentTrainingCost)
                    return CommandResult.Reject("装备库存不足");

                if (unitConfig.cost != null)
                    c.ConsumeResources(unitConfig.cost);
                if (unitConfig.equipmentTrainingCost > 0)
                    c.equipmentStockpile -= unitConfig.equipmentTrainingCost;

                c.unitProductionQueue.Add(new UnitProductionOrder
                {
                    unitType = divisionTemplateId,
                    turnsRemaining = eco.unitProductionTurns
                });
                return CommandResult.Accept();
            }

            // 资源校验
            if (divTemplate.trainingCost != null)
            {
                if (!c.HasResources(divTemplate.trainingCost))
                    return CommandResult.Reject("资源不足");
            }

            // 人力校验
            if (c.manpower < divTemplate.trainingManpowerCost)
                return CommandResult.Reject("人力不足");

            // C10: 装备库存校验
            if (divTemplate.trainingEquipmentCost > 0 && c.equipmentStockpile < divTemplate.trainingEquipmentCost)
                return CommandResult.Reject("装备库存不足");

            // 全部校验通过后才扣减
            if (divTemplate.trainingCost != null)
                c.ConsumeResources(divTemplate.trainingCost);
            c.manpower -= divTemplate.trainingManpowerCost;
            if (divTemplate.trainingEquipmentCost > 0)
                c.equipmentStockpile -= divTemplate.trainingEquipmentCost;

            c.unitProductionQueue.Add(new UnitProductionOrder
            {
                unitType = divisionTemplateId,  // 复用字段存 divisionTemplateId
                turnsRemaining = divTemplate.trainingTurns
            });
            return CommandResult.Accept();
        }

        /// <summary>结算阶段：推进建造进度，完工则生成师</summary>
        public List<UnitState> ResolveProduction(WorldState world, IConfigRegistry config)
        {
            var produced = new List<UnitState>();
            foreach (var country in world.countries.Values.OrderBy(c => c.id, System.StringComparer.Ordinal))
            {
                var completed = new List<UnitProductionOrder>();
                foreach (var order in country.unitProductionQueue)
                {
                    order.turnsRemaining--;
                    if (order.turnsRemaining <= 0)
                    {
                        var divTemplate = config.Get<DivisionTemplate>(order.unitType);

                        if (divTemplate != null)
                        {
                            // C11: 师级创建
                            int seq = world.units.Values.Count(u => u.ownerCountry == country.id) + 1;
                            string unitId = $"{country.id}_div_{seq}";
                            var unit = UnitFactory.CreateFromDivisionTemplate(unitId, divTemplate, country.id, country.capitalProvinceId, config);
                            world.units[unitId] = unit;
                            country.unitIds.Add(unitId);
                            produced.Add(unit);
                        }
                        else
                        {
                            // Fallback: 旧 UnitConfig 模式
                            var unitConfig = config.Get<UnitConfig>(order.unitType);
                            if (unitConfig == null) continue;
                            int seq = world.units.Values.Count(u => u.ownerCountry == country.id) + 1;
                            string unitId = $"{country.id}_{order.unitType}_{seq}";
                            var unit = UnitFactory.CreateFromTemplate(unitId, order.unitType, country.id, country.capitalProvinceId, unitConfig);
                            world.units[unitId] = unit;
                            country.unitIds.Add(unitId);
                            produced.Add(unit);
                        }

                        completed.Add(order);
                    }
                }
                foreach (var d in completed)
                    country.unitProductionQueue.Remove(d);
            }
            return produced;
        }
    }
}

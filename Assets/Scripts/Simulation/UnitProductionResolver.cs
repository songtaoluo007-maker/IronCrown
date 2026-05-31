// ============================================================================
// Simulation/UnitProductionResolver.cs — 造兵结算器
// C2a: 仅 infantry、仅首都、多回合队列
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class UnitProductionResolver
    {
        private static readonly HashSet<string> AllowedTypes = new() { "infantry" };

        /// <summary>尝试入队造兵（校验+扣费+入队）</summary>
        public CommandResult TryEnqueue(CountryState c, string unitType, IConfigRegistry config, EconomyConfig eco)
        {
            if (!AllowedTypes.Contains(unitType))
                return CommandResult.Reject($"unitType 不允许: {unitType}");

            var template = config.Get<UnitConfig>(unitType);
            if (template == null)
                return CommandResult.Reject($"未找到 unitType 模板: {unitType}");

            if (template.cost != null)
            {
                if (!c.HasResources(template.cost))
                    return CommandResult.Reject("资源不足");
            }

            if (c.manpower < template.hp)
                return CommandResult.Reject("人力不足");

            // C10: 装备库存校验（资源/人力校验之后、扣减之前）
            if (template.equipmentTrainingCost > 0 && c.equipmentStockpile < template.equipmentTrainingCost)
                return CommandResult.Reject("装备库存不足");

            // 全部校验通过后才扣减
            if (template.cost != null)
                c.ConsumeResources(template.cost);
            c.manpower -= template.hp;
            if (template.equipmentTrainingCost > 0)
                c.equipmentStockpile -= template.equipmentTrainingCost;

            c.unitProductionQueue.Add(new UnitProductionOrder
            {
                unitType = unitType,
                turnsRemaining = eco.unitProductionTurns
            });
            return CommandResult.Accept();
        }

        /// <summary>结算阶段：推进建造进度，完工则生成部队</summary>
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
                        var template = config.Get<UnitConfig>(order.unitType);
                        if (template == null) continue;

                        int seq = world.units.Values.Count(u => u.ownerCountry == country.id && u.unitType == order.unitType) + 1;
                        string shortType = order.unitType == "infantry" ? "inf" : order.unitType;
                        string unitId = $"{country.id}_{shortType}_{seq}";

                        var unit = UnitFactory.CreateFromTemplate(unitId, order.unitType, country.id, country.capitalProvinceId, template);
                        world.units[unitId] = unit;
                        country.unitIds.Add(unitId);
                        produced.Add(unit);
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

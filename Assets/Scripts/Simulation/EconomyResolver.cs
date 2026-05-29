// ============================================================================
// Simulation/EconomyResolver.cs — 经济结算器
// T5: 注入配置，实现 ResolveProduction，去硬编码
// ============================================================================

using System;
using System.Linq;
using IronCrown.Domain;
using IronCrown.Contracts;

namespace IronCrown.Simulation
{
    public sealed class EconomyResolver
    {
        private readonly IConfigRegistry _config;
        private readonly IEventPublisher _events;

        public EconomyResolver(IConfigRegistry config, IEventPublisher events)
        {
            _config = config;
            _events = events;
        }

        /// <summary>
        /// 内政阶段：省份产出 + 军工产出（确定性、有序）
        /// </summary>
        public void ResolveProduction(CountryState country, WorldState world)
        {
            var eco = _config.Get<EconomyConfig>("global");
            if (eco == null) return;

            // (1) 省份原料产出 → 国库存（按省份 id 升序，确定性）
            var ownedProvinces = world.provinces.Values
                .Where(p => p.ownerCountry == country.id)
                .OrderBy(p => p.id, StringComparer.Ordinal);

            foreach (var province in ownedProvinces)
            {
                if (province.resourceOutput == null) continue;
                foreach (var resId in province.resourceOutput)
                {
                    int amt = eco.provinceBaseOutputPerResource
                              + province.infrastructure * eco.provinceInfraOutputBonus;
                    int oldV = country.GetResource(resId);
                    country.ModifyResource(resId, amt);
                    _events.Publish(new ResourceChangedEvent
                    {
                        CountryId = country.id,
                        ResourceId = resId,
                        OldValue = oldV,
                        NewValue = oldV + amt
                    });
                }
            }

            // (2) 军工产出：steel(+capital) -> equipment（受输入门限）
            int desired = country.militaryFactories * eco.militaryFactoryEquipmentOutput;
            int bySteel = country.GetResource("steel") / Math.Max(1, eco.equipmentSteelCost);
            int byCap = country.GetResource("capital") / Math.Max(1, eco.equipmentCapitalCost);
            int actual = Math.Min(desired, Math.Min(bySteel, byCap));
            if (actual > 0)
            {
                country.ModifyResource("steel", -actual * eco.equipmentSteelCost);
                country.ModifyResource("capital", -actual * eco.equipmentCapitalCost);
                country.equipmentStockpile += actual;
            }
        }

        /// <summary>
        /// 结算阶段：税收 − 维护费（维护费来自配置）
        /// </summary>
        public EconomyResult ResolveEconomy(CountryState country, WorldState world)
        {
            var result = new EconomyResult();
            var eco = _config.Get<EconomyConfig>("global");

            float stabilityMod = 0.5f + (country.stability / 200f);
            result.taxIncome = (int)(country.taxIncome * stabilityMod);
            result.tradeIncome = country.tradeIncome;
            result.militaryExpense = CalculateMilitaryExpense(country, world, eco);
            result.civilExpense = country.civilExpense;
            result.netIncome = result.taxIncome + result.tradeIncome
                             - result.militaryExpense - result.civilExpense;

            if (country.inflation > 50)
            {
                float inflationPenalty = (country.inflation - 50) / 100f;
                result.netIncome = (int)(result.netIncome * (1f - inflationPenalty));
            }

            country.treasury += result.netIncome;

            if (country.inflation > 0)
                country.inflation = Math.Max(0, country.inflation - 1);

            return result;
        }

        private int CalculateMilitaryExpense(CountryState country, WorldState world, EconomyConfig eco)
        {
            int expense = 0;
            int civUpkeep = eco != null ? eco.civilianFactoryUpkeep : 2;
            int milUpkeep = eco != null ? eco.militaryFactoryUpkeep : 3;
            int dockUpkeep = eco != null ? eco.dockyardUpkeep : 4;

            expense += country.civilianFactories * civUpkeep;
            expense += country.militaryFactories * milUpkeep;
            expense += country.dockyards * dockUpkeep;
            // TODO: 遍历 unitIds 计算总维护费
            return expense;
        }
    }

    public struct EconomyResult
    {
        public int taxIncome;
        public int tradeIncome;
        public int militaryExpense;
        public int civilExpense;
        public int netIncome;
    }
}

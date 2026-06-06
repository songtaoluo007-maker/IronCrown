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
                .Where(p => p.controllerCountry == country.id)
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

            // (1.5) 每省基础粮食产出（避免单产出国饿死）
            foreach (var province in ownedProvinces)
            {
                int foodAmt = eco.provinceBaseFoodOutput;
                if (foodAmt > 0)
                {
                    int oldFood = country.GetResource("food");
                    country.ModifyResource("food", foodAmt);
                    _events.Publish(new ResourceChangedEvent
                    {
                        CountryId = country.id,
                        ResourceId = "food",
                        OldValue = oldFood,
                        NewValue = oldFood + foodAmt
                    });
                }
            }

            // (1.6) 每省基础钢铁产出（避免单产出国饿死）
            foreach (var province in ownedProvinces)
            {
                int steelAmt = eco.provinceBaseSteelOutput;
                if (steelAmt > 0)
                {
                    int oldSteel = country.GetResource("steel");
                    country.ModifyResource("steel", steelAmt);
                    _events.Publish(new ResourceChangedEvent
                    {
                        CountryId = country.id,
                        ResourceId = "steel",
                        OldValue = oldSteel,
                        NewValue = oldSteel + steelAmt
                    });
                }
            }

            // (1.7) 民用工厂产出 capital（T5 遗漏，C9a 修复）
            int capitalOutput = country.civilianFactories * eco.civilianFactoryCapitalOutput;
            if (capitalOutput > 0)
            {
                int oldCap = country.GetResource("capital");
                country.ModifyResource("capital", capitalOutput);
                _events.Publish(new ResourceChangedEvent
                {
                    CountryId = country.id,
                    ResourceId = "capital",
                    OldValue = oldCap,
                    NewValue = oldCap + capitalOutput
                });
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

            int taxLv = Math.Clamp(country.taxLevel, 0, 2);
            int civLv = Math.Clamp(country.civilLevel, 0, 2);

            float stabilityMod = 0.5f + (country.stability / 200f);
            int baseTax = (int)(country.taxIncome * stabilityMod);
            result.taxIncome = baseTax * (eco?.taxRatePercents?[taxLv] ?? 100) / 100;
            result.tradeIncome = country.tradeIncome;
            result.militaryExpense = CalculateMilitaryExpense(country, world, eco);
            result.civilExpense = country.civilExpense * (eco?.civilExpensePercents?[civLv] ?? 100) / 100;
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

            // C10: treasury → capital 自动转化
            if (country.treasury > 0 && eco != null && eco.treasuryToCapitalRatePct > 0)
            {
                int conversion = country.treasury * eco.treasuryToCapitalRatePct / 100;
                if (conversion > 0)
                {
                    country.treasury -= conversion;
                    country.ModifyResource("capital", conversion);
                }
            }

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

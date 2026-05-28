// ============================================================================
// Simulation/EconomyResolver.cs — 经济结算器
// Phase 5: GameState → WorldState
// ============================================================================

using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class EconomyResolver
    {
        public EconomyResult ResolveEconomy(CountryState country, WorldState world)
        {
            var result = new EconomyResult();

            float stabilityMod = 0.5f + (country.stability / 200f);
            result.taxIncome = (int)(country.taxIncome * stabilityMod);
            result.tradeIncome = country.tradeIncome;
            result.militaryExpense = CalculateMilitaryExpense(country, world);
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
                country.inflation = System.Math.Max(0, country.inflation - 1);

            return result;
        }

        public void ResolveProduction(CountryState country, WorldState world)
        {
            int factoryOutput = country.militaryFactories;
            float efficiency = 1.0f;
            // TODO: 科技/政策加成
        }

        private int CalculateMilitaryExpense(CountryState country, WorldState world)
        {
            int expense = 0;
            expense += country.civilianFactories * 2;
            expense += country.militaryFactories * 3;
            expense += country.dockyards * 4;
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

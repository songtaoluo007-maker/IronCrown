// ============================================================================
// Tests/EditMode/EconomyResolverTests.cs �?经济结算器回归测�?// 锁定当前公式行为，防止后续改动引入回�?// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;

namespace IronCrown.Simulation.Tests
{
    public class EconomyResolverTests
    {
        [Test]
        public void ResolveEconomy_StableCountry_PositiveNetIncome()
        {
            var resolver = new EconomyResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 80,
                taxIncome = 100,
                tradeIncome = 50,
                civilExpense = 20,
                civilianFactories = 2,
                militaryFactories = 1,
                dockyards = 0
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // stabilityMod = 0.5 + 80/200 = 0.9
            // taxIncome = (int)(100 * 0.9f) = 89 (truncation)
            // tradeIncome = 50
            // militaryExpense = 2*2 + 1*3 + 0*4 = 7
            // netIncome = 89 + 50 - 7 - 20 = 112
            Assert.AreEqual(89, result.taxIncome);
            Assert.AreEqual(50, result.tradeIncome);
            Assert.AreEqual(7, result.militaryExpense);
            Assert.AreEqual(112, result.netIncome);
        }

        [Test]
        public void ResolveEconomy_NonIntegerDivision_Truncates()
        {
            var resolver = new EconomyResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 70,
                taxIncome = 95,
                tradeIncome = 0,
                civilExpense = 0,
                civilianFactories = 0,
                militaryFactories = 0,
                dockyards = 0
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // stabilityMod = 0.5 + 70/200 = 0.85
            // taxIncome = (int)(95 * 0.85f) = (int)(80.75f) = 80 (truncation)
            Assert.AreEqual(80, result.taxIncome);
        }

        [Test]
        public void ResolveEconomy_HighInflation_Penalized()
        {
            var resolver = new EconomyResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxIncome = 200,
                tradeIncome = 0,
                civilExpense = 0,
                inflation = 80
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // inflationPenalty = (80-50)/100 = 0.3
            // netIncome should be reduced by 30%
            Assert.Less(result.netIncome, 200);
        }

        [Test]
        public void ResolveEconomy_InflationDecays()
        {
            var resolver = new EconomyResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxIncome = 100,
                inflation = 30
            };
            var world = new WorldState();

            resolver.ResolveEconomy(country, world);
            Assert.AreEqual(29, country.inflation);
        }
    }
}


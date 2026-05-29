// ============================================================================
// Tests/EditMode/PoliticsResolverTests.cs — 政治结算器测试
// B1.5: 税率/民生档位稳定修正
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using System.Collections.Generic;

namespace IronCrown.Simulation.Tests
{
    public class PoliticsResolverTests
    {
        private TestConfigRegistry _config;

        private EconomyConfig DefaultEconomyConfig() => new EconomyConfig
        {
            id = "global",
            taxRatePercents = new[] { 70, 100, 130 },
            taxStabilityDeltas = new[] { 1, 0, -2 },
            civilExpensePercents = new[] { 50, 100, 150 },
            civilStabilityDeltas = new[] { -2, 0, 2 }
        };

        [SetUp]
        public void SetUp()
        {
            _config = new TestConfigRegistry();
            _config.Register("global", DefaultEconomyConfig());
        }

        private PoliticsResolver CreateResolver() => new PoliticsResolver(_config);

        [Test]
        public void ResolveStability_TaxLevel2_HighTax_DropsStability()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxLevel = 2,   // 高税: -2 稳定
                civilLevel = 1, // 正常: 0
                corruption = 0
            };
            var world = new WorldState();

            resolver.ResolvePolitics(country, world);

            // baseRecovery=1, corruptionPenalty=0, taxDelta=-2, civilDelta=0
            // change = 1 - 0 + 0 + (-2) = -1
            // stability = 50 + (-1) = 49
            Assert.AreEqual(49, country.stability);
        }

        [Test]
        public void ResolveStability_CivilLevel0_Austerity_DropsStability()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxLevel = 1,   // 中: 0
                civilLevel = 0, // 紧缩: -2
                corruption = 0
            };
            var world = new WorldState();

            resolver.ResolvePolitics(country, world);

            // change = 1 - 0 + 0 + (-2) = -1
            Assert.AreEqual(49, country.stability);
        }

        [Test]
        public void ResolveStability_CivilLevel2_Welfare_BoostsStability()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxLevel = 1,   // 中: 0
                civilLevel = 2, // 宽裕: +2
                corruption = 0
            };
            var world = new WorldState();

            resolver.ResolvePolitics(country, world);

            // change = 1 - 0 + 0 + 2 = 3
            Assert.AreEqual(53, country.stability);
        }

        [Test]
        public void ResolveStability_TaxHigh_CivilAusterity_DoublePenalty()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxLevel = 2,   // 高税: -2
                civilLevel = 0, // 紧缩: -2
                corruption = 0
            };
            var world = new WorldState();

            resolver.ResolvePolitics(country, world);

            // change = 1 - 0 + (-2) + (-2) = -3
            Assert.AreEqual(47, country.stability);
        }

        [Test]
        public void ResolveStability_TaxLow_CivilWelfare_MaxBoost()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxLevel = 0,   // 低税: +1
                civilLevel = 2, // 宽裕: +2
                corruption = 0
            };
            var world = new WorldState();

            resolver.ResolvePolitics(country, world);

            // change = 1 - 0 + 1 + 2 = 4
            Assert.AreEqual(54, country.stability);
        }

        [Test]
        public void ResolveStability_ValuesClamped0to100()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 0,
                taxLevel = 2,   // -2
                civilLevel = 0, // -2
                corruption = 0
            };
            var world = new WorldState();

            resolver.ResolvePolitics(country, world);

            // change = 1 + (-2) + (-2) = -3, but clamp to 0
            Assert.AreEqual(0, country.stability);
        }

        [Test]
        public void ResolveStability_UsesConfigValues()
        {
            // 修改配置值验证联动
            var custom = new TestConfigRegistry();
            custom.Register("global", new EconomyConfig
            {
                id = "global",
                taxStabilityDeltas = new[] { 5, 0, -10 },
                civilStabilityDeltas = new[] { -10, 0, 5 }
            });
            var resolver = new PoliticsResolver(custom);

            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxLevel = 0,   // custom: +5
                civilLevel = 2, // custom: +5
                corruption = 0
            };
            var world = new WorldState();

            resolver.ResolvePolitics(country, world);

            // change = 1 + 5 + 5 = 11
            Assert.AreEqual(61, country.stability);
        }
    }
}

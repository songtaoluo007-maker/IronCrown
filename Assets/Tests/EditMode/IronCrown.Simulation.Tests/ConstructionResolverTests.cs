// ============================================================================
// ConstructionResolverTests.cs — B3 TryBuild + 原有结算测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using System.Collections.Generic;

namespace IronCrown.Simulation.Tests
{
    public class ConstructionResolverTests
    {
        private EconomyConfig CreateEco()
        {
            return new EconomyConfig
            {
                civilianFactoryBuildCost = 30,
                militaryFactoryBuildCost = 40,
                factoryBuildTurns = 3
            };
        }

        // === TryBuild 测试 (B3) ===

        [Test]
        public void TryBuild_CapitalEnough_DeductsAndEnqueues()
        {
            var resolver = new ConstructionResolver();
            var eco = CreateEco();
            var country = new CountryState
            {
                ideology = Ideology.FreeRepublic,
                civilianFactories = 2,
                resources = new Dictionary<string, int> { { "capital", 100 } }
            };

            bool result = resolver.TryBuild(country, "civilian", eco);

            Assert.IsTrue(result);
            Assert.AreEqual(70, country.GetResource("capital"));  // 100 - 30
            Assert.AreEqual(1, country.constructionQueue.Count);
        }

        [Test]
        public void TryBuild_CapitalInsufficient_ReturnsFalse()
        {
            var resolver = new ConstructionResolver();
            var eco = CreateEco();
            var country = new CountryState
            {
                ideology = Ideology.FreeRepublic,
                resources = new Dictionary<string, int> { { "capital", 20 } }  // < 30
            };

            bool result = resolver.TryBuild(country, "civilian", eco);

            Assert.IsFalse(result);
            Assert.AreEqual(20, country.GetResource("capital"));  // unchanged
            Assert.AreEqual(0, country.constructionQueue.Count);
        }

        [Test]
        public void TryBuild_Military_UsesCorrectCost()
        {
            var resolver = new ConstructionResolver();
            var eco = CreateEco();
            var country = new CountryState
            {
                ideology = Ideology.FreeRepublic,
                resources = new Dictionary<string, int> { { "capital", 100 } }
            };

            bool result = resolver.TryBuild(country, "military", eco);

            Assert.IsTrue(result);
            Assert.AreEqual(60, country.GetResource("capital"));  // 100 - 40
            Assert.AreEqual("military", country.constructionQueue[0].factoryKind);
        }

        // === 原有结算测试 ===

        [Test]
        public void ResolveConstruction_CompletesAfterBuildTurns()
        {
            var resolver = new ConstructionResolver();
            var country = new CountryState
            {
                ideology = Ideology.FreeRepublic,
                civilianFactories = 2
            };
            country.constructionQueue.Add(new ConstructionOrder
            {
                factoryKind = "civilian",
                turnsRemaining = 1
            });

            resolver.ResolveConstruction(country);

            Assert.AreEqual(3, country.civilianFactories);
            Assert.AreEqual(0, country.constructionQueue.Count);
        }

        [Test]
        public void ResolveConstruction_ContinuesBuilding()
        {
            var resolver = new ConstructionResolver();
            var country = new CountryState
            {
                ideology = Ideology.FreeRepublic,
                civilianFactories = 2
            };
            country.constructionQueue.Add(new ConstructionOrder
            {
                factoryKind = "civilian",
                turnsRemaining = 2
            });

            resolver.ResolveConstruction(country);

            Assert.AreEqual(2, country.civilianFactories);
            Assert.AreEqual(1, country.constructionQueue[0].turnsRemaining);
        }

        [Test]
        public void ResolveConstruction_MilitaryFactory()
        {
            var resolver = new ConstructionResolver();
            var country = new CountryState
            {
                ideology = Ideology.FreeRepublic,
                militaryFactories = 1
            };
            country.constructionQueue.Add(new ConstructionOrder
            {
                factoryKind = "military",
                turnsRemaining = 1
            });

            resolver.ResolveConstruction(country);

            Assert.AreEqual(2, country.militaryFactories);
        }
    }
}

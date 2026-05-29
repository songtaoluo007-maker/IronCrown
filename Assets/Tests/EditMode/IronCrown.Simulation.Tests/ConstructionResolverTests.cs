// ============================================================================
// ConstructionResolverTests.cs — 建造结算单测
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using System.Collections.Generic;

namespace IronCrown.Simulation.Tests
{
    public class ConstructionResolverTests
    {
        private EconomyConfig CreateConfig()
        {
            return new EconomyConfig
            {
                id = "global",
                civilianFactoryBuildCost = 30,
                militaryFactoryBuildCost = 40,
                factoryBuildTurns = 3
            };
        }

        [Test]
        public void EnqueueBuild_AddsToQueue()
        {
            var resolver = new ConstructionResolver();
            var config = CreateConfig();
            var country = new CountryState { id = "test", civilianFactories = 2 };

            resolver.EnqueueBuild(country, "civilian", config);

            Assert.AreEqual(1, country.constructionQueue.Count);
            Assert.AreEqual("civilian", country.constructionQueue[0].factoryKind);
            Assert.AreEqual(3, country.constructionQueue[0].turnsRemaining);
        }

        [Test]
        public void ResolveConstruction_CompletesAfterTurns()
        {
            var resolver = new ConstructionResolver();
            var config = CreateConfig();
            var country = new CountryState { id = "test", civilianFactories = 2 };

            resolver.EnqueueBuild(country, "civilian", config);

            // 推进 2 回合——未完工
            resolver.ResolveConstruction(country);
            resolver.ResolveConstruction(country);
            Assert.AreEqual(1, country.constructionQueue.Count);
            Assert.AreEqual(2, country.civilianFactories);

            // 第 3 回合——完工
            resolver.ResolveConstruction(country);
            Assert.AreEqual(0, country.constructionQueue.Count);
            Assert.AreEqual(3, country.civilianFactories);
        }

        [Test]
        public void ResolveConstruction_MilitaryFactory()
        {
            var resolver = new ConstructionResolver();
            var config = CreateConfig();
            var country = new CountryState { id = "test", militaryFactories = 1 };

            resolver.EnqueueBuild(country, "military", config);

            for (int i = 0; i < 3; i++)
                resolver.ResolveConstruction(country);

            Assert.AreEqual(0, country.constructionQueue.Count);
            Assert.AreEqual(2, country.militaryFactories);
        }

        [Test]
        public void ResolveConstruction_MultipleOrders_FIFO()
        {
            var resolver = new ConstructionResolver();
            var config = CreateConfig();
            var country = new CountryState { id = "test", civilianFactories = 0, militaryFactories = 0 };

            resolver.EnqueueBuild(country, "civilian", config);
            resolver.EnqueueBuild(country, "military", config);

            Assert.AreEqual(2, country.constructionQueue.Count);

            // 推进 3 回合——两个都完工
            for (int i = 0; i < 3; i++)
                resolver.ResolveConstruction(country);

            Assert.AreEqual(0, country.constructionQueue.Count);
            Assert.AreEqual(1, country.civilianFactories);
            Assert.AreEqual(1, country.militaryFactories);
        }
    }
}

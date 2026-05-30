// ============================================================================
// AIResolverTests.cs — B3 AI 经济决策测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using System.Collections.Generic;

namespace IronCrown.Simulation.Tests
{
    // 本地 stub，不依赖 Application 层
    internal class LocalConfigRegistry : IConfigRegistry
    {
        private readonly Dictionary<string, object> _data = new();
        public void Register<T>(string id, T value) where T : class => _data[id] = value;
        public T Get<T>(string id) where T : class => _data.TryGetValue(id, out var v) ? v as T : null;
        public IReadOnlyList<T> All<T>() where T : class => new List<T>();
        public bool Has<T>(string id) where T : class => _data.ContainsKey(id);
        public void LoadAll() { }
    }

    public class AIResolverTests
    {
        private LocalConfigRegistry CreateConfig()
        {
            var config = new LocalConfigRegistry();
            config.Register("global", new EconomyConfig
            {
                civilianFactoryBuildCost = 30,
                militaryFactoryBuildCost = 40,
                factoryBuildTurns = 3,
                aiBuildCapitalThreshold = 60,
                aiMaxCivilianFactories = 20,
                aiMaxMilitaryFactories = 15
            });
            return config;
        }

        [Test]
        public void MakeDecisions_CapitalAboveThreshold_BuildsCivilian()
        {
            var config = CreateConfig();
            var construction = new ConstructionResolver();
            var ai = new AIResolver(config, construction, new BattleResolver(rng, new EventBus()));

            var country = new CountryState
            {
                id = "ai_nation",
                ideology = Ideology.FreeRepublic,
                civilianFactories = 2,
                militaryFactories = 1,
                resources = new Dictionary<string, int> { { "capital", 200 } }
            };
            var world = new WorldState();
            world.countries["ai_nation"] = country;

            ai.MakeDecisions(country, world);

            // capital should decrease by 30 (civilian cost)
            Assert.AreEqual(170, country.GetResource("capital"));
            Assert.AreEqual(1, country.constructionQueue.Count);
            Assert.AreEqual("civilian", country.constructionQueue[0].factoryKind);
        }

        [Test]
        public void MakeDecisions_CapitalBelowThreshold_NoAction()
        {
            var config = CreateConfig();
            var construction = new ConstructionResolver();
            var ai = new AIResolver(config, construction, new BattleResolver(rng, new EventBus()));

            var country = new CountryState
            {
                id = "poor_nation",
                ideology = Ideology.FreeRepublic,
                civilianFactories = 2,
                resources = new Dictionary<string, int> { { "capital", 50 } }  // below threshold 60
            };
            var world = new WorldState();
            world.countries["poor_nation"] = country;

            ai.MakeDecisions(country, world);

            Assert.AreEqual(50, country.GetResource("capital"));  // unchanged
            Assert.AreEqual(0, country.constructionQueue.Count);
        }

        [Test]
        public void MakeDecisions_CivilianMax_SwitchesToMilitary()
        {
            var config = CreateConfig();
            var construction = new ConstructionResolver();
            var ai = new AIResolver(config, construction, new BattleResolver(rng, new EventBus()));

            var country = new CountryState
            {
                id = "industrial",
                ideology = Ideology.ImperialOrder,
                civilianFactories = 20,  // at max
                militaryFactories = 5,
                resources = new Dictionary<string, int> { { "capital", 200 } }
            };
            var world = new WorldState();
            world.countries["industrial"] = country;

            ai.MakeDecisions(country, world);

            Assert.AreEqual(160, country.GetResource("capital"));  // -40 military cost
            Assert.AreEqual("military", country.constructionQueue[0].factoryKind);
        }

        [Test]
        public void MakeDecisions_PlayerCountry_Skipped()
        {
            var config = CreateConfig();
            var construction = new ConstructionResolver();
            var ai = new AIResolver(config, construction, new BattleResolver(rng, new EventBus()));

            var country = new CountryState
            {
                id = "player_nation",
                ideology = Ideology.FreeRepublic,
                civilianFactories = 2,
                resources = new Dictionary<string, int> { { "capital", 200 } }
            };
            var world = new WorldState { playerCountryId = "player_nation" };
            world.countries["player_nation"] = country;

            ai.MakeDecisions(country, world);

            Assert.AreEqual(200, country.GetResource("capital"));  // unchanged
            Assert.AreEqual(0, country.constructionQueue.Count);
        }
    }
}

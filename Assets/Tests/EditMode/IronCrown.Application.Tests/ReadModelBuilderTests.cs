// ============================================================================
// ReadModelBuilderTests.cs — ReadModelBuilder 单元测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Application;
using IronCrown.Contracts;
using IronCrown.Domain;
using System.Collections.Generic;

namespace IronCrown.Application.Tests
{
    public class ReadModelBuilderTests
    {
        private ReadModelBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _builder = new ReadModelBuilder();
        }

        [Test]
        public void BuildWorldView_MapsFieldsCorrectly()
        {
            var world = new WorldState();
            world.countries["alpha"] = new CountryState
            {
                id = "alpha",
                name = "Alpha",
                ideology = Ideology.ImperialOrder,
                treasury = 500,
                stability = 75,
                warSupport = 60,
                legitimacy = 90,
                civilianFactories = 3,
                militaryFactories = 2,
                dockyards = 1,
                manpower = 10000,
                resources = new Dictionary<string, int> { { "steel", 50 }, { "oil", 20 } }
            };
            world.countries["beta"] = new CountryState
            {
                id = "beta",
                name = "Beta",
                ideology = Ideology.FreeRepublic,
                treasury = 300,
                stability = 60,
                warSupport = 40,
                legitimacy = 80,
                civilianFactories = 2,
                militaryFactories = 1,
                dockyards = 0,
                manpower = 5000,
                resources = new Dictionary<string, int> { { "steel", 30 } }
            };

            var clock = new GameClock(new EventBus());
            clock.Reset(60);

            var view = _builder.BuildWorldView(world, clock);

            Assert.AreEqual(1, view.turn);
            Assert.AreEqual("TurnStart", view.phase);
            Assert.AreEqual(2, view.countries.Count);
        }

        [Test]
        public void BuildWorldView_CountriesSortedById()
        {
            var world = new WorldState();
            world.countries["zulu"] = new CountryState { id = "zulu", name = "Zulu", ideology = Ideology.FreeRepublic };
            world.countries["alpha"] = new CountryState { id = "alpha", name = "Alpha", ideology = Ideology.ImperialOrder };

            var clock = new GameClock(new EventBus());
            var view = _builder.BuildWorldView(world, clock);

            Assert.AreEqual("alpha", view.countries[0].id);
            Assert.AreEqual("zulu", view.countries[1].id);
        }

        [Test]
        public void BuildCountryView_IdeologyIsString()
        {
            var country = new CountryState
            {
                id = "test",
                name = "Test",
                ideology = Ideology.ImperialOrder,
                resources = new Dictionary<string, int>()
            };

            var view = _builder.BuildCountryView(country);

            Assert.AreEqual("ImperialOrder", view.ideology);
            Assert.AreEqual("test", view.id);
            Assert.AreEqual("Test", view.name);
        }

        [Test]
        public void BuildCountryView_ResourcesAreCopied()
        {
            var country = new CountryState
            {
                id = "test",
                name = "Test",
                ideology = Ideology.FreeRepublic,
                resources = new Dictionary<string, int> { { "steel", 100 } }
            };

            var view = _builder.BuildCountryView(country);

            Assert.AreEqual(100, view.resources["steel"]);
            // Verify it's a copy, not a reference
            country.resources["steel"] = 999;
            Assert.AreEqual(100, view.resources["steel"]);
        }
    }
}

// ============================================================================
// Tests/PeaceResolverC5Tests.cs — 停战和平期测试 (C9d)
// ============================================================================

using System;
using System.Collections.Generic;
using IronCrown.Contracts;
using IronCrown.Domain;
using IronCrown.Simulation;
using NUnit.Framework;

namespace IronCrown.Simulation.Tests
{
    [TestFixture]
    public class PeaceResolverC5Tests
    {
        private IEventPublisher _events;
        private PeaceResolver _peace;
        private EconomyConfig _eco;

        [SetUp]
        public void SetUp()
        {
            _events = new EventBus();
            _peace = new PeaceResolver(_events);
            _eco = new EconomyConfig
            {
                id = "global",
                aiPeaceAcceptExhaustionThreshold = 30,
                aiPeaceAcceptPowerRatioPct = 80,
                aiPeaceOfferCooldownTurns = 5,
                aiPeaceOfferExhaustionThreshold = 40,
                aiPeaceOfferPowerRatioPct = 80,
                aiPeaceOfferExpiryTurns = 10,
                aiPeaceTruceTurns = 10
            };
        }

        private WorldState CreateWorld()
        {
            var world = new WorldState
            {
                countries = new Dictionary<string, CountryState>(),
                provinces = new Dictionary<string, ProvinceState>(),
                units = new Dictionary<string, UnitState>(),
                warRelations = new List<WarRelation>(),
                activeBattles = new List<ActiveBattle>(),
                truceUntilTurn = new Dictionary<string, int>()
            };
            var a = new CountryState { id = "countryA", name = "Alpha", stability = 100, warSupport = 100, civilianFactories = 5, militaryFactories = 3 };
            var b = new CountryState { id = "countryB", name = "Beta", stability = 100, warSupport = 100, civilianFactories = 5, militaryFactories = 3 };
            world.countries[a.id] = a;
            world.countries[b.id] = b;
            WarRegistry.TryDeclareWar(world, "countryA", "countryB", 1, out _);
            world.turnNumber = 5;
            return world;
        }

        [Test]
        public void OfferPeace_AcceptedSetsTruce()
        {
            var world = CreateWorld();
            world.countries["countryB"].warExhaustion = 60; // 足够高让 AI 接受

            _peace.OfferPeace(world, "countryA", "countryB", _eco);

            Assert.IsTrue(WarRegistry.IsInTruce(world, "countryA", "countryB", 5 + 10 - 1),
                "停战后应设和平期");
            Assert.IsFalse(WarRegistry.IsInTruce(world, "countryA", "countryB", 5 + 10),
                "和平期到期后不应再有");
        }

        [Test]
        public void OfferPeace_TruceUntilTurn_IsCurrentPlusConfig()
        {
            var world = CreateWorld();
            world.turnNumber = 8;
            world.countries["countryB"].warExhaustion = 60;

            _peace.OfferPeace(world, "countryA", "countryB", _eco);

            // truceUntilTurn = 8 + 10 = 18
            Assert.IsTrue(WarRegistry.IsInTruce(world, "countryA", "countryB", 17));
            Assert.IsFalse(WarRegistry.IsInTruce(world, "countryA", "countryB", 18));
        }

        [Test]
        public void AcceptPeace_AlsoSetsTruce()
        {
            var world = CreateWorld();
            var player = world.countries["countryA"];
            player.pendingPeaceOfferFrom = "countryB";
            world.countries["countryB"].warExhaustion = 50;

            _peace.AcceptPeace(world, "countryA", "countryB", _eco);

            Assert.IsTrue(WarRegistry.IsInTruce(world, "countryA", "countryB", 5 + 10 - 1),
                "AcceptPeace 后应设和平期");
        }
    }
}

// ============================================================================
// Tests/AiPeaceOfferResolverTests.cs — C7 AI 主动求和测试
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Tests
{
    [TestFixture]
    public class AiPeaceOfferResolverTests
    {
        private EconomyConfig _eco;
        private EventRecorderC7 _events;

        [SetUp]
        public void SetUp()
        {
            _eco = new EconomyConfig
            {
                aiPeaceOfferExhaustionThreshold = 40,
                aiPeaceOfferPowerRatioPct = 80,
                aiPeaceOfferCooldownTurns = 5,
                aiPeaceOfferExpiryTurns = 10
            };
            _events = new EventRecorderC7();
        }

        // ── 触发条件 ──

        [Test]
        public void Trigger_AiExhaustedAndWeak_OfferIssued()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            ai.warExhaustion = 40; // >= threshold
            ai.civilianFactories = 2;
            player.civilianFactories = 10; // ai power <= 80% of player

            var resolver = new AiPeaceOfferResolver(_events);
            resolver.CheckAiPeaceOffers(world, _eco, "player", 1);

            Assert.AreEqual("ai", player.pendingPeaceOfferFrom);
            Assert.AreEqual(1, _events.Offers.Count);
            Assert.AreEqual("ai", _events.Offers[0].fromCountry);
            Assert.AreEqual("player", _events.Offers[0].toCountry);
            Assert.AreEqual(11, _events.Offers[0].expiryTurnNumber); // 1 + 10
        }

        [Test]
        public void NoTrigger_ExhaustionTooLow()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            ai.warExhaustion = 39; // < 40

            var resolver = new AiPeaceOfferResolver(_events);
            resolver.CheckAiPeaceOffers(world, _eco, "player", 1);

            Assert.IsNull(player.pendingPeaceOfferFrom);
            Assert.AreEqual(0, _events.Offers.Count);
        }

        [Test]
        public void NoTrigger_AiTooStrong()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            ai.warExhaustion = 40;
            ai.civilianFactories = 10;
            player.civilianFactories = 5; // ai power > 80% of player

            var resolver = new AiPeaceOfferResolver(_events);
            resolver.CheckAiPeaceOffers(world, _eco, "player", 1);

            Assert.IsNull(player.pendingPeaceOfferFrom);
            Assert.AreEqual(0, _events.Offers.Count);
        }

        // ── 冷却机制 ──

        [Test]
        public void Cooldown_BlocksOffer()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            ai.warExhaustion = 60;
            ai.peaceOfferCooldown = 3; // still cooling down

            var resolver = new AiPeaceOfferResolver(_events);
            resolver.CheckAiPeaceOffers(world, _eco, "player", 1);

            Assert.IsNull(player.pendingPeaceOfferFrom);
            Assert.AreEqual(0, _events.Offers.Count);
            Assert.AreEqual(2, ai.peaceOfferCooldown); // decremented
        }

        [Test]
        public void Cooldown_DecrementsEachTurn()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            ai.warExhaustion = 60;
            ai.peaceOfferCooldown = 1;

            var resolver = new AiPeaceOfferResolver(_events);

            // Turn 1: cooldown decremented from 1 to 0, but offer not issued yet (continue)
            resolver.CheckAiPeaceOffers(world, _eco, "player", 1);
            Assert.IsNull(player.pendingPeaceOfferFrom, "冷却回合内不应触发");
            Assert.AreEqual(0, ai.peaceOfferCooldown);

            // Turn 2: cooldown is 0, offer should be issued
            resolver.CheckAiPeaceOffers(world, _eco, "player", 2);
            Assert.AreEqual("ai", player.pendingPeaceOfferFrom, "冷却结束后应触发");
        }

        // ── 过期/重复 ──

        [Test]
        public void NoTrigger_AlreadyPending()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            ai.warExhaustion = 60;
            player.pendingPeaceOfferFrom = "other_country"; // already has a pending offer

            var resolver = new AiPeaceOfferResolver(_events);
            resolver.CheckAiPeaceOffers(world, _eco, "player", 1);

            Assert.AreEqual("other_country", player.pendingPeaceOfferFrom); // unchanged
            Assert.AreEqual(0, _events.Offers.Count);
        }

        // ── AI vs AI 跳过 ──

        [Test]
        public void NoTrigger_AiVsAi_Skipped()
        {
            var world = CreateWorld(out var player, out var ai);
            // 两个 AI 国家交战，玩家不参与
            var other = new CountryState { id = "other_ai", name = "OtherAI", civilianFactories = 5 };
            world.countries["other_ai"] = other;
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "other_ai" });
            ai.warExhaustion = 60;

            var resolver = new AiPeaceOfferResolver(_events);
            resolver.CheckAiPeaceOffers(world, _eco, "player", 1);

            // Player not involved in this war, no offers should be made
            Assert.IsNull(player.pendingPeaceOfferFrom);
            Assert.AreEqual(0, _events.Offers.Count);
        }

        // ── AcceptPeace ──

        [Test]
        public void AcceptPeace_EndsWarAndHalvesExhaustion()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            player.pendingPeaceOfferFrom = "ai";
            player.warExhaustion = 40;
            ai.warExhaustion = 60;

            var resolver = new PeaceResolver(_events);
            var result = resolver.AcceptPeace(world, "player", "ai", _eco);

            Assert.IsTrue(result.accepted);
            Assert.AreEqual(0, world.warRelations.Count);
            Assert.AreEqual(20, player.warExhaustion); // 40/2
            Assert.AreEqual(30, ai.warExhaustion);     // 60/2
            Assert.IsNull(player.pendingPeaceOfferFrom);
        }

        [Test]
        public void AcceptPeace_RejectsIfNoPending()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            player.pendingPeaceOfferFrom = null; // no pending

            var resolver = new PeaceResolver(_events);
            var result = resolver.AcceptPeace(world, "player", "ai", _eco);

            Assert.IsFalse(result.accepted);
        }

        [Test]
        public void AcceptPeace_RejectsIfWrongSender()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            player.pendingPeaceOfferFrom = "other_country"; // wrong sender

            var resolver = new PeaceResolver(_events);
            var result = resolver.AcceptPeace(world, "player", "ai", _eco);

            Assert.IsFalse(result.accepted);
        }

        // ── RejectPeace ──

        [Test]
        public void RejectPeace_SetsCooldown()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "ai", countryB = "player" });
            player.pendingPeaceOfferFrom = "ai";

            var resolver = new PeaceResolver(_events);
            var result = resolver.RejectPeace(world, "player", "ai", _eco);

            Assert.IsTrue(result.accepted);
            Assert.IsNull(player.pendingPeaceOfferFrom);
            Assert.AreEqual(5, ai.peaceOfferCooldown);
            Assert.AreEqual(1, world.warRelations.Count); // war continues
        }

        [Test]
        public void RejectPeace_RejectsIfNoPending()
        {
            var world = CreateWorld(out var player, out var ai);
            player.pendingPeaceOfferFrom = null;

            var resolver = new PeaceResolver(_events);
            var result = resolver.RejectPeace(world, "player", "ai", _eco);

            Assert.IsFalse(result.accepted);
        }

        // ── 辅助 ──

        private WorldState CreateWorld(out CountryState player, out CountryState ai)
        {
            player = new CountryState
            {
                id = "player",
                name = "Player",
                stability = 70,
                warSupport = 60,
                civilianFactories = 10,
                militaryFactories = 5
            };
            ai = new CountryState
            {
                id = "ai",
                name = "AI",
                stability = 60,
                warSupport = 50,
                civilianFactories = 5,
                militaryFactories = 3
            };

            return new WorldState
            {
                countries = new Dictionary<string, CountryState>
                {
                    { "player", player },
                    { "ai", ai }
                },
                provinces = new Dictionary<string, ProvinceState>(),
                units = new Dictionary<string, UnitState>(),
                activeBattles = new List<ActiveBattle>(),
                warRelations = new List<WarRelation>()
            };
        }
    }

    internal class EventRecorderC7 : IEventPublisher
    {
        public List<AiPeaceOfferedEvent> Offers = new();
        public List<PeaceConcludedEvent> Peaces = new();
        public void Publish<T>(T evt)
        {
            if (evt is AiPeaceOfferedEvent e) Offers.Add(e);
            if (evt is PeaceConcludedEvent p) Peaces.Add(p);
        }
        public void Subscribe<T>(Action<T> handler) { }
        public void Unsubscribe<T>(Action<T> handler) { }
        public void Clear() { }
    }
}

// ============================================================================
// GachaResolverTests.cs — [已废弃 P2.1] 保留 AwardTickets 兼容测试
// 新测试请使用 CommanderUnlockResolverTests
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Simulation.Tests
{
    /// <summary>
    /// P2.1: GachaResolver 仅保留 AwardTicketsForVictory 兼容方法。
    /// DrawCard/保底/商城测试全部移除,功能已迁移至 CommanderUnlockResolverTests。
    /// </summary>
    public class GachaResolverTests
    {
        private EconomyConfig _eco;
        private GachaResolver _gacha;

        [SetUp]
        public void SetUp()
        {
            _eco = new EconomyConfig
            {
                id = "global",
                gachaTicketsPerVictory = 1,
                gachaTicketsPerEncirclement = 3,
                gachaTicketsPerCapitalCapture = 10,
                starBonusPerStar = 5,
                maxStarLevel = 5
            };
            // P2.1: GachaResolver 无参构造（兼容）
            _gacha = new GachaResolver();
        }

        private (CountryState country, WorldState world) MakeWorld(int tickets = 0)
        {
            var world = new WorldState();
            var country = new CountryState { id = "empire", name = "帝国", gachaTickets = tickets };
            world.countries["empire"] = country;
            return (country, world);
        }

        [Test]
        public void AwardTickets_Victory_AddsOne()
        {
            var (country, world) = MakeWorld();
            _gacha.AwardTicketsForVictory(country, _eco);
            Assert.AreEqual(1, country.gachaTickets);
        }

        [Test]
        public void AwardTickets_Encirclement_AddsFour()
        {
            var (country, world) = MakeWorld();
            _gacha.AwardTicketsForVictory(country, _eco, wasEncirclement: true);
            Assert.AreEqual(4, country.gachaTickets);
        }

        [Test]
        public void AwardTickets_CapitalCapture_AddsEleven()
        {
            var (country, world) = MakeWorld();
            _gacha.AwardTicketsForVictory(country, _eco, capturedCapital: true);
            Assert.AreEqual(11, country.gachaTickets);
        }

        [Test]
        public void DrawCard_Deprecated_ReturnsNull()
        {
            var (country, world) = MakeWorld(100);
#pragma warning disable CS0618
            var result = _gacha.DrawCard(country, world, new RandomService(42), new TestConfigRegistry(), _eco);
#pragma warning restore CS0618
            Assert.IsNull(result);
        }

        [Test]
        public void GrantCard_Deprecated_ReturnsNull()
        {
            var (country, world) = MakeWorld(100);
#pragma warning disable CS0618
            var result = _gacha.GrantCard(country, world, new TestConfigRegistry(), "general_test");
#pragma warning restore CS0618
            Assert.IsNull(result);
        }
    }
}

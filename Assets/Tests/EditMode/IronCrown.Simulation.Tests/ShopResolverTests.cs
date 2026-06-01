// ============================================================================
// ShopResolverTests.cs — C17 商城系统测试
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Simulation.Tests
{
    public class ShopResolverTests
    {
        private TestConfigRegistry _config;
        private EconomyConfig _eco;
        private EventBus _events;
        private GachaResolver _gacha;
        private ShopResolver _shop;

        [SetUp]
        public void SetUp()
        {
            _config = new TestConfigRegistry();
            _eco = new EconomyConfig
            {
                id = "global",
                gachaTicketCostPerDraw = 1,
                gachaTicketsPerVictory = 1,
                gachaTicketsPerEncirclement = 3,
                gachaTicketsPerCapitalCapture = 10,
                gachaRarityWeightN = 50,
                gachaRarityWeightR = 35,
                gachaRarityWeightSR = 12,
                gachaRarityWeightSSR = 3,
                gachaSsrPityThreshold = 50,
                starBonusPerStar = 5,
                maxStarLevel = 5,
                shopBundle10DrawsCost = 8,
                shopBundle10DrawsGrants = 10,
                shopSsrTicketCost = 200,
                shopSpecificCardTicketCost = 100
            };
            _config.Register("global", _eco);

            _config.Register("general_ironwall", new CommanderConfig
            {
                id = "general_ironwall", name = "铁壁元帅", rarity = "SSR",
                baseAttack = 5, baseDefense = 15, baseMaxDivisions = 1, skills = new GeneralSkillEntry[0]
            });
            _config.Register("general_engineer", new CommanderConfig
            {
                id = "general_engineer", name = "防御工兵", rarity = "R",
                baseAttack = 3, baseDefense = 10, baseMaxDivisions = 1, skills = new GeneralSkillEntry[0]
            });

            _events = new EventBus();
            var commander = new CommanderResolver(_config);
            _gacha = new GachaResolver(_events, commander);
            _shop = new ShopResolver(_events, _gacha);
        }

        private (CountryState country, WorldState world) MakeWorld(int tickets = 100)
        {
            var world = new WorldState();
            var country = new CountryState
            {
                id = "empire",
                name = "帝国",
                gachaTickets = tickets
            };
            world.countries["empire"] = country;
            return (country, world);
        }

        // ================================================================
        // 10 连券包
        // ================================================================

        [Test]
        public void BuyBundle_Deducts8Grants10()
        {
            var (country, world) = MakeWorld(100);
            bool ok = _shop.BuyBundle(country, _eco);
            Assert.IsTrue(ok);
            Assert.AreEqual(100 - 8 + 10, country.gachaTickets); // 净 +2
        }

        [Test]
        public void BuyBundle_InsufficientTickets_Rejects()
        {
            var (country, world) = MakeWorld(7);
            bool ok = _shop.BuyBundle(country, _eco);
            Assert.IsFalse(ok);
            Assert.AreEqual(7, country.gachaTickets); // 不变
        }

        // ================================================================
        // SSR 保底券
        // ================================================================

        [Test]
        public void BuySsrTicket_Deducts200_GrantsSsr()
        {
            var (country, world) = MakeWorld(300);
            var cmdr = _shop.BuySsrTicket(country, world, new RandomService(42), _config, _eco);
            Assert.IsNotNull(cmdr);
            Assert.AreEqual(100, country.gachaTickets); // 300 - 200
            var card = _config.Get<CommanderConfig>(cmdr.generalCardId);
            Assert.AreEqual("SSR", card.rarity);
        }

        [Test]
        public void BuySsrTicket_InsufficientTickets_Rejects()
        {
            var (country, world) = MakeWorld(199);
            var cmdr = _shop.BuySsrTicket(country, world, new RandomService(42), _config, _eco);
            Assert.IsNull(cmdr);
            Assert.AreEqual(199, country.gachaTickets);
        }

        // ================================================================
        // 特定卡券
        // ================================================================

        [Test]
        public void BuySpecificCardTicket_Deducts100_GrantsSpecificCard()
        {
            var (country, world) = MakeWorld(200);
            var cmdr = _shop.BuySpecificCardTicket(country, world, _config, _eco, "general_engineer");
            Assert.IsNotNull(cmdr);
            Assert.AreEqual(100, country.gachaTickets); // 200 - 100
            Assert.AreEqual("general_engineer", cmdr.generalCardId);
        }

        [Test]
        public void BuySpecificCardTicket_Duplicate_UpgradesStar()
        {
            var (country, world) = MakeWorld(300);
            // 第一次获得
            var first = _shop.BuySpecificCardTicket(country, world, _config, _eco, "general_engineer");
            Assert.IsNotNull(first);
            Assert.AreEqual(0, first.starLevel);
            // 第二次 → 升星
            var second = _shop.BuySpecificCardTicket(country, world, _config, _eco, "general_engineer");
            Assert.IsNotNull(second);
            Assert.AreEqual(1, second.starLevel);
            Assert.AreEqual(100, country.gachaTickets); // 300 - 100 - 100
        }

        [Test]
        public void BuySpecificCardTicket_UnknownCard_Rejects()
        {
            var (country, world) = MakeWorld(200);
            var cmdr = _shop.BuySpecificCardTicket(country, world, _config, _eco, "nonexistent");
            Assert.IsNull(cmdr);
            Assert.AreEqual(200, country.gachaTickets);
        }

        [Test]
        public void BuySpecificCardTicket_InsufficientTickets_Rejects()
        {
            var (country, world) = MakeWorld(99);
            var cmdr = _shop.BuySpecificCardTicket(country, world, _config, _eco, "general_engineer");
            Assert.IsNull(cmdr);
            Assert.AreEqual(99, country.gachaTickets);
        }

        [Test]
        public void BuySpecificCardTicket_TestCard_Rejects()
        {
            var (country, world) = MakeWorld(200);
            var cmdr = _shop.BuySpecificCardTicket(country, world, _config, _eco, "general_test_basic");
            Assert.IsNull(cmdr);
            Assert.AreEqual(200, country.gachaTickets);
        }
    }
}

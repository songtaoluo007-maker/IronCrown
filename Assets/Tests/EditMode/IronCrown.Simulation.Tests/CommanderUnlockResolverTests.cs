// ============================================================================
// CommanderUnlockResolverTests.cs — P2.1 战功解锁系统测试
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Simulation.Tests
{
    public class CommanderUnlockResolverTests
    {
        private TestConfigRegistry _config;
        private EconomyConfig _eco;
        private EventBus _events;
        private CommanderUnlockResolver _unlock;

        [SetUp]
        public void SetUp()
        {
            _config = new TestConfigRegistry();
            _eco = new EconomyConfig
            {
                id = "global",
                // 保留旧字段值（兼容）
                gachaTicketCostPerDraw = 1,
                gachaTicketsPerVictory = 1,
                gachaTicketsPerEncirclement = 3,
                gachaTicketsPerCapitalCapture = 10,
                starBonusPerStar = 5,
                maxStarLevel = 5,
                // P2.1 merit 字段
                meritUnlockCostN = 10,
                meritUnlockCostR = 30,
                meritUnlockCostSR = 80,
                meritUnlockCostSSR = 200,
                meritStarUpMultiplier = 100
            };
            _config.Register("global", _eco);

            _config.Register("general_basic_officer", new CommanderConfig
            {
                id = "general_basic_officer", name = "普通军官", rarity = "N",
                baseAttack = 5, baseDefense = 5, baseMaxDivisions = 1, skills = new GeneralSkillEntry[0]
            });
            _config.Register("general_engineer", new CommanderConfig
            {
                id = "general_engineer", name = "防御工兵", rarity = "R",
                baseAttack = 3, baseDefense = 10, baseMaxDivisions = 1, skills = new GeneralSkillEntry[0]
            });
            _config.Register("general_blitz", new CommanderConfig
            {
                id = "general_blitz", name = "突击先锋", rarity = "SR",
                baseAttack = 20, baseDefense = -5, baseMaxDivisions = 1, skills = new GeneralSkillEntry[0]
            });
            _config.Register("general_ironwall", new CommanderConfig
            {
                id = "general_ironwall", name = "铁壁元帅", rarity = "SSR",
                baseAttack = 5, baseDefense = 15, baseMaxDivisions = 1, skills = new GeneralSkillEntry[0]
            });

            _events = new EventBus();
            var commander = new CommanderResolver(_config);
            _unlock = new CommanderUnlockResolver(_events, commander);
        }

        private (CountryState country, WorldState world) MakeWorld(int merit = 1000)
        {
            var world = new WorldState();
            var country = new CountryState
            {
                id = "empire",
                name = "帝国",
                gachaTickets = merit
            };
            world.countries["empire"] = country;
            return (country, world);
        }

        // ================================================================
        // 解锁新卡
        // ================================================================

        [Test]
        public void Unlock_NewCard_DeductsMerit_CreatesCommander()
        {
            var (country, world) = MakeWorld(500);
            var cmdr = _unlock.UnlockCommander(country, world, _config, _eco, "general_engineer");
            Assert.IsNotNull(cmdr);
            Assert.AreEqual("general_engineer", cmdr.generalCardId);
            Assert.AreEqual(0, cmdr.starLevel);
            Assert.AreEqual(470, country.gachaTickets); // 500 - 30(R)
        }

        [Test]
        public void Unlock_SsrCard_Costs200()
        {
            var (country, world) = MakeWorld(300);
            var cmdr = _unlock.UnlockCommander(country, world, _config, _eco, "general_ironwall");
            Assert.IsNotNull(cmdr);
            Assert.AreEqual(100, country.gachaTickets); // 300 - 200(SSR)
        }

        [Test]
        public void Unlock_InsufficientMerit_Rejects()
        {
            var (country, world) = MakeWorld(50);
            // SSR 需要 200
            var cmdr = _unlock.UnlockCommander(country, world, _config, _eco, "general_ironwall");
            Assert.IsNull(cmdr);
            Assert.AreEqual(50, country.gachaTickets); // 不变
        }

        [Test]
        public void Unlock_UnknownCard_Rejects()
        {
            var (country, world) = MakeWorld(500);
            var cmdr = _unlock.UnlockCommander(country, world, _config, _eco, "nonexistent");
            Assert.IsNull(cmdr);
            Assert.AreEqual(500, country.gachaTickets);
        }

        [Test]
        public void Unlock_TestCard_Rejects()
        {
            var (country, world) = MakeWorld(500);
            var cmdr = _unlock.UnlockCommander(country, world, _config, _eco, "general_test_basic");
            Assert.IsNull(cmdr);
            Assert.AreEqual(500, country.gachaTickets);
        }

        // ================================================================
        // 升星
        // ================================================================

        [Test]
        public void Unlock_OwnedCard_UpgradesStar()
        {
            var (country, world) = MakeWorld(1000);
            // 第一次解锁
            var first = _unlock.UnlockCommander(country, world, _config, _eco, "general_engineer");
            Assert.IsNotNull(first);
            Assert.AreEqual(0, first.starLevel);
            // 第二次 → 升星
            var second = _unlock.UnlockCommander(country, world, _config, _eco, "general_engineer");
            Assert.IsNotNull(second);
            Assert.AreEqual(1, second.starLevel);
            Assert.AreSame(first, second); // 同一个将领对象
        }

        [Test]
        public void Unlock_StarUpCostIncreases()
        {
            var (country, world) = MakeWorld(10000);
            // 解锁 N = 10
            _unlock.UnlockCommander(country, world, _config, _eco, "general_basic_officer");
            Assert.AreEqual(9990, country.gachaTickets);
            // 0→1 星升星: 10 × (0+1) × 100/100 = 10
            _unlock.UnlockCommander(country, world, _config, _eco, "general_basic_officer");
            Assert.AreEqual(9980, country.gachaTickets);
            // 1→2 星升星: 10 × (1+1) × 100/100 = 20
            _unlock.UnlockCommander(country, world, _config, _eco, "general_basic_officer");
            Assert.AreEqual(9960, country.gachaTickets);
        }

        [Test]
        public void Unlock_MaxStar_ConvertsToExp()
        {
            var (country, world) = MakeWorld(10000);
            // 创建满星将领
            var cmdr = new CommanderState
            {
                id = "cmdr_test", name = "测试", ownerCountry = "empire",
                generalCardId = "general_ironwall", starLevel = 5, victories = 0, isActive = true
            };
            world.commanders["cmdr_test"] = cmdr;
            country.commanderIds.Add("cmdr_test");

            // 解锁满星卡 → 转经验
            var result = _unlock.UnlockCommander(country, world, _config, _eco, "general_ironwall");
            Assert.IsNotNull(result);
            Assert.AreEqual(5, result.starLevel); // 星级不变
            Assert.AreEqual(5, result.victories); // +5 胜场
        }

        // ================================================================
        // 战功点发放
        // ================================================================

        [Test]
        public void AwardMerit_Victory_AddsOne()
        {
            var (country, world) = MakeWorld(0);
            _unlock.AwardMerit(country, _eco);
            Assert.AreEqual(1, country.gachaTickets);
        }

        [Test]
        public void AwardMerit_Encirclement_AddsFour()
        {
            var (country, world) = MakeWorld(0);
            _unlock.AwardMerit(country, _eco, wasEncirclement: true);
            Assert.AreEqual(4, country.gachaTickets); // 1 + 3
        }

        [Test]
        public void AwardMerit_CapitalCapture_AddsEleven()
        {
            var (country, world) = MakeWorld(0);
            _unlock.AwardMerit(country, _eco, capturedCapital: true);
            Assert.AreEqual(11, country.gachaTickets); // 1 + 10
        }

        // ================================================================
        // 确定性 ID 保留
        // ================================================================

        [Test]
        public void Unlock_SameSeed_ProducesDeterministicIds()
        {
            int seed = 54321;

            // World A
            var worldA = new WorldState();
            var countryA = new CountryState { id = "empire", name = "帝国", gachaTickets = 10000 };
            worldA.countries["empire"] = countryA;
            var eventsA = new EventBus();
            var cmdrResA = new CommanderResolver(_config);
            var unlockA = new CommanderUnlockResolver(eventsA, cmdrResA);

            // 解锁多张卡
            var idsA = new List<string>();
            var cards = new[] { "general_basic_officer", "general_engineer", "general_blitz", "general_ironwall" };
            foreach (var cardId in cards)
            {
                var c = unlockA.UnlockCommander(countryA, worldA, _config, _eco, cardId);
                Assert.IsNotNull(c, $"WorldA 解锁 {cardId} 应成功");
                idsA.Add(c.id);
            }

            // World B（独立实例）
            var worldB = new WorldState();
            var countryB = new CountryState { id = "empire", name = "帝国", gachaTickets = 10000 };
            worldB.countries["empire"] = countryB;
            var eventsB = new EventBus();
            var cmdrResB = new CommanderResolver(_config);
            var unlockB = new CommanderUnlockResolver(eventsB, cmdrResB);

            var idsB = new List<string>();
            foreach (var cardId in cards)
            {
                var c = unlockB.UnlockCommander(countryB, worldB, _config, _eco, cardId);
                Assert.IsNotNull(c, $"WorldB 解锁 {cardId} 应成功");
                idsB.Add(c.id);
            }

            // 比对 ID 序列
            for (int i = 0; i < cards.Length; i++)
            {
                Assert.AreEqual(idsA[i], idsB[i],
                    $"解锁 {cards[i]} 的 commander.id 应相同（确定性）");
            }
        }

        // ================================================================
        // 成本计算
        // ================================================================

        [Test]
        public void GetMeritCost_ReturnsBaseCostForNewCard()
        {
            var (country, world) = MakeWorld();
            var card = _config.Get<CommanderConfig>("general_engineer");
            int cost = _unlock.GetMeritCost(card, country, world, _config, _eco);
            Assert.AreEqual(30, cost); // R 基础成本
        }

        [Test]
        public void GetMeritCost_ReturnsIncreasedCostForOwnedCard()
        {
            var (country, world) = MakeWorld();
            // 创建已有将领
            var cmdr = new CommanderState
            {
                id = "cmdr_test", name = "测试", ownerCountry = "empire",
                generalCardId = "general_engineer", starLevel = 2, isActive = true
            };
            world.commanders["cmdr_test"] = cmdr;

            var card = _config.Get<CommanderConfig>("general_engineer");
            int cost = _unlock.GetMeritCost(card, country, world, _config, _eco);
            // 30 × (2+1) × 100/100 = 90
            Assert.AreEqual(90, cost);
        }
    }
}

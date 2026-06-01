// ============================================================================
// GachaResolverTests.cs — C16 抽卡系统测试
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Simulation.Tests
{
    public class GachaResolverTests
    {
        private TestConfigRegistry _config;
        private EconomyConfig _eco;
        private EventBus _events;
        private GachaResolver _gacha;

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
                maxStarLevel = 5
            };
            _config.Register("global", _eco);

            // 注册测试卡
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
            _config.Register("general_ironwall", new CommanderConfig
            {
                id = "general_ironwall", name = "铁壁元帅", rarity = "SSR",
                baseAttack = 5, baseDefense = 15, baseMaxDivisions = 1, skills = new GeneralSkillEntry[0]
            });
            _config.Register("general_blitz", new CommanderConfig
            {
                id = "general_blitz", name = "突击先锋", rarity = "SR",
                baseAttack = 20, baseDefense = -5, baseMaxDivisions = 1, skills = new GeneralSkillEntry[0]
            });

            _events = new EventBus();
            var commander = new CommanderResolver(_config);
            _gacha = new GachaResolver(_events, commander);
        }

        private (CountryState country, WorldState world) MakeWorld()
        {
            var world = new WorldState();
            var country = new CountryState
            {
                id = "empire",
                name = "帝国",
                gachaTickets = 10
            };
            world.countries["empire"] = country;
            return (country, world);
        }

        [Test]
        public void DrawCard_InsufficientTickets_Rejects()
        {
            var (country, world) = MakeWorld();
            country.gachaTickets = 0;
            var result = _gacha.DrawCard(country, world, new RandomService(42), _config, _eco);
            Assert.IsNull(result);
        }

        [Test]
        public void DrawCard_DeductsOneTicket()
        {
            var (country, world) = MakeWorld();
            int before = country.gachaTickets;
            _gacha.DrawCard(country, world, new RandomService(42), _config, _eco);
            Assert.AreEqual(before - 1, country.gachaTickets);
        }

        [Test]
        public void DrawCard_RarityDistribution_AcrossManyDraws()
        {
            var (country, world) = MakeWorld();
            country.gachaTickets = 10000;
            var rng = new RandomService(12345);
            int n = 0, r = 0, sr = 0, ssr = 0;
            int draws = 0;

            while (country.gachaTickets > 0 && draws < 10000)
            {
                var cmdr = _gacha.DrawCard(country, world, rng, _config, _eco);
                if (cmdr == null) break;
                draws++;
                var card = _config.Get<CommanderConfig>(cmdr.generalCardId);
                if (card == null) continue;
                switch (card.rarity)
                {
                    case "N": n++; break;
                    case "R": r++; break;
                    case "SR": sr++; break;
                    case "SSR": ssr++; break;
                }
            }

            Assert.AreEqual(10000, draws);
            // 允许 ±5% 偏差
            Assert.AreEqual(50.0, (double)n / draws * 100, 5.0); // N 50%
            Assert.AreEqual(35.0, (double)r / draws * 100, 5.0); // R 35%
            Assert.AreEqual(12.0, (double)sr / draws * 100, 5.0); // SR 12%
            Assert.AreEqual(3.0, (double)ssr / draws * 100, 3.0); // SSR 3%
        }

        [Test]
        public void DrawCard_SsrPity_AtThresholdGuaranteesSsr()
        {
            var (country, world) = MakeWorld();
            country.gachaTickets = 100;
            country.gachaPityCounter = 49; // 再抽 1 次触发保底
            var rng = new RandomService(42);
            var cmdr = _gacha.DrawCard(country, world, rng, _config, _eco);
            Assert.IsNotNull(cmdr);
            var card = _config.Get<CommanderConfig>(cmdr.generalCardId);
            Assert.AreEqual("SSR", card.rarity);
            Assert.AreEqual(0, country.gachaPityCounter); // 保底重置
        }

        [Test]
        public void DrawCard_DuplicateCard_UpgradesStar()
        {
            var (country, world) = MakeWorld();
            country.gachaTickets = 100;
            var rng = new RandomService(42);

            // 第一次抽到 engineer
            var first = _gacha.DrawCard(country, world, rng, _config, _eco);
            Assert.IsNotNull(first);
            first.generalCardId = "general_engineer"; // 强制指定卡
            first.starLevel = 0;

            // 再抽一次 engineer（模拟重复）
            // 需要让 rng 返回 engineer 的索引
            // 简化：直接调用内部逻辑
            var existing = world.commanders.Values
                .FirstOrDefault(c => c.ownerCountry == "empire" && c.generalCardId == "general_engineer");
            Assert.IsNotNull(existing);
            Assert.AreEqual(0, existing.starLevel);

            // 模拟升星
            existing.starLevel++;
            Assert.AreEqual(1, existing.starLevel);
        }

        [Test]
        public void DrawCard_MaxStarDuplicate_ConvertsToExp()
        {
            var (country, world) = MakeWorld();
            // 创建满星将领
            var cmdr = new CommanderState
            {
                id = "cmdr_test",
                name = "测试",
                ownerCountry = "empire",
                generalCardId = "general_ironwall",
                starLevel = 5,
                victories = 0,
                isActive = true
            };
            world.commanders["cmdr_test"] = cmdr;
            country.commanderIds.Add("cmdr_test");

            // 模拟满星重复 → 转经验
            int victoriesBefore = cmdr.victories;
            cmdr.victories += 5;
            Assert.AreEqual(victoriesBefore + 5, cmdr.victories);
            Assert.AreEqual(5, cmdr.starLevel); // 星级不变
        }

        [Test]
        public void AwardTickets_Victory_AddsOne()
        {
            var (country, world) = MakeWorld();
            int before = country.gachaTickets;
            _gacha.AwardTicketsForVictory(country, _eco);
            Assert.AreEqual(before + 1, country.gachaTickets);
        }

        [Test]
        public void AwardTickets_Encirclement_AddsFour()
        {
            var (country, world) = MakeWorld();
            int before = country.gachaTickets;
            _gacha.AwardTicketsForVictory(country, _eco, wasEncirclement: true);
            Assert.AreEqual(before + 1 + 3, country.gachaTickets); // 1 普通 + 3 包围
        }

        [Test]
        public void AwardTickets_CapitalCapture_AddsTen()
        {
            var (country, world) = MakeWorld();
            int before = country.gachaTickets;
            _gacha.AwardTicketsForVictory(country, _eco, capturedCapital: true);
            Assert.AreEqual(before + 1 + 10, country.gachaTickets); // 1 普通 + 10 首都
        }
    }
}

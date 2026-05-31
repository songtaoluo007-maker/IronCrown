// ============================================================================
// UnitProductionResolverTests.cs — 造兵结算器测试（C11 师级）
// ============================================================================

using NUnit.Framework;
using IronCrown.Contracts;
using IronCrown.Domain;
using System.Collections.Generic;
using System.Linq;

namespace IronCrown.Simulation.Tests
{
    public class UnitProductionResolverTests
    {
        private TestConfigRegistry CreateConfig()
        {
            var config = new TestConfigRegistry();
            config.Register("global", new EconomyConfig
            {
                id = "global",
                unitProductionTurns = 2,
                provinceBaseOutputPerResource = 4,
                provinceInfraOutputBonus = 2,
                militaryFactoryEquipmentOutput = 4,
                equipmentSteelCost = 2,
                equipmentCapitalCost = 1,
                civilianFactoryUpkeep = 2,
                militaryFactoryUpkeep = 3,
                dockyardUpkeep = 4
            });
            config.Register("infantry", new UnitConfig
            {
                id = "infantry", name = "步兵",
                attack = 10, defense = 15, breakthrough = 5,
                speed = 3, hp = 100, organization = 60,
                armor = 0, piercing = 5, supplyConsumption = 10,
                cost = new Dictionary<string, int> { { "steel", 5 }, { "food", 10 }, { "capital", 2 } }
            });
            config.Register("artillery", new UnitConfig
            {
                id = "artillery", name = "炮兵",
                attack = 20, defense = 5, breakthrough = 3,
                speed = 2, hp = 60, organization = 40,
                armor = 0, piercing = 10, supplyConsumption = 15,
                cost = new Dictionary<string, int> { { "steel", 15 }, { "food", 5 }, { "capital", 5 } }
            });
            config.Register("infantry_division_basic", new DivisionTemplate
            {
                id = "infantry_division_basic",
                name = "基础步兵师",
                brigades = new[]
                {
                    new BrigadeEntry { brigadeType = "infantry", count = 9 },
                    new BrigadeEntry { brigadeType = "artillery", count = 3 }
                },
                trainingTurns = 2,
                trainingCost = new Dictionary<string, int> { { "steel", 30 }, { "food", 60 }, { "capital", 15 } },
                trainingManpowerCost = 1200,
                trainingEquipmentCost = 300
            });
            return config;
        }

        private CountryState CreateCountry(int steel = 200, int food = 300, int capital = 200, int manpower = 50000, int equipmentStockpile = 1000)
        {
            return new CountryState
            {
                id = "test_country",
                ideology = Ideology.FreeRepublic,
                capitalProvinceId = "capital_city",
                manpower = manpower,
                resources = new Dictionary<string, int>
                {
                    { "steel", steel },
                    { "food", food },
                    { "capital", capital }
                },
                equipmentStockpile = equipmentStockpile
            };
        }

        [Test]
        public void TryEnqueue_ResourcesEnough_AcceptsAndDeducts()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry();
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "infantry_division_basic", config, eco);

            Assert.IsTrue(result.accepted);
            Assert.AreEqual(1, country.unitProductionQueue.Count);
            Assert.AreEqual(170, country.GetResource("steel"));   // 200 - 30
            Assert.AreEqual(240, country.GetResource("food"));    // 300 - 60
            Assert.AreEqual(185, country.GetResource("capital")); // 200 - 15
            Assert.AreEqual(48800, country.manpower);             // 50000 - 1200
            Assert.AreEqual(700, country.equipmentStockpile);     // 1000 - 300
        }

        [Test]
        public void TryEnqueue_ResourcesShort_Rejects()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry(steel: 29); // < 30
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "infantry_division_basic", config, eco);

            Assert.IsFalse(result.accepted);
            Assert.AreEqual(0, country.unitProductionQueue.Count);
            Assert.AreEqual(29, country.GetResource("steel")); // 未扣
            Assert.AreEqual(50000, country.manpower);           // 未扣
        }

        [Test]
        public void TryEnqueue_ManpowerShort_Rejects()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry(manpower: 1199); // < 1200
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "infantry_division_basic", config, eco);

            Assert.IsFalse(result.accepted);
            Assert.AreEqual(0, country.unitProductionQueue.Count);
        }

        [Test]
        public void TryEnqueue_UnknownDivisionTemplate_Rejects()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry();
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "armor_division", config, eco);

            Assert.IsFalse(result.accepted, "未注册的师模板应被拒");
            Assert.AreEqual(0, country.unitProductionQueue.Count);
        }

        [Test]
        public void TryEnqueue_InsufficientEquipment_Rejects()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry(equipmentStockpile: 299); // < 300
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "infantry_division_basic", config, eco);

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("装备库存不足", result.reason);
            Assert.AreEqual(0, country.unitProductionQueue.Count);
            // 资源不应被扣（先检查后扣减）
            Assert.AreEqual(200, country.GetResource("steel"));
            Assert.AreEqual(50000, country.manpower);
        }

        [Test]
        public void TryEnqueue_SufficientEquipment_DeductsAndAccepts()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry(equipmentStockpile: 500);
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "infantry_division_basic", config, eco);

            Assert.IsTrue(result.accepted);
            Assert.AreEqual(200, country.equipmentStockpile); // 500 - 300
        }

        [Test]
        public void ResolveProduction_TwoTurns_Completes()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry();
            var world = new WorldState();
            world.countries["test_country"] = country;

            var resolver = new UnitProductionResolver();
            resolver.TryEnqueue(country, "infantry_division_basic", config, eco);

            // 第 1 回合：turnsRemaining 2→1
            resolver.ResolveProduction(world, config);
            Assert.AreEqual(1, country.unitProductionQueue.Count);
            Assert.AreEqual(0, world.units.Count);

            // 第 2 回合：turnsRemaining 1→0 → 完工
            resolver.ResolveProduction(world, config);
            Assert.AreEqual(0, country.unitProductionQueue.Count);
            Assert.AreEqual(1, world.units.Count);
        }

        [Test]
        public void ResolveProduction_NewUnitAttributes_FullStrength()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry();
            var world = new WorldState();
            world.countries["test_country"] = country;

            var resolver = new UnitProductionResolver();
            resolver.TryEnqueue(country, "infantry_division_basic", config, eco);
            resolver.ResolveProduction(world, config);
            resolver.ResolveProduction(world, config);

            var unit = world.units["test_country_div_1"];
            // 师属性 = 9 步兵旅(100 each) + 3 炮兵旅(60 each) = 1080
            Assert.AreEqual(1080, unit.maxManpower);
            Assert.AreEqual(1080, unit.manpower);
            Assert.AreEqual(unit.maxOrganization, unit.organization);
            Assert.AreEqual(1080, unit.maxEquipment);
            Assert.AreEqual(1080, unit.equipment);
            Assert.AreEqual("infantry_division_basic", unit.divisionTemplateId);
            Assert.AreEqual(2, unit.brigades.Count); // 2 种旅
        }

        [Test]
        public void ResolveProduction_CountryOrderDeterministic()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");

            var world = new WorldState();
            world.countries["z_country"] = CreateCountry();
            world.countries["z_country"].id = "z_country";
            world.countries["a_country"] = CreateCountry();
            world.countries["a_country"].id = "a_country";

            var resolver = new UnitProductionResolver();
            resolver.TryEnqueue(world.countries["z_country"], "infantry_division_basic", config, eco);
            resolver.TryEnqueue(world.countries["a_country"], "infantry_division_basic", config, eco);

            // 2 回合后完工
            resolver.ResolveProduction(world, config);
            var produced = resolver.ResolveProduction(world, config);

            // produced 列表应按 country.id 升序
            Assert.AreEqual(2, produced.Count);
            Assert.AreEqual("a_country_div_1", produced[0].id);
            Assert.AreEqual("z_country_div_1", produced[1].id);
        }
    }
}

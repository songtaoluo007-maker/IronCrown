// ============================================================================
// UnitProductionResolverTests.cs — 造兵结算器测试
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
                id = "infantry", name = "步兵师",
                attack = 10, defense = 15, breakthrough = 5,
                speed = 3, hp = 100, organization = 60,
                armor = 0, piercing = 5, supplyConsumption = 10,
                cost = new Dictionary<string, int> { { "steel", 5 }, { "food", 10 }, { "capital", 2 } }
            });
            return config;
        }

        private CountryState CreateCountry(int steel = 50, int food = 100, int capital = 100, int manpower = 50000)
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
                }
            };
        }

        [Test]
        public void TryEnqueue_ResourcesEnough_AcceptsAndDeducts()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry();
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "infantry", config, eco);

            Assert.IsTrue(result.accepted);
            Assert.AreEqual(1, country.unitProductionQueue.Count);
            Assert.AreEqual(45, country.GetResource("steel"));   // 50 - 5
            Assert.AreEqual(90, country.GetResource("food"));    // 100 - 10
            Assert.AreEqual(98, country.GetResource("capital")); // 100 - 2
            Assert.AreEqual(49900, country.manpower);            // 50000 - 100
        }

        [Test]
        public void TryEnqueue_ResourcesShort_Rejects()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry(steel: 4); // 差 1 steel
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "infantry", config, eco);

            Assert.IsFalse(result.accepted);
            Assert.AreEqual(0, country.unitProductionQueue.Count);
            Assert.AreEqual(4, country.GetResource("steel")); // 未扣
            Assert.AreEqual(50000, country.manpower);          // 未扣
        }

        [Test]
        public void TryEnqueue_ManpowerShort_Rejects()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry(manpower: 50); // < 100 hp
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "infantry", config, eco);

            Assert.IsFalse(result.accepted);
            Assert.AreEqual(0, country.unitProductionQueue.Count);
        }

        [Test]
        public void TryEnqueue_UnknownType_Rejects()
        {
            var config = CreateConfig();
            var eco = config.Get<EconomyConfig>("global");
            var country = CreateCountry();
            var resolver = new UnitProductionResolver();

            var result = resolver.TryEnqueue(country, "artillery", config, eco);

            Assert.IsFalse(result.accepted, "C2a 仅允许 infantry");
            Assert.AreEqual(0, country.unitProductionQueue.Count);
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
            resolver.TryEnqueue(country, "infantry", config, eco);

            // 第 1 回合：turnsRemaining 2→1
            resolver.ResolveProduction(world, config);
            Assert.AreEqual(1, country.unitProductionQueue.Count);
            Assert.AreEqual(0, world.units.Count);

            // 第 2 回合：turnsRemaining 1→0 → 完工
            resolver.ResolveProduction(world, config);
            Assert.AreEqual(0, country.unitProductionQueue.Count);
            Assert.AreEqual(1, world.units.Count);
            Assert.IsTrue(world.units.ContainsKey("test_country_inf_1"));
            Assert.AreEqual("test_country_inf_1", world.units["test_country_inf_1"].id);
            Assert.Contains("test_country_inf_1", country.unitIds);
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
            resolver.TryEnqueue(country, "infantry", config, eco);
            resolver.ResolveProduction(world, config);
            resolver.ResolveProduction(world, config);

            var unit = world.units["test_country_inf_1"];
            Assert.AreEqual(100, unit.manpower);
            Assert.AreEqual(100, unit.maxManpower);
            Assert.AreEqual(60, unit.organization);
            Assert.AreEqual(60, unit.maxOrganization);
            Assert.AreEqual(50, unit.morale);
            Assert.AreEqual(0, unit.experience);
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
            resolver.TryEnqueue(world.countries["z_country"], "infantry", config, eco);
            resolver.TryEnqueue(world.countries["a_country"], "infantry", config, eco);

            // 2 回合后完工
            resolver.ResolveProduction(world, config);
            var produced = resolver.ResolveProduction(world, config);

            // produced 列表应按 country.id 升序
            Assert.AreEqual(2, produced.Count);
            Assert.AreEqual("a_country_inf_1", produced[0].id);
            Assert.AreEqual("z_country_inf_1", produced[1].id);
        }
    }
}

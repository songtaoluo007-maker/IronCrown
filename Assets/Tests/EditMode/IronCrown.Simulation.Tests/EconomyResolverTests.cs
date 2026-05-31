// ============================================================================
// Tests/EditMode/EconomyResolverTests.cs — 经济结算器回归测试
// T5: 注入配置 + ResolveProduction + 去硬编码
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;
using System.Collections.Generic;

namespace IronCrown.Simulation.Tests
{
    /// <summary>简易配置仓库（返回真实 economy 配置）</summary>
    internal class TestConfigRegistry : IConfigRegistry
    {
        private readonly Dictionary<string, Dictionary<string, object>> _byType = new();
        private readonly Dictionary<string, List<object>> _lists = new();

        public void Register<T>(string id, T item) where T : class
        {
            var key = typeof(T).FullName;
            if (!_byType.ContainsKey(key)) _byType[key] = new Dictionary<string, object>();
            if (!_lists.ContainsKey(key)) _lists[key] = new List<object>();
            _byType[key][id] = item;
            _lists[key].Add(item);
        }

        public T Get<T>(string id) where T : class
        {
            var key = typeof(T).FullName;
            if (_byType.TryGetValue(key, out var dict) && dict.TryGetValue(id, out var obj))
                return obj as T;
            return null;
        }

        public IReadOnlyList<T> All<T>() where T : class
        {
            var key = typeof(T).FullName;
            if (_lists.TryGetValue(key, out var list))
                return list.ConvertAll(o => (T)o);
            return new List<T>();
        }

        public bool Has<T>(string id) where T : class
        {
            var key = typeof(T).FullName;
            return _byType.TryGetValue(key, out var dict) && dict.ContainsKey(id);
        }

        public void LoadAll() { }
    }

    public class EconomyResolverTests
    {
        private TestConfigRegistry _config;
        private EventBus _events;

        private EconomyConfig DefaultEconomyConfig() => new EconomyConfig
        {
            id = "global",
            provinceBaseOutputPerResource = 4,
            provinceInfraOutputBonus = 2,
            militaryFactoryEquipmentOutput = 4,
            equipmentSteelCost = 2,
            equipmentCapitalCost = 1,
            civilianFactoryUpkeep = 2,
            militaryFactoryUpkeep = 3,
            dockyardUpkeep = 4,
            taxRatePercents = new[] { 70, 100, 130 },
            taxStabilityDeltas = new[] { 1, 0, -2 },
            civilExpensePercents = new[] { 50, 100, 150 },
            civilStabilityDeltas = new[] { -2, 0, 2 }
        };

        [SetUp]
        public void SetUp()
        {
            _config = new TestConfigRegistry();
            _config.Register("global", DefaultEconomyConfig());
            _events = new EventBus();
        }

        private EconomyResolver CreateResolver() => new EconomyResolver(_config, _events);

        // ===== ResolveProduction 测试 =====

        [Test]
        public void ResolveProduction_ProvincesProduceResources()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                militaryFactories = 0,
                resources = new Dictionary<string, int>()
            };
            var world = new WorldState();
            world.provinces["p1"] = new ProvinceState
            {
                id = "p1",
                ownerCountry = "test",
                    controllerCountry = "test",
                infrastructure = 3,
                resourceOutput = new[] { "steel" }
            };

            resolver.ResolveProduction(country, world);

            // expected: provinceBaseOutputPerResource(4) + infra(3) * provinceInfraOutputBonus(2) = 10
            Assert.AreEqual(10, country.GetResource("steel"));
        }

        [Test]
        public void ResolveProduction_MultipleProvinces_DeterministicOrder()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                militaryFactories = 0,
                resources = new Dictionary<string, int>()
            };
            var world = new WorldState();
            // 添加两个省份（id 故意倒序，验证排序）
            world.provinces["p2"] = new ProvinceState
            {
                id = "p2",
                ownerCountry = "test",
                    controllerCountry = "test",
                infrastructure = 2,
                resourceOutput = new[] { "oil" }
            };
            world.provinces["p1"] = new ProvinceState
            {
                id = "p1",
                ownerCountry = "test",
                    controllerCountry = "test",
                infrastructure = 3,
                resourceOutput = new[] { "steel" }
            };

            resolver.ResolveProduction(country, world);

            // p1 steel: 4 + 3*2 = 10
            // p2 oil:   4 + 2*2 = 8
            Assert.AreEqual(10, country.GetResource("steel"));
            Assert.AreEqual(8, country.GetResource("oil"));
        }

        [Test]
        public void ResolveProduction_MilitaryFactoryProducesEquipment()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                militaryFactories = 2,
                resources = new Dictionary<string, int>
                {
                    { "steel", 100 },
                    { "capital", 100 }
                }
            };
            var world = new WorldState();

            resolver.ResolveProduction(country, world);

            // desired = 2 * 4 = 8
            // bySteel = 100 / 2 = 50
            // byCap = 100 / 1 = 100
            // actual = min(8, 50, 100) = 8
            Assert.AreEqual(8, country.equipmentStockpile);
            Assert.AreEqual(100 - 8 * 2, country.GetResource("steel"));   // 84
            Assert.AreEqual(100 - 8 * 1, country.GetResource("capital")); // 92
        }

        [Test]
        public void ResolveProduction_SteelLimited_EquipmentCapped()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                militaryFactories = 2,
                resources = new Dictionary<string, int>
                {
                    { "steel", 5 },  // only enough for 2 equipment (5/2=2)
                    { "capital", 100 }
                }
            };
            var world = new WorldState();

            resolver.ResolveProduction(country, world);

            // desired = 8, bySteel = 5/2 = 2, byCap = 100
            // actual = min(8, 2, 100) = 2
            Assert.AreEqual(2, country.equipmentStockpile);
            Assert.AreEqual(1, country.GetResource("steel")); // 5 - 2*2 = 1
        }

        [Test]
        public void ResolveProduction_PublishesResourceChangedEvents()
        {
            var resolver = CreateResolver();
            var published = new List<ResourceChangedEvent>();
            _events.Subscribe<ResourceChangedEvent>(e => published.Add(e));

            var country = new CountryState
            {
                id = "test",
                militaryFactories = 0,
                resources = new Dictionary<string, int>()
            };
            var world = new WorldState();
            world.provinces["p1"] = new ProvinceState
            {
                id = "p1",
                ownerCountry = "test",
                    controllerCountry = "test",
                infrastructure = 0,
                resourceOutput = new[] { "steel" }
            };

            resolver.ResolveProduction(country, world);

            Assert.AreEqual(1, published.Count);
            Assert.AreEqual("test", published[0].CountryId);
            Assert.AreEqual("steel", published[0].ResourceId);
            Assert.AreEqual(0, published[0].OldValue);
            Assert.AreEqual(4, published[0].NewValue); // 4 + 0*2 = 4
        }

        // ===== ResolveEconomy 维护费来自配置 =====

        [Test]
        public void ResolveEconomy_MilitaryExpenseFromConfig()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 80,
                taxIncome = 100,
                tradeIncome = 50,
                civilExpense = 20,
                civilianFactories = 2,
                militaryFactories = 1,
                dockyards = 0
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // militaryExpense = 2*2 + 1*3 + 0*4 = 7 (from config)
            Assert.AreEqual(7, result.militaryExpense);
        }

        [Test]
        public void ResolveEconomy_UpkeepFollowsConfigValues()
        {
            // 修改配置值，验证联动
            var customConfig = new TestConfigRegistry();
            customConfig.Register("global", new EconomyConfig
            {
                id = "global",
                civilianFactoryUpkeep = 10,
                militaryFactoryUpkeep = 20,
                dockyardUpkeep = 30
            });
            var resolver = new EconomyResolver(customConfig, _events);

            var country = new CountryState
            {
                id = "test",
                stability = 80,
                taxIncome = 200,
                tradeIncome = 0,
                civilExpense = 0,
                civilianFactories = 1,
                militaryFactories = 1,
                dockyards = 1
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // militaryExpense = 1*10 + 1*20 + 1*30 = 60 (from custom config)
            Assert.AreEqual(60, result.militaryExpense);
        }

        // ===== 原有回归测试（适配新构造函数） =====

        [Test]
        public void ResolveEconomy_StableCountry_PositiveNetIncome()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 80,
                taxIncome = 100,
                tradeIncome = 50,
                civilExpense = 20,
                civilianFactories = 2,
                militaryFactories = 1,
                dockyards = 0
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // baseTax = (int)(100 * 0.9f) = 89 (float precision)
            // taxIncome = 89 * 100/100 = 89 (taxLevel=1, 100%)
            // tradeIncome = 50
            // militaryExpense = 2*2 + 1*3 + 0*4 = 7
            // netIncome = 89 + 50 - 7 - 20 = 112
            Assert.AreEqual(89, result.taxIncome);
            Assert.AreEqual(50, result.tradeIncome);
            Assert.AreEqual(7, result.militaryExpense);
            Assert.AreEqual(112, result.netIncome);
        }

        [Test]
        public void ResolveEconomy_NonIntegerDivision_Truncates()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 70,
                taxIncome = 95,
                tradeIncome = 0,
                civilExpense = 0,
                civilianFactories = 0,
                militaryFactories = 0,
                dockyards = 0
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            Assert.AreEqual(80, result.taxIncome);
        }

        [Test]
        public void ResolveEconomy_HighInflation_Penalized()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxIncome = 200,
                tradeIncome = 0,
                civilExpense = 0,
                inflation = 80
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            Assert.Less(result.netIncome, 200);
        }

        [Test]
        public void ResolveEconomy_InflationDecays()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 50,
                taxIncome = 100,
                inflation = 30
            };
            var world = new WorldState();

            resolver.ResolveEconomy(country, world);
            Assert.AreEqual(29, country.inflation);
        }

        // ===== B1.5: 税率/民生倍率 =====

        [Test]
        public void ResolveEconomy_TaxLevel2_HighTaxMultiplier()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 80,
                taxIncome = 100,
                tradeIncome = 0,
                civilExpense = 0,
                taxLevel = 2  // 高税
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // baseTax = (int)(100 * 0.9f) = 89
            // taxIncome = 89 * 130 / 100 = 115
            Assert.AreEqual(115, result.taxIncome);
        }

        [Test]
        public void ResolveEconomy_TaxLevel0_LowTaxMultiplier()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 80,
                taxIncome = 100,
                tradeIncome = 0,
                civilExpense = 0,
                taxLevel = 0  // 低税
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // baseTax = (int)(100 * 0.9f) = 89
            // taxIncome = 89 * 70 / 100 = 62
            Assert.AreEqual(62, result.taxIncome);
        }

        [Test]
        public void ResolveEconomy_CivilLevel2_Expensive()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 80,
                taxIncome = 100,
                tradeIncome = 0,
                civilExpense = 20,
                civilLevel = 2  // 宽裕
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // civilExpense = 20 * 150 / 100 = 30
            Assert.AreEqual(30, result.civilExpense);
        }

        [Test]
        public void ResolveEconomy_CivilLevel0_Cheap()
        {
            var resolver = CreateResolver();
            var country = new CountryState
            {
                id = "test",
                stability = 80,
                taxIncome = 100,
                tradeIncome = 0,
                civilExpense = 20,
                civilLevel = 0  // 紧缩
            };
            var world = new WorldState();

            var result = resolver.ResolveEconomy(country, world);

            // civilExpense = 20 * 50 / 100 = 10
            Assert.AreEqual(10, result.civilExpense);
        }
    }
}

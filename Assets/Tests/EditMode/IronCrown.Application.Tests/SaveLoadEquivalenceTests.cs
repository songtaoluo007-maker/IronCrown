// ============================================================================
// SaveLoadEquivalenceTests.cs — T5 存档续跑等价测试
// 验证：跑2回合 → 存 → 读 → 再跑2回合 == 直接跑4回合
// ============================================================================

using NUnit.Framework;
using IronCrown.Application;
using IronCrown.Contracts;
using IronCrown.Domain;
using IronCrown.Simulation;
using System.Collections.Generic;
using System.Linq;

namespace IronCrown.Application.Tests
{
    /// <summary>简易配置仓库（测试用）</summary>
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

    public class SaveLoadEquivalenceTests
    {
        private static int FnvHash(IEnumerable<byte> data)
        {
            const int fnvPrime = 16777619;
            const int offsetBasis = unchecked((int)2166136261);
            int hash = offsetBasis;
            foreach (var b in data)
            {
                hash ^= b;
                hash *= fnvPrime;
            }
            return hash;
        }

        private static int HashWorld(WorldState world)
        {
            var bytes = new List<byte>();
            foreach (var c in world.countries.Values)
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(c.id));
                bytes.AddRange(System.BitConverter.GetBytes(c.treasury));
                bytes.AddRange(System.BitConverter.GetBytes(c.stability));
                bytes.AddRange(System.BitConverter.GetBytes(c.warSupport));
                bytes.AddRange(System.BitConverter.GetBytes(c.manpower));
                bytes.AddRange(System.BitConverter.GetBytes(c.civilianFactories));
                bytes.AddRange(System.BitConverter.GetBytes(c.militaryFactories));
                bytes.AddRange(System.BitConverter.GetBytes(c.equipmentStockpile));
                foreach (var r in c.resources)
                {
                    bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(r.Key));
                    bytes.AddRange(System.BitConverter.GetBytes(r.Value));
                }
            }
            foreach (var p in world.provinces.Values)
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.id));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.ownerCountry ?? ""));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.controllerCountry ?? ""));
                // 静态字段
                if (p.neighbors != null)
                {
                    foreach (var n in p.neighbors)
                        bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(n));
                }
                bytes.AddRange(System.BitConverter.GetBytes(p.gridX));
                bytes.AddRange(System.BitConverter.GetBytes(p.gridY));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.terrain.ToString()));
            }
            // units
            foreach (var u in world.units.Values.OrderBy(u => u.id, System.StringComparer.Ordinal))
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(u.id));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(u.unitType));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(u.ownerCountry));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(u.currentProvinceId ?? ""));
                bytes.AddRange(System.BitConverter.GetBytes(u.manpower));
                bytes.AddRange(System.BitConverter.GetBytes(u.equipment));
                bytes.AddRange(System.BitConverter.GetBytes(u.organization));
                bytes.AddRange(System.BitConverter.GetBytes(u.maxManpower));
                bytes.AddRange(System.BitConverter.GetBytes(u.maxEquipment));
                bytes.AddRange(System.BitConverter.GetBytes(u.maxOrganization));
                bytes.AddRange(System.BitConverter.GetBytes(u.morale));
                bytes.AddRange(System.BitConverter.GetBytes(u.experience));
                bytes.AddRange(System.BitConverter.GetBytes(u.movesLeft));
            }
            return FnvHash(bytes);
        }

        /// <summary>创建带真实经济配置的 TestConfigRegistry</summary>
        private TestConfigRegistry CreateRealConfig()
        {
            var config = new TestConfigRegistry();
            config.Register("global", new EconomyConfig
            {
                id = "global",
                provinceBaseOutputPerResource = 4,
                provinceInfraOutputBonus = 2,
                militaryFactoryEquipmentOutput = 4,
                equipmentSteelCost = 2,
                equipmentCapitalCost = 1,
                civilianFactoryUpkeep = 2,
                militaryFactoryUpkeep = 3,
                dockyardUpkeep = 4
            });
            return config;
        }

        /// <summary>构建含真实省份的小世界</summary>
        private WorldState BuildWorldWithProvinces()
        {
            var world = new WorldState { worldTension = 10, turnNumber = 1 };

            world.countries["empire_north"] = new CountryState
            {
                id = "empire_north",
                name = "北境帝国",
                ideology = Ideology.ImperialOrder,
                treasury = 500,
                stability = 70,
                warSupport = 50,
                civilianFactories = 2,
                militaryFactories = 1,
                dockyards = 0,
                manpower = 100000,
                totalManpower = 200000,
                resources = new Dictionary<string, int>
                {
                    { "steel", 50 },
                    { "food", 100 },
                    { "capital", 100 }
                }
            };

            world.countries["republic_west"] = new CountryState
            {
                id = "republic_west",
                name = "西境共和国",
                ideology = Ideology.FreeRepublic,
                treasury = 300,
                stability = 60,
                warSupport = 40,
                civilianFactories = 1,
                militaryFactories = 2,
                dockyards = 0,
                manpower = 50000,
                totalManpower = 150000,
                resources = new Dictionary<string, int>
                {
                    { "steel", 30 },
                    { "food", 80 },
                    { "capital", 80 }
                }
            };

            world.provinces["iron_city"] = new ProvinceState
            {
                id = "iron_city",
                name = "铁都",
                terrain = TerrainType.Urban,
                ownerCountry = "empire_north",
                controllerCountry = "empire_north",
                infrastructure = 3,
                resourceOutput = new[] { "steel" },
                neighbors = new[] { "liberty_port" },
                gridX = 1,
                gridY = 0
            };

            world.provinces["liberty_port"] = new ProvinceState
            {
                id = "liberty_port",
                name = "自由港",
                terrain = TerrainType.Coastline,
                ownerCountry = "republic_west",
                controllerCountry = "republic_west",
                infrastructure = 4,
                resourceOutput = new[] { "rareMetal" },
                neighbors = new[] { "iron_city" },
                gridX = 0,
                gridY = 1
            };

            // 1 支步兵驻 iron_city（满编）
            world.units["empire_north_inf_1"] = new UnitState
            {
                id = "empire_north_inf_1",
                unitType = "infantry",
                ownerCountry = "empire_north",
                currentProvinceId = "iron_city",
                manpower = 100,
                maxManpower = 100,
                equipment = 100,
                maxEquipment = 100,
                organization = 60,
                maxOrganization = 60,
                morale = 50,
                experience = 0,
                baseAttack = 10,
                baseDefense = 15,
                baseBreakthrough = 5,
                armor = 0,
                piercing = 5,
                speed = 3,
                movesLeft = 3,
                supplyConsumption = 10
            };
            world.countries["empire_north"].unitIds.Add("empire_north_inf_1");

            return world;
        }

        /// <summary>手动跑 N 个完整回合（直接驱动 TurnResolver）</summary>
        private void RunTurns(TurnResolver turnResolver, GameClock clock, WorldState world, int turns)
        {
            for (int t = 0; t < turns; t++)
            {
                turnResolver.ExecuteTurn(world);
                // 推进 clock 跳过 5 阶段到下一回合
                for (int p = 0; p < 5; p++)
                    clock.AdvancePhase();
            }
        }

        [Test]
        public void SaveLoadEquivalence_2TurnsSave2More_Equals4Turns()
        {
            var config = CreateRealConfig();
            var events = new EventBus();

            // === Path A: 跑 2 回合 → 存 → 读 → 再跑 2 回合 ===
            var worldA = BuildWorldWithProvinces();
            var clockA = new GameClock(events);
            var rngA = new RandomService(12345);
            var economyA = new EconomyResolver(config, events);
            var politicsA = new PoliticsResolver(config);
            var battleA = new BattleResolver(rngA, events);
            var supplyA = new SupplyResolver();
            var constructionA = new ConstructionResolver();
            var aiA = new AIResolver(config, constructionA);
            var diplomacyA = new DiplomacyResolver();
            var turnA = new TurnResolver(clockA, events, economyA, politicsA, battleA, supplyA, aiA, diplomacyA, constructionA);

            RunTurns(turnA, clockA, worldA, 2);

            // 存档
            var saveData = SaveMapper.ToSave(worldA, 12345, rngA.State, clockA.CurrentPhase);
            var saveRepo = new InMemorySaveRepository();
            saveRepo.Save("slot1", saveData);

            // 读档到新世界
            var loaded = saveRepo.Load("slot1");
            var worldB = SaveMapper.ToRuntime(loaded);
            var rngB = new RandomService(loaded.seed);
            rngB.RestoreState(loaded.rngState);
            var clockB = new GameClock(events);
            clockB.Restore(loaded.turnNumber, System.Enum.Parse<GamePhase>(loaded.phase));

            var eventsB = new EventBus();
            var economyB = new EconomyResolver(config, eventsB);
            var politicsB = new PoliticsResolver(config);
            var battleB = new BattleResolver(rngB, eventsB);
            var supplyB = new SupplyResolver();
            var constructionB = new ConstructionResolver();
            var aiB = new AIResolver(config, constructionB);
            var diplomacyB = new DiplomacyResolver();
            var turnB = new TurnResolver(clockB, eventsB, economyB, politicsB, battleB, supplyB, aiB, diplomacyB, constructionB);

            RunTurns(turnB, clockB, worldB, 2);

            // === Path C: 直接跑 4 回合 ===
            var worldC = BuildWorldWithProvinces();
            var eventsC = new EventBus();
            var clockC = new GameClock(eventsC);
            var rngC = new RandomService(12345);
            var economyC = new EconomyResolver(config, eventsC);
            var politicsC = new PoliticsResolver(config);
            var battleC = new BattleResolver(rngC, eventsC);
            var supplyC = new SupplyResolver();
            var constructionC = new ConstructionResolver();
            var aiC = new AIResolver(config, constructionC);
            var diplomacyC = new DiplomacyResolver();
            var turnC = new TurnResolver(clockC, eventsC, economyC, politicsC, battleC, supplyC, aiC, diplomacyC, constructionC);

            RunTurns(turnC, clockC, worldC, 4);

            // 比较世界状态哈希
            var hashB = HashWorld(worldB);
            var hashC = HashWorld(worldC);
            Assert.AreEqual(hashC, hashB,
                "跑2→存→读→再跑2 应等于 直接跑4 的世界状态");

            // 逐字段验证关键指标
            foreach (var id in worldC.countries.Keys)
            {
                var cC = worldC.countries[id];
                var cB = worldB.countries[id];
                Assert.AreEqual(cC.treasury, cB.treasury, $"{id} treasury 不一致");
                Assert.AreEqual(cC.stability, cB.stability, $"{id} stability 不一致");
                Assert.AreEqual(cC.equipmentStockpile, cB.equipmentStockpile, $"{id} equipmentStockpile 不一致");
                foreach (var kv in cC.resources)
                {
                    Assert.AreEqual(kv.Value, cB.resources[kv.Key], $"{id} resource {kv.Key} 不一致");
                }
            }
        }

        [Test]
        public void Determinism_SameSeed_SameEconomyOutcome()
        {
            var config = CreateRealConfig();

            var worldA = BuildWorldWithProvinces();
            var eventsA = new EventBus();
            var economyA = new EconomyResolver(config, eventsA);
            var politicsA = new PoliticsResolver(config);
            var rngA = new RandomService(42);
            var battleA = new BattleResolver(rngA, eventsA);
            var supplyA = new SupplyResolver();
            var constructionA = new ConstructionResolver();
            var aiA = new AIResolver(config, constructionA);
            var diploA = new DiplomacyResolver();
            var clockA = new GameClock(eventsA);
            var turnA = new TurnResolver(clockA, eventsA, economyA, politicsA, battleA, supplyA, aiA, diploA, constructionA);

            var worldB = BuildWorldWithProvinces();
            var eventsB = new EventBus();
            var economyB = new EconomyResolver(config, eventsB);
            var politicsB = new PoliticsResolver(config);
            var rngB = new RandomService(42);
            var battleB = new BattleResolver(rngB, eventsB);
            var supplyB = new SupplyResolver();
            var constructionB = new ConstructionResolver();
            var aiB = new AIResolver(config, constructionB);
            var diploB = new DiplomacyResolver();
            var clockB = new GameClock(eventsB);
            var turnB = new TurnResolver(clockB, eventsB, economyB, politicsB, battleB, supplyB, aiB, diploB, constructionB);

            // 跑 3 回合
            for (int i = 0; i < 3; i++)
            {
                turnA.ExecuteTurn(worldA);
                turnB.ExecuteTurn(worldB);
            }

            Assert.AreEqual(HashWorld(worldA), HashWorld(worldB),
                "同种子 + 同操作 = 同世界（确定性）");
        }

        [Test]
        public void SaveLoadEquivalence_GovernanceLevels_Preserved()
        {
            var config = CreateRealConfig();
            var events = new EventBus();

            var world = BuildWorldWithProvinces();
            var clock = new GameClock(events);
            var rng = new RandomService(12345);
            var economy = new EconomyResolver(config, events);
            var politics = new PoliticsResolver(config);
            var battle = new BattleResolver(rng, events);
            var supply = new SupplyResolver();
            var construction = new ConstructionResolver();
            var ai = new AIResolver(config, construction);
            var diplo = new DiplomacyResolver();
            var turn = new TurnResolver(clock, events, economy, politics, battle, supply, ai, diplo, construction);

            // 改档位
            world.countries["empire_north"].taxLevel = 2;    // 高税
            world.countries["empire_north"].civilLevel = 0;  // 紧缩

            // 跑 1 回合
            turn.ExecuteTurn(world);
            for (int p = 0; p < 5; p++) clock.AdvancePhase();

            // 存
            var saveData = SaveMapper.ToSave(world, 12345, rng.State, clock.CurrentPhase);

            // 读
            var loaded = SaveMapper.ToRuntime(saveData);

            // 验证档位保留
            Assert.AreEqual(2, loaded.countries["empire_north"].taxLevel, "taxLevel 应保留");
            Assert.AreEqual(0, loaded.countries["empire_north"].civilLevel, "civilLevel 应保留");

            // 续跑 1 回合 → 等价
            var rngL = new RandomService(12345);
            rngL.RestoreState(saveData.rngState);
            var clockL = new GameClock(events);
            clockL.Restore(saveData.turnNumber, System.Enum.Parse<GamePhase>(saveData.phase));
            var economyL = new EconomyResolver(config, events);
            var politicsL = new PoliticsResolver(config);
            var battleL = new BattleResolver(rngL, events);
            var supplyL = new SupplyResolver();
            var constructionL = new ConstructionResolver();
            var aiL = new AIResolver(config, constructionL);
            var diploL = new DiplomacyResolver();
            var turnL = new TurnResolver(clockL, events, economyL, politicsL, battleL, supplyL, aiL, diploL, constructionL);

            turn.ExecuteTurn(world);
            turnL.ExecuteTurn(loaded);

            Assert.AreEqual(HashWorld(world), HashWorld(loaded),
                "改档→存→读→续跑 应等价");
        }

        [Test]
        public void SaveLoad_Units_PreservedAcrossSave()
        {
            // 1 支已损耗 infantry 存→读→hash 等价
            var world = BuildWorldWithProvinces();
            var unit = world.units["empire_north_inf_1"];
            unit.manpower = 80;
            unit.organization = 30;
            unit.movesLeft = 1;

            var saveData = SaveMapper.ToSave(world, 99, 0, GamePhase.TurnStart);
            var loaded = SaveMapper.ToRuntime(saveData);

            Assert.AreEqual(HashWorld(world), HashWorld(loaded),
                "已损耗部队存→读 应 hash 等价");
            Assert.AreEqual(80, loaded.units["empire_north_inf_1"].manpower);
            Assert.AreEqual(30, loaded.units["empire_north_inf_1"].organization);
            Assert.AreEqual(1, loaded.units["empire_north_inf_1"].movesLeft);
        }

        [Test]
        public void SaveLoad_UnitProductionQueue_Preserved()
        {
            // 下单 → 1 回合 → 存（turnsRemaining=1）→ 读 → 再跑 1 回合 → 完工
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

            var world = BuildWorldWithProvinces();
            // empire_north 有 steel=50, capital=100，够造 1 支步兵
            var unitProd = new UnitProductionResolver();
            var eco = config.Get<EconomyConfig>("global");

            // 入队
            var result = unitProd.TryEnqueue(world.countries["empire_north"], "infantry", config, eco);
            Assert.IsTrue(result.accepted);

            // 跑 1 回合
            unitProd.ResolveProduction(world, config);

            // 存（turnsRemaining=1）
            var saveData = SaveMapper.ToSave(world, 99, 0, GamePhase.TurnStart);
            var loaded = SaveMapper.ToRuntime(saveData);

            Assert.AreEqual(1, loaded.countries["empire_north"].unitProductionQueue.Count);
            Assert.AreEqual(1, loaded.countries["empire_north"].unitProductionQueue[0].turnsRemaining);

            // 再跑 1 回合 → 完工
            unitProd.ResolveProduction(loaded, config);

            Assert.AreEqual(0, loaded.countries["empire_north"].unitProductionQueue.Count);
            Assert.IsTrue(loaded.units.ContainsKey("empire_north_inf_2"), "新部队应生成");
            Assert.AreEqual(100, loaded.units["empire_north_inf_2"].manpower, "新部队应满编");
        }
    }
}

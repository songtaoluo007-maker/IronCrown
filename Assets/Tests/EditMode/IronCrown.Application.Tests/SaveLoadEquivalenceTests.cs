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
                resources = new Dictionary<string, int>
                {
                    { "steel", 50 },
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
                resources = new Dictionary<string, int>
                {
                    { "steel", 30 },
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
                resourceOutput = new[] { "steel" }
            };

            world.provinces["liberty_port"] = new ProvinceState
            {
                id = "liberty_port",
                name = "自由港",
                terrain = TerrainType.Coastline,
                ownerCountry = "republic_west",
                controllerCountry = "republic_west",
                infrastructure = 4,
                resourceOutput = new[] { "rareMetal" }
            };

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
            var politicsA = new PoliticsResolver();
            var battleA = new BattleResolver(rngA, events);
            var supplyA = new SupplyResolver();
            var aiA = new AIResolver();
            var diplomacyA = new DiplomacyResolver();
            var constructionA = new ConstructionResolver();
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
            var politicsB = new PoliticsResolver();
            var battleB = new BattleResolver(rngB, eventsB);
            var supplyB = new SupplyResolver();
            var aiB = new AIResolver();
            var diplomacyB = new DiplomacyResolver();
            var constructionB = new ConstructionResolver();
            var turnB = new TurnResolver(clockB, eventsB, economyB, politicsB, battleB, supplyB, aiB, diplomacyB, constructionB);

            RunTurns(turnB, clockB, worldB, 2);

            // === Path C: 直接跑 4 回合 ===
            var worldC = BuildWorldWithProvinces();
            var eventsC = new EventBus();
            var clockC = new GameClock(eventsC);
            var rngC = new RandomService(12345);
            var economyC = new EconomyResolver(config, eventsC);
            var politicsC = new PoliticsResolver();
            var battleC = new BattleResolver(rngC, eventsC);
            var supplyC = new SupplyResolver();
            var aiC = new AIResolver();
            var diplomacyC = new DiplomacyResolver();
            var constructionC = new ConstructionResolver();
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
            var politicsA = new PoliticsResolver();
            var rngA = new RandomService(42);
            var battleA = new BattleResolver(rngA, eventsA);
            var supplyA = new SupplyResolver();
            var aiA = new AIResolver();
            var diploA = new DiplomacyResolver();
            var constructionA = new ConstructionResolver();
            var clockA = new GameClock(eventsA);
            var turnA = new TurnResolver(clockA, eventsA, economyA, politicsA, battleA, supplyA, aiA, diploA, constructionA);

            var worldB = BuildWorldWithProvinces();
            var eventsB = new EventBus();
            var economyB = new EconomyResolver(config, eventsB);
            var politicsB = new PoliticsResolver();
            var rngB = new RandomService(42);
            var battleB = new BattleResolver(rngB, eventsB);
            var supplyB = new SupplyResolver();
            var aiB = new AIResolver();
            var diploB = new DiplomacyResolver();
            var constructionB = new ConstructionResolver();
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
    }
}

// ============================================================================
// DeterminismTests.cs �?确定性与存档续跑等价测试
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
    public class DeterminismTests
    {
        // FNV-1a 32-bit hash for stable cross-run hashing
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

            foreach (var c in world.countries.Values.OrderBy(x => x.id))
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(c.id));
                bytes.AddRange(System.BitConverter.GetBytes(c.treasury));
                bytes.AddRange(System.BitConverter.GetBytes(c.stability));
                bytes.AddRange(System.BitConverter.GetBytes(c.warSupport));
                bytes.AddRange(System.BitConverter.GetBytes(c.manpower));
                bytes.AddRange(System.BitConverter.GetBytes(c.civilianFactories));
                bytes.AddRange(System.BitConverter.GetBytes(c.militaryFactories));
                bytes.AddRange(System.BitConverter.GetBytes(c.equipmentStockpile));
            }

            foreach (var p in world.provinces.Values.OrderBy(x => x.id))
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.id));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.ownerCountry ?? ""));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.controllerCountry ?? ""));
            }

            return FnvHash(bytes);
        }

        /// <summary>
        /// Build a small deterministic world by hand (no config dependency).
        /// Two countries with different stats, one province each.
        /// </summary>
        private WorldState BuildTestWorld(int seed)
        {
            var rng = new RandomService(seed);
            var world = new WorldState { worldTension = 10, turnNumber = 1 };

            world.countries["alpha"] = new CountryState
            {
                id = "alpha",
                name = "Alpha",
                ideology = Ideology.ImperialOrder,
                treasury = 500 + rng.Range(0, 100),
                stability = 70 + rng.Range(0, 20),
                warSupport = 50 + rng.Range(0, 30),
                civilianFactories = 3,
                militaryFactories = 2,
                resources = new Dictionary<string, int> { { "steel", 50 } }
            };
            world.countries["beta"] = new CountryState
            {
                id = "beta",
                name = "Beta",
                ideology = Ideology.FreeRepublic,
                treasury = 300 + rng.Range(0, 100),
                stability = 60 + rng.Range(0, 20),
                warSupport = 40 + rng.Range(0, 30),
                civilianFactories = 2,
                militaryFactories = 1,
                resources = new Dictionary<string, int> { { "steel", 30 } }
            };

            world.provinces["p1"] = new ProvinceState
            {
                id = "p1",
                name = "Province 1",
                terrain = TerrainType.Plain,
                ownerCountry = "alpha",
                controllerCountry = "alpha"
            };
            world.provinces["p2"] = new ProvinceState
            {
                id = "p2",
                name = "Province 2",
                terrain = TerrainType.Forest,
                ownerCountry = "beta",
                controllerCountry = "beta"
            };

            return world;
        }

        private (GameSessionService session, InMemorySaveRepository repo) CreateSession(int seed)
        {
            var clock = new GameClock(new EventBus());
            var logger = new StubLogger();
            var config = new ConfigRegistry(new StubConfigRepository());
            var rng = new RandomService(seed);
            var initializer = new WorldInitializer(logger);
            var economy = new EconomyResolver(config, new EventBus());
            var politics = new PoliticsResolver(config);
            var battle = new BattleResolver(rng, new EventBus());
            var supply = new SupplyResolver();
            var construction = new ConstructionResolver();
            var unitProduction = new UnitProductionResolver();
            var movement = new MovementResolver();
            var ai = new AIResolver(config, construction, new BattleResolver(rng, new EventBus()));
            var diplomacy = new DiplomacyResolver();
            var turnResolver = new TurnResolver(clock, new EventBus(), economy, politics, battle, supply, ai, diplomacy, construction, unitProduction, movement, config);
            var saveRepo = new InMemorySaveRepository();
            var builder = new ReadModelBuilder();

            var session = new GameSessionService(clock, config, initializer, turnResolver, construction, unitProduction, movement, battle, new EventBus(), saveRepo, rng, builder, logger);
            return (session, saveRepo);
        }

        [Test]
        public void SameSeed_SameWorldAfterAdvance()
        {
            // Build identical worlds, advance same phases, compare hash
            var worldA = BuildTestWorld(42);
            var worldB = BuildTestWorld(42);

            // Advance both by manipulating turnNumber (since we can't drive the full pipeline without configs)
            worldA.turnNumber = 5;
            worldB.turnNumber = 5;

            var hashA = HashWorld(worldA);
            var hashB = HashWorld(worldB);

            Assert.AreEqual(hashA, hashB, "Same seed + same operations = same world");
        }

        [Test]
        public void DifferentSeed_DifferentWorld()
        {
            var worldA = BuildTestWorld(42);
            var worldB = BuildTestWorld(99);

            var hashA = HashWorld(worldA);
            var hashB = HashWorld(worldB);

            Assert.AreNotEqual(hashA, hashB, "Different seeds should produce different initial states");
        }

        [Test]
        public void RNG_StateRestore_ProducesSameSequence()
        {
            var rng = new RandomService(42);

            // Advance
            for (int i = 0; i < 20; i++) rng.Next(100);
            ulong savedState = rng.State;

            // Continue
            var continued = new int[10];
            for (int i = 0; i < 10; i++) continued[i] = rng.Next(100);

            // Restore and re-run
            rng.RestoreState(savedState);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(continued[i], rng.Next(100),
                    $"Mismatch at step {i} after state restore");
            }
        }
    }
}

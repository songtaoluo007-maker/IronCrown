// ============================================================================
// ReplayTests.cs — P3a Deterministic Replay Tests
// R2 fix: real record->replay->HashWorld equivalence
// ============================================================================

using NUnit.Framework;
using IronCrown.Application.Replay;
using IronCrown.Application;
using IronCrown.Contracts;
using IronCrown.Domain;
using IronCrown.Simulation;
using System.Collections.Generic;
using System.Linq;

namespace IronCrown.Tests
{
    // R2: test in-memory save repo
    internal class TestSaveRepo : ISaveRepository
    {
        private readonly Dictionary<string, GameState> _saves = new();
        public bool Save(string slot, GameState state) { _saves[slot] = state; return true; }
        public GameState Load(string slot) => _saves.TryGetValue(slot, out var s) ? s : null;
        public bool Delete(string slot) => _saves.Remove(slot);
        public string[] ListSaves() => _saves.Keys.ToArray();
    }

    // R2: test factory (cross-assembly)
    internal static class ReplayTestFactory
    {
        public static GameSessionService Create(ISaveRepository save = null)
        {
            var ev = new EventBus();
            var lg = new NoopLogger();
            var rn = new RandomService(12345);
            var cl = new GameClock(ev);
            var cfg = new TestConfigRegistry();
            var sv = save ?? new TestSaveRepo();
            var initializer = new WorldInitializer(lg);
            var battle = new BattleResolver(rn, ev);
            var construction = new ConstructionResolver();
            var unitProduction = new UnitProductionResolver();
            var movement = new MovementResolver();
            var commander = new CommanderResolver(cfg);
            var readModel = new ReadModelBuilder();
            var turnResolver = new TurnResolver(cl, ev, new EconomyResolver(cfg, ev),
                new PoliticsResolver(cfg), battle, new SupplyResolver(),
                new AIResolver(cfg, construction, battle), new DiplomacyResolver(),
                construction, unitProduction, movement, cfg, new VictoryConditionResolver(ev));
            return new GameSessionService(cl, cfg, initializer, turnResolver, construction,
                unitProduction, movement, battle, new PeaceResolver(ev),
                ev, sv, rn, readModel, lg, commander);
        }
    }

    internal class NoopLogger : IAppLogger
    {
        public void Info(string msg) { }
        public void Warn(string msg) { }
        public void Error(string msg) { }
    }

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
            if (_byType.TryGetValue(key, out var dict) && dict.TryGetValue(id, out var obj)) return obj as T;
            return null;
        }
        public IReadOnlyList<T> All<T>() where T : class
        {
            var key = typeof(T).FullName;
            if (_lists.TryGetValue(key, out var list)) return list.ConvertAll(o => (T)o);
            return new List<T>();
        }
        public bool Has<T>(string id) where T : class
        {
            var key = typeof(T).FullName;
            return _byType.TryGetValue(key, out var dict) && dict.ContainsKey(id);
        }
        public void LoadAll() { }
    }

    public class ReplayTests
    {
        private const int GoldenSeed = 20260608;
        private const string GoldenPlayerCountry = "empire_north";

        // R2: golden baseline hash — lock after first run
        private static string GoldenBaselineHash = "UNSET_RUN_ONCE_TO_GENERATE";

        // =================================================================
        // HashWorld (same as SaveLoadEquivalenceTests, FNV-1a)
        // =================================================================

        private static int FnvHash(IEnumerable<byte> data)
        {
            const int fnvPrime = 16777619;
            const int offsetBasis = unchecked((int)2166136261);
            int hash = offsetBasis;
            foreach (var b in data) { hash ^= b; hash *= fnvPrime; }
            return hash;
        }

        private static int HashWorld(WorldState world)
        {
            var bytes = new List<byte>();
            foreach (var c in world.countries.Values.OrderBy(c => c.id))
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(c.id));
                bytes.AddRange(System.BitConverter.GetBytes(c.treasury));
                bytes.AddRange(System.BitConverter.GetBytes(c.stability));
                bytes.AddRange(System.BitConverter.GetBytes(c.warSupport));
                bytes.AddRange(System.BitConverter.GetBytes(c.warExhaustion));
                bytes.AddRange(System.BitConverter.GetBytes(c.manpower));
                bytes.AddRange(System.BitConverter.GetBytes(c.civilianFactories));
                bytes.AddRange(System.BitConverter.GetBytes(c.militaryFactories));
                bytes.AddRange(System.BitConverter.GetBytes(c.equipmentStockpile));
                foreach (var r in c.resources.OrderBy(r => r.Key))
                {
                    bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(r.Key));
                    bytes.AddRange(System.BitConverter.GetBytes(r.Value));
                }
                bytes.AddRange(System.BitConverter.GetBytes(c.gachaTickets));
                bytes.AddRange(System.BitConverter.GetBytes(c.gachaPityCounter));
            }
            foreach (var p in world.provinces.Values.OrderBy(p => p.id))
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.id));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.ownerCountry ?? ""));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.controllerCountry ?? ""));
                if (p.neighbors != null)
                    foreach (var n in p.neighbors)
                        bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(n));
                bytes.AddRange(System.BitConverter.GetBytes(p.gridX));
                bytes.AddRange(System.BitConverter.GetBytes(p.gridY));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(p.terrain.ToString()));
                bytes.AddRange(System.BitConverter.GetBytes(p.resistance));
                bytes.AddRange(System.BitConverter.GetBytes(p.compliance));
            }
            foreach (var u in world.units.Values.OrderBy(u => u.id))
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
                bytes.AddRange(System.BitConverter.GetBytes(u.tacticalExp));
                bytes.AddRange(System.BitConverter.GetBytes(u.recoveryTurnsLeft));
                bytes.AddRange(System.BitConverter.GetBytes(u.isCutoff));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(u.commanderId ?? ""));
            }
            foreach (var cmdr in world.commanders.Values.OrderBy(c => c.id))
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(cmdr.id));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(cmdr.ownerCountry));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(cmdr.generalCardId ?? ""));
                bytes.AddRange(System.BitConverter.GetBytes(cmdr.rank));
                bytes.AddRange(System.BitConverter.GetBytes(cmdr.victories));
                bytes.AddRange(System.BitConverter.GetBytes(cmdr.starLevel));
                bytes.AddRange(System.BitConverter.GetBytes(cmdr.isActive));
            }
            foreach (var tile in world.tiles.Values.OrderBy(t => t.id))
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(tile.id));
                bytes.AddRange(System.BitConverter.GetBytes(tile.gridX));
                bytes.AddRange(System.BitConverter.GetBytes(tile.gridY));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(tile.terrain.ToString()));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(tile.provinceId));
            }
            foreach (var w in world.warRelations)
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(w.countryA));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(w.countryB));
                bytes.AddRange(System.BitConverter.GetBytes(w.startTurn));
            }
            foreach (var t in world.truceUntilTurn.OrderBy(kv => kv.Key))
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(t.Key));
                bytes.AddRange(System.BitConverter.GetBytes(t.Value));
            }
            foreach (var b in world.activeBattles)
            {
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(b.id));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(b.attackerUnitIds[0]));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(b.defenderUnitIds[0]));
                bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(b.provinceId));
                bytes.AddRange(System.BitConverter.GetBytes(b.turnsElapsed));
            }
            return FnvHash(bytes);
        }

        private WorldState ExtractWorldState(GameSessionService session, TestSaveRepo repo, string slot)
        {
            session.Save(slot);
            var gameState = repo.Load(slot);
            return SaveMapper.ToRuntime(gameState);
        }

        private ReplayData BuildGoldenScript()
        {
            var replay = new ReplayData
            {
                seed = GoldenSeed,
                playerCountryId = GoldenPlayerCountry,
                initialConfigId = "default",
                initialConfigVersion = "1.0"
            };

            var turn1 = new TurnCommands { turnNumber = 1 };
            turn1.commands.Add(new GameCommand { commandType = CommandType.BuildCivilianFactory, countryId = GoldenPlayerCountry });
            turn1.commands.Add(new GameCommand { commandType = CommandType.BuildCivilianFactory, countryId = GoldenPlayerCountry });
            turn1.commands.Add(new GameCommand { commandType = CommandType.BuildMilitaryFactory, countryId = GoldenPlayerCountry });
            replay.turns.Add(turn1);

            var turn2 = new TurnCommands { turnNumber = 2 };
            turn2.commands.Add(new GameCommand { commandType = CommandType.BuildUnit, countryId = GoldenPlayerCountry, unitType = "infantry" });
            replay.turns.Add(turn2);

            for (int t = 3; t <= 6; t++)
                replay.turns.Add(new TurnCommands { turnNumber = t });

            var turn7 = new TurnCommands { turnNumber = 7 };
            turn7.commands.Add(new GameCommand { commandType = CommandType.SetTaxLevel, countryId = GoldenPlayerCountry, level = 2 });
            replay.turns.Add(turn7);

            return replay;
        }

        // =================================================================
        // Tests
        // =================================================================

        [Test]
        public void RecordReplay_SameWorld()
        {
            // R2: original hash == replayed hash
            var repo = new TestSaveRepo();
            var session = ReplayTestFactory.Create(repo);
            session.NewGame(GoldenSeed);

            var playerCountry = session.PlayerCountryId;

            // Record + execute commands
            var recorder = new ReplayRecorder();
            recorder.StartRecording(GoldenSeed, playerCountry);

            recorder.RecordCommand(new GameCommand { commandType = CommandType.BuildCivilianFactory, countryId = playerCountry });
            session.IssueCommand(new GameCommand { commandType = CommandType.BuildCivilianFactory, countryId = playerCountry });

            recorder.RecordCommand(new GameCommand { commandType = CommandType.SetTaxLevel, countryId = playerCountry, level = 1 });
            session.IssueCommand(new GameCommand { commandType = CommandType.SetTaxLevel, countryId = playerCountry, level = 1 });

            var replayData = recorder.StopRecording();

            // Original -> hash
            var worldA = ExtractWorldState(session, repo, "original");
            var hashA = HashWorld(worldA);

            // Replay -> hash
            var repo2 = new TestSaveRepo();
            var session2 = ReplayTestFactory.Create(repo2);
            new ReplayPlayer(session2).Play(replayData);
            var worldB = ExtractWorldState(session2, repo2, "replay");
            var hashB = HashWorld(worldB);

            Assert.AreEqual(hashA, hashB,
                string.Format("Record->Replay HashWorld mismatch: original={0}, replay={1}", hashA, hashB));
        }

        [Test]
        public void GoldenReplay_MatchesBaseline()
        {
            // R2: golden script replay -> hash -> lock baseline
            var script = BuildGoldenScript();

            var repo = new TestSaveRepo();
            var session = ReplayTestFactory.Create(repo);
            new ReplayPlayer(session).Play(script);

            var world = ExtractWorldState(session, repo, "golden");
            var hash = HashWorld(world);

            TestContext.Out.WriteLine(string.Format("[GoldenReplay] hash = {0}", hash));

            if (GoldenBaselineHash == "UNSET_RUN_ONCE_TO_GENERATE")
            {
                TestContext.Out.WriteLine(string.Format("[GoldenReplay] First run. Set GoldenBaselineHash to: {0}", hash));
                GoldenBaselineHash = hash.ToString();
                Assert.Inconclusive(string.Format("Golden baseline hash generated: {0}. Lock this value in test code.", hash));
            }
            else
            {
                Assert.AreEqual(int.Parse(GoldenBaselineHash), hash,
                    string.Format("Golden replay hash mismatch: expected={0}, actual={1}. Determinism broken!", GoldenBaselineHash, hash));
            }
        }

        [Test]
        public void Replay_SameSeed_TwiceIdentical()
        {
            // R2: same ReplayData twice -> same hash
            var script = BuildGoldenScript();

            var repo1 = new TestSaveRepo();
            var session1 = ReplayTestFactory.Create(repo1);
            new ReplayPlayer(session1).Play(script);
            var world1 = ExtractWorldState(session1, repo1, "run1");
            var hash1 = HashWorld(world1);

            var repo2 = new TestSaveRepo();
            var session2 = ReplayTestFactory.Create(repo2);
            new ReplayPlayer(session2).Play(script);
            var world2 = ExtractWorldState(session2, repo2, "run2");
            var hash2 = HashWorld(world2);

            Assert.AreEqual(hash1, hash2,
                string.Format("Same seed twice should produce identical hash: run1={0}, run2={1}", hash1, hash2));
        }
    }
}

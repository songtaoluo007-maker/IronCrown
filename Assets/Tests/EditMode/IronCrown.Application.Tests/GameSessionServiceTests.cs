// ============================================================================
// GameSessionServiceTests.cs — GameSessionService 集成测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Application;
using IronCrown.Contracts;
using IronCrown.Domain;
using IronCrown.Simulation;
using System.Collections.Generic;

namespace IronCrown.Application.Tests
{
    // --- Test stubs for isolation ---
    internal class StubConfigRepository : IConfigRepository
    {
        public T Load<T>(string configName) where T : class => null;
        public List<T> LoadList<T>(string configName) where T : class => new List<T>();
        public void ClearCache() { }
    }

    internal class StubLogger : IAppLogger
    {
        public void Info(string msg) { }
        public void Warn(string msg) { }
        public void Error(string msg) { }
    }

    internal class InMemorySaveRepository : ISaveRepository
    {
        private readonly Dictionary<string, GameState> _store = new();
        public bool Save(string slot, GameState state) { _store[slot] = state; return true; }
        public GameState Load(string slot) => _store.TryGetValue(slot, out var s) ? s : null;
        public bool Delete(string slot) => _store.Remove(slot);
        public string[] ListSaves() => new List<string>(_store.Keys).ToArray();
    }

    public class GameSessionServiceTests
    {
        private GameSessionService _session;
        private GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = new GameClock(new EventBus());
            var logger = new StubLogger();
            var config = new ConfigRegistry(new StubConfigRepository());
            var rng = new RandomService(42);
            var initializer = new WorldInitializer(logger);
            var economy = new EconomyResolver();
            var politics = new PoliticsResolver();
            var battle = new BattleResolver(rng, new EventBus());
            var supply = new SupplyResolver();
            var ai = new AIResolver();
            var diplomacy = new DiplomacyResolver();
            var turnResolver = new TurnResolver(_clock, new EventBus(), economy, politics, battle, supply, ai, diplomacy);
            var saveRepo = new InMemorySaveRepository();
            var builder = new ReadModelBuilder();

            _session = new GameSessionService(_clock, config, initializer, turnResolver, saveRepo, rng, builder, logger);
        }

        [Test]
        public void NewGame_CreatesWorld()
        {
            _session.NewGame();
            var view = _session.GetWorldView();

            Assert.IsNotNull(view);
            Assert.AreEqual(1, view.turn);
        }

        [Test]
        public void AdvancePhase_ProgressesClock()
        {
            _session.NewGame();
            var before = _session.GetWorldView().phase;

            _session.AdvancePhase();
            var after = _session.GetWorldView().phase;

            Assert.AreNotEqual(before, after);
            Assert.AreEqual("InternalAffairs", after);
        }

        [Test]
        public void GetWorldView_ReturnsNullBeforeNewGame()
        {
            var view = _session.GetWorldView();
            Assert.IsNull(view);
        }
    }
}

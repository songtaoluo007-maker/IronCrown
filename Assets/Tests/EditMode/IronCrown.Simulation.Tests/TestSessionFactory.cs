// ============================================================================
// TestSessionFactory.cs — 测试专用工厂（防构造函数参数变更炸测试）
// 以后 GameSessionService 加新参数，只改这一个文件
// ============================================================================

using IronCrown.Application;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Simulation.Tests
{
    /// <summary>测试用 GameSessionService 工厂</summary>
    internal static class TestSessionFactory
    {
        /// <summary>创建最小可用 GameSessionService（所有依赖可选传入）</summary>
        public static GameSessionService Create(
            IConfigRegistry config = null,
            ITurnClock clock = null,
            IEventPublisher events = null,
            IRandom rng = null,
            IAppLogger logger = null,
            ISaveRepository save = null)
        {
            var ev = events ?? new EventBus();
            var lg = logger ?? new NoopLogger();
            var rn = rng ?? new RandomService(12345);
            var cl = clock ?? new GameClock(ev);
            var cfg = config ?? new TestConfigRegistry();
            var sv = save ?? new MemSaveRepo();

            var initializer = new WorldInitializer(lg);
            var economy = new EconomyResolver(cfg, ev);
            var politics = new PoliticsResolver(cfg);
            var battle = new BattleResolver(rn, ev);
            var supply = new SupplyResolver();
            var construction = new ConstructionResolver();
            var unitProduction = new UnitProductionResolver();
            var movement = new MovementResolver();
            var ai = new AIResolver(cfg, construction, battle);
            var diplo = new DiplomacyResolver();
            var victory = new VictoryConditionResolver(ev);
            var peace = new PeaceResolver(ev);
            var commander = new CommanderResolver(cfg);
            var readModel = new ReadModelBuilder();

            var turnResolver = new TurnResolver(
                cl, ev, economy, politics, battle, supply,
                ai, diplo, construction, unitProduction, movement, cfg, victory);

            return new GameSessionService(
                cl, cfg, initializer, turnResolver, construction,
                unitProduction, movement, battle, peace,
                ev, sv, rn, readModel, lg, commander);
        }
    }

    /// <summary>空日志器（测试用）</summary>
    internal class NoopLogger : IAppLogger
    {
        public void Info(string msg) { }
        public void Warn(string msg) { }
        public void Error(string msg) { }
    }

    /// <summary>内存存档仓库（测试用）</summary>
    internal class MemSaveRepo : ISaveRepository
    {
        private readonly System.Collections.Generic.Dictionary<string, GameState> _saves = new();
        public bool Save(string slot, GameState state) { _saves[slot] = state; return true; }
        public GameState Load(string slot) => _saves.TryGetValue(slot, out var s) ? s : null;
        public bool Delete(string slot) => _saves.Remove(slot);
        public string[] ListSaves() => System.Linq.Enumerable.ToArray(_saves.Keys);
    }
}

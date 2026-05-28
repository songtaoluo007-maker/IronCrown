// ============================================================================
// Application/Session/GameSessionService.cs — 游戏会话门面
// UI 唯一合法入口（规则 4），持有运行时 WorldState
// ============================================================================

using IronCrown.Contracts;
using IronCrown.Domain;
using IronCrown.Simulation;

namespace IronCrown.Application
{
    public sealed class GameSessionService
    {
        private readonly ITurnClock _clock;
        private readonly IConfigRegistry _config;
        private readonly WorldInitializer _initializer;
        private readonly TurnResolver _turnResolver;
        private readonly ISaveRepository _save;
        private readonly IRandom _rng;
        private readonly ReadModelBuilder _builder;
        private readonly IAppLogger _logger;

        private WorldState _world;
        private int _initialSeed;

        public GameSessionService(
            ITurnClock clock,
            IConfigRegistry config,
            WorldInitializer initializer,
            TurnResolver turnResolver,
            ISaveRepository save,
            IRandom rng,
            ReadModelBuilder builder,
            IAppLogger logger)
        {
            _clock = clock;
            _config = config;
            _initializer = initializer;
            _turnResolver = turnResolver;
            _save = save;
            _rng = rng;
            _builder = builder;
            _logger = logger;
        }

        public void NewGame(int? seed = null)
        {
            if (seed.HasValue)
                _rng.Reset(seed.Value);

            _config.LoadAll();
            _clock.Reset(60);
            _world = _initializer.CreateNewGame(_config);

            _logger.Info($"[Session] New game: {_world.countries.Count} countries, {_world.provinces.Count} provinces");
        }

        public void AdvancePhase()
        {
            if (_world == null) return;

            if (_clock.CurrentPhase == GamePhase.GameOver)
            {
                _logger.Info("[Session] Game over");
                return;
            }

            if (_clock.CurrentPhase == GamePhase.TurnStart)
            {
                _turnResolver.ExecuteTurn(_world);
            }

            _clock.AdvancePhase();
            _logger.Info($"[Session] Turn {_clock.CurrentTurn} - Phase: {_clock.CurrentPhase}");
        }

        public bool Save(string slot)
        {
            if (_world == null) return false;
            var gameState = SaveMapper.ToSave(_world, _initialSeed, _clock.CurrentPhase);
            _save.Save(slot, gameState);
            return true;
        }

        public bool Load(string slot)
        {
            var gameState = _save.Load(slot);
            if (gameState == null) return false;

            _world = SaveMapper.ToRuntime(gameState);
            _clock.Reset(60);
            _logger.Info($"[Session] Loaded save, turn {gameState.turnNumber}");
            return true;
        }

        public WorldView GetWorldView()
        {
            if (_world == null) return null;
            return _builder.BuildWorldView(_world, _clock);
        }
    }
}

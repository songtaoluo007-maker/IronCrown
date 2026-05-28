// ============================================================================
// Bootstrap/GameEntryPoint.cs — 游戏入口（VContainer IStartable）
// ============================================================================

using IronCrown.Application;
using IronCrown.Domain;
using IronCrown.Contracts;
using IronCrown.Simulation;
using VContainer.Unity;

namespace IronCrown.Bootstrap
{
    public sealed class GameEntryPoint : IStartable
    {
        private readonly ITurnClock _clock;
        private readonly IEventPublisher _events;
        private readonly IConfigRepository _config;
        private readonly ISaveRepository _save;
        private readonly IAppLogger _logger;
        private readonly IConfigRegistry _configRegistry;
        private readonly WorldInitializer _worldInitializer;
        private readonly TurnResolver _turnResolver;
        private readonly EconomyResolver _economy;
        private readonly PoliticsResolver _politics;

        private WorldState _world;
        private int _initialSeed;

        public GameEntryPoint(
            ITurnClock clock,
            IEventPublisher events,
            IConfigRepository config,
            ISaveRepository save,
            IAppLogger logger,
            IConfigRegistry configRegistry,
            WorldInitializer worldInitializer,
            TurnResolver turnResolver,
            EconomyResolver economy,
            PoliticsResolver politics)
        {
            _clock = clock;
            _events = events;
            _config = config;
            _save = save;
            _logger = logger;
            _configRegistry = configRegistry;
            _worldInitializer = worldInitializer;
            _turnResolver = turnResolver;
            _economy = economy;
            _politics = politics;
        }

        public void Start()
        {
            _logger.Info("[EntryPoint] Game started");

            _configRegistry.LoadAll();
            _logger.Info("[EntryPoint] Config loaded");

            StartNewGame();
        }

        public void StartNewGame()
        {
            _clock.Reset(60);
            _world = _worldInitializer.CreateNewGame(_configRegistry);
            _logger.Info($"[EntryPoint] New game: {_world.countries.Count} countries, {_world.provinces.Count} provinces");
        }

        public void NextPhase()
        {
            if (_clock.CurrentPhase == GamePhase.GameOver)
            {
                _logger.Info("[EntryPoint] Game over");
                return;
            }

            if (_clock.CurrentPhase == GamePhase.TurnStart)
            {
                _turnResolver.ExecuteTurn(_world);
            }

            _clock.AdvancePhase();
            _logger.Info($"[EntryPoint] Turn {_clock.CurrentTurn} - Phase: {_clock.CurrentPhase}");
        }

        public void SaveGame(string slotName)
        {
            var gameState = SaveMapper.ToSave(_world, _initialSeed, _clock.CurrentPhase);
            _save.Save(slotName, gameState);
        }

        public void LoadGame(string slotName)
        {
            var gameState = _save.Load(slotName);
            if (gameState != null)
            {
                _world = SaveMapper.ToRuntime(gameState);
                _clock.Reset(60);
                _logger.Info($"[EntryPoint] Loaded save, turn {gameState.turnNumber}");
            }
        }
    }
}

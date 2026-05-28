// ============================================================================
// Bootstrap/GameEntryPoint.cs — 游戏入口（VContainer IStartable）
// 替代 Infrastructure/GameManager.cs
// ============================================================================

using IronCrown.Application;
using IronCrown.Domain;
using UnityEngine;

namespace IronCrown.Bootstrap
{
    public sealed class GameEntryPoint
    {
        private readonly ITurnClock _clock;
        private readonly IEventPublisher _events;
        private readonly IConfigRepository _config;
        private readonly ISaveRepository _save;
        private readonly IAppLogger _logger;
        private readonly TurnResolver _turnResolver;
        private readonly Simulation.EconomyResolver _economy;
        private readonly Simulation.PoliticsResolver _politics;

        private WorldState _world;
        private int _initialSeed;

        public GameEntryPoint(
            ITurnClock clock,
            IEventPublisher events,
            IConfigRepository config,
            ISaveRepository save,
            IAppLogger logger,
            TurnResolver turnResolver,
            Simulation.EconomyResolver economy,
            Simulation.PoliticsResolver politics)
        {
            _clock = clock;
            _events = events;
            _config = config;
            _save = save;
            _logger = logger;
            _turnResolver = turnResolver;
            _economy = economy;
            _politics = politics;
        }

        public void Start()
        {
            _logger.Info("[EntryPoint] 游戏启动");
            StartNewGame();
        }

        public void StartNewGame()
        {
            _clock.Reset(60);
            _world = new WorldState { worldTension = 10, turnNumber = 1 };
            // TODO: 从配置初始化国家、省份、单位
            _logger.Info("[EntryPoint] 新游戏开始");
        }

        public void NextPhase()
        {
            if (_clock.CurrentPhase == GamePhase.GameOver)
            {
                _logger.Info("[EntryPoint] 游戏已结束");
                return;
            }

            if (_clock.CurrentPhase == GamePhase.TurnStart)
            {
                _turnResolver.ExecuteTurn(_world);
            }

            _clock.AdvancePhase();
            _logger.Info($"[EntryPoint] 回合 {_clock.CurrentTurn} - 阶段: {_clock.CurrentPhase}");
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
                _logger.Info($"[EntryPoint] 读档成功，回合 {gameState.turnNumber}");
            }
        }
    }
}

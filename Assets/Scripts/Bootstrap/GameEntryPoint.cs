// ============================================================================
// Bootstrap/GameEntryPoint.cs — 游戏入口（VContainer IStartable）
// 瘦身为只委托 GameSessionService
// ============================================================================

using IronCrown.Application;
using IronCrown.Domain;
using VContainer.Unity;

namespace IronCrown.Bootstrap
{
    public sealed class GameEntryPoint : IStartable
    {
        private readonly GameSessionService _session;
        private readonly IAppLogger _logger;

        public GameEntryPoint(GameSessionService session, IAppLogger logger)
        {
            _session = session;
            _logger = logger;
        }

        public void Start()
        {
            _logger.Info("[EntryPoint] Game started");
            _session.NewGame();
            _logger.Info("[EntryPoint] Session initialized");
        }
    }
}

// ============================================================================
// Bootstrap/GameEntryPoint.cs — 游戏入口（VContainer IStartable）
// 先建世界，再初始化 UI
// ============================================================================

using IronCrown.Application;
using IronCrown.Domain;
using IronCrown.Presentation;
using UnityEngine;
using VContainer.Unity;

namespace IronCrown.Bootstrap
{
    public sealed class GameEntryPoint : IStartable
    {
        private readonly GameSessionService _session;
        private readonly MainHudController _hudController;
        private readonly IAppLogger _logger;

        public GameEntryPoint(GameSessionService session, MainHudController hudController, IAppLogger logger)
        {
            _session = session;
            _hudController = hudController;
            _logger = logger;
        }

        public void Start()
        {
            _logger.Info("[EntryPoint] Game started");
            _session.NewGame();
            _logger.Info("[EntryPoint] Session initialized");

            // 初始化 UI（场景中必须有 MainHudBehaviour）
            var hudBehaviour = Object.FindObjectOfType<MainHudBehaviour>();
            if (hudBehaviour != null)
            {
                hudBehaviour.SetController(_hudController);
                _logger.Info("[EntryPoint] HUD bound");
            }
            else
            {
                _logger.Warn("[EntryPoint] MainHudBehaviour not found in scene");
            }
        }
    }
}

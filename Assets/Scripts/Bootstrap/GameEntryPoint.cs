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
        private readonly NationSelectionController _nationSelection;
        private readonly IAppLogger _logger;

        public GameEntryPoint(GameSessionService session, MainHudController hudController, NationSelectionController nationSelection, IAppLogger logger)
        {
            _session = session;
            _hudController = hudController;
            _nationSelection = nationSelection;
            _logger = logger;
        }

        public void Start()
        {
            try
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

                    // C9b: 选国覆盖面板
                    var uiDoc = hudBehaviour.GetComponent<UnityEngine.UIElements.UIDocument>();
                    if (uiDoc != null)
                    {
                        _nationSelection.Bind(uiDoc.rootVisualElement);
                        _logger.Info("[EntryPoint] NationSelection bound");
                    }

                    // P2.3: 初始化 Tilemap 地图渲染
                    var mapRenderer = Object.FindObjectOfType<MapRenderer>();
                    if (mapRenderer != null)
                    {
                        // MapRenderer 由 SetupScene 创建,已在场景中
                        // 绑定到 HUD Controller 的渲染流程
                        _hudController.SetMapRenderer(mapRenderer);
                        _logger.Info("[EntryPoint] MapRenderer bound");
                    }
                    else
                    {
                        _logger.Warn("[EntryPoint] MapRenderer not found — 地图将不渲染");
                    }

                    // P2.3: 初始化地图输入控制
                    var mapInput = Object.FindObjectOfType<MapInputController>();
                    if (mapInput != null)
                    {
                        mapInput.Initialize(_session, _hudController);
                        _logger.Info("[EntryPoint] MapInputController initialized");
                    }
                }
                else
                {
                    _logger.Warn("[EntryPoint] MainHudBehaviour not found in scene");
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error($"[EntryPoint] STARTUP FAILED: {ex}");
                UnityEngine.Debug.LogError($"[EntryPoint] STARTUP FAILED: {ex}");
                throw;
            }
        }
    }
}

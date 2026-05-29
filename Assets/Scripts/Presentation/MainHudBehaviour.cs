// ============================================================================
// Presentation/Views/MainHudBehaviour.cs — MonoBehaviour 桥接
// 持有 UIDocument，在 OnEnable 时把 rootVisualElement 交给 Controller
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace IronCrown.Presentation
{
    /// <summary>
    /// 挂在 UIDocument 所在 GameObject 上。
    /// 通过 VContainer 注入 MainHudController，或在 Enable 时自行获取。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainHudBehaviour : MonoBehaviour
    {
        private MainHudController _controller;
        private UIDocument _doc;

        /// <summary>暴露控制器供集成测试程序化触发（只读）</summary>
        public MainHudController Controller => _controller;

        /// <summary>由 DI 容器或 EntryPoint 注入控制器</summary>
        public void SetController(MainHudController controller)
        {
            _controller = controller;
            // 绑定时机修复：OnEnable 可能早于控制器注入而跳过 Bind。
            // 控制器到位时若 UIDocument 已就绪，立即绑定渲染（此时世界已由 NewGame 建好）。
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_controller != null && _doc != null)
                _controller.Bind(_doc.rootVisualElement);
        }

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            if (_controller != null && _doc != null)
            {
                _controller.Bind(_doc.rootVisualElement);
            }
        }

        private void OnDisable()
        {
            _controller?.Unbind();
        }
    }
}

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

        /// <summary>由 DI 容器或 EntryPoint 注入控制器</summary>
        public void SetController(MainHudController controller)
        {
            _controller = controller;
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

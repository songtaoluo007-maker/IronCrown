// ============================================================================
// Presentation/ShopPanelController.cs — P2.1 商城退役后仅显示战功点余额
// 已删除: OnBuyBundle / OnBuySsr / OnBuySpecific 钩子
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace IronCrown.Presentation
{
    public sealed class ShopPanelController
    {
        private readonly VisualElement _root;
        private readonly Label _ticketsLabel;
        private readonly Button _btnClose;

        public ShopPanelController(VisualElement root)
        {
            _root = root.Q<VisualElement>("shop-panel");
            _ticketsLabel = _root.Q<Label>("tickets-label");
            _btnClose = _root.Q<Button>("btn-close");

            _btnClose.clicked += Hide;

            // P2.1: 商城按钮全部下线（保留面板壳用于战功点显示）
            var btnBundle = _root.Q<Button>("btn-bundle");
            var btnSsr = _root.Q<Button>("btn-ssr");
            var btnSpecific = _root.Q<Button>("btn-specific");
            if (btnBundle != null) btnBundle.style.display = DisplayStyle.None;
            if (btnSsr != null) btnSsr.style.display = DisplayStyle.None;
            if (btnSpecific != null) btnSpecific.style.display = DisplayStyle.None;
        }

        public void Show(int gachaTickets)
        {
            _root.style.display = DisplayStyle.Flex;
            _ticketsLabel.text = $"战功点: {gachaTickets}";
        }

        public void UpdateTickets(int gachaTickets)
        {
            _ticketsLabel.text = $"战功点: {gachaTickets}";
        }

        public void Hide() => _root.style.display = DisplayStyle.None;
    }
}

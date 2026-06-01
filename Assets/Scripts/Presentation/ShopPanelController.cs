// ============================================================================
// Presentation/ShopPanelController.cs — C17 商城面板
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using IronCrown.Contracts;

namespace IronCrown.Presentation
{
    public sealed class ShopPanelController
    {
        private readonly VisualElement _root;
        private readonly Label _ticketsLabel;
        private readonly Button _btnBundle;
        private readonly Button _btnSsr;
        private readonly Button _btnSpecific;
        private readonly Button _btnClose;

        public ShopPanelController(VisualElement root)
        {
            _root = root.Q<VisualElement>("shop-panel");
            _ticketsLabel = _root.Q<Label>("tickets-label");
            _btnBundle = _root.Q<Button>("btn-bundle");
            _btnSsr = _root.Q<Button>("btn-ssr");
            _btnSpecific = _root.Q<Button>("btn-specific");
            _btnClose = _root.Q<Button>("btn-close");

            _btnClose.clicked += Hide;
        }

        public System.Action OnBuyBundle { get; set; }
        public System.Action OnBuySsr { get; set; }
        public System.Action OnBuySpecific { get; set; }

        public void Show(int gachaTickets)
        {
            _root.style.display = DisplayStyle.Flex;
            _ticketsLabel.text = $"券: {gachaTickets}";

            _btnBundle.clicked -= HandleBundle;
            _btnBundle.clicked += HandleBundle;
            _btnSsr.clicked -= HandleSsr;
            _btnSsr.clicked += HandleSsr;
            _btnSpecific.clicked -= HandleSpecific;
            _btnSpecific.clicked += HandleSpecific;
        }

        public void UpdateTickets(int gachaTickets)
        {
            _ticketsLabel.text = $"券: {gachaTickets}";
        }

        public void Hide() => _root.style.display = DisplayStyle.None;

        private void HandleBundle() => OnBuyBundle?.Invoke();
        private void HandleSsr() => OnBuySsr?.Invoke();
        private void HandleSpecific() => OnBuySpecific?.Invoke();
    }
}

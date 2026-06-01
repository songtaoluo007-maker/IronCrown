// ============================================================================
// Presentation/GachaPanelController.cs — C17 抽卡结果面板
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Presentation
{
    public sealed class GachaPanelController
    {
        private readonly VisualElement _root;
        private readonly Label _rarityLabel;
        private readonly Label _cardName;
        private readonly Label _cardSkills;
        private readonly Label _starLabel;
        private readonly Label _msgLabel;
        private readonly Button _btnDrawAgain;
        private readonly Button _btnClose;

        public GachaPanelController(VisualElement root)
        {
            _root = root.Q<VisualElement>("gacha-panel");
            _rarityLabel = _root.Q<Label>("rarity-label");
            _cardName = _root.Q<Label>("card-name");
            _cardSkills = _root.Q<Label>("card-skills");
            _starLabel = _root.Q<Label>("star-label");
            _msgLabel = _root.Q<Label>("msg-label");
            _btnDrawAgain = _root.Q<Button>("btn-draw-again");
            _btnClose = _root.Q<Button>("btn-close");

            _btnClose.clicked += Hide;
        }

        public System.Action OnDrawAgain { get; set; }

        public void Show(CommanderView cmdr, string rarity, string cardName, string skillsDesc)
        {
            _root.style.display = DisplayStyle.Flex;
            _rarityLabel.text = rarity;
            _cardName.text = cardName;
            _cardSkills.text = skillsDesc;
            _starLabel.text = $"★{cmdr.starLevel}";
            _msgLabel.text = $"抽到: {cardName} ({rarity})";

            // 稀有度颜色
            _rarityLabel.style.color = rarity switch
            {
                "SSR" => new Color(1f, 0.84f, 0f),   // 金色
                "SR" => new Color(0.75f, 0f, 0.75f),  // 紫色
                "R" => new Color(0f, 0.5f, 1f),       // 蓝色
                _ => new Color(0.6f, 0.6f, 0.6f)      // 灰色
            };

            _btnDrawAgain.clicked -= HandleDrawAgain;
            _btnDrawAgain.clicked += HandleDrawAgain;
        }

        public void ShowMessage(string msg)
        {
            _msgLabel.text = msg;
        }

        public void Hide() => _root.style.display = DisplayStyle.None;

        private void HandleDrawAgain() => OnDrawAgain?.Invoke();
    }
}

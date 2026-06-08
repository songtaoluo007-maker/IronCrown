// ============================================================================
// Presentation/GachaPanelController.cs — P2.1 改造为将领解锁面板
// 列出所有将军卡(稀有度色+名+技能+战功点价+已拥有/星级),点"解锁/升星"
// P2.1-fix: 不再引用 Domain，改用 Contracts DTO
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using IronCrown.Contracts;

namespace IronCrown.Presentation
{
    public sealed class GachaPanelController
    {
        private readonly VisualElement _root;
        private readonly Label _meritLabel;
        private readonly ScrollView _cardList;
        private readonly Button _btnClose;

        public GachaPanelController(VisualElement root)
        {
            _root = root.Q<VisualElement>("gacha-panel");
            _meritLabel = root.Q<Label>("merit-label") ?? root.Q<Label>("rarity-label");
            _cardList = root.Q<ScrollView>("card-list") ?? root.Q<ScrollView>("gacha-card-list");
            _btnClose = root.Q<Button>("btn-close");

            if (_btnClose != null) _btnClose.clicked += Hide;
        }

        /// <summary>点击"解锁/升星"时回调(cardId)</summary>
        public Action<string> OnUnlock { get; set; }

        /// <summary>显示将领解锁面板（P2.1-fix: 使用 Contracts DTO，不引用 Domain）</summary>
        public void Show(List<CommanderCardView> cards, int meritPoints)
        {
            _root.style.display = DisplayStyle.Flex;
            if (_meritLabel != null) _meritLabel.text = $"战功点: {meritPoints}";

            if (_cardList == null) return;
            _cardList.Clear();

            // 按稀有度排序: SSR→SR→R→N
            var rarityOrder = new Dictionary<string, int> { { "SSR", 0 }, { "SR", 1 }, { "R", 2 }, { "N", 3 } };
            var sorted = cards
                .Where(c => c.cardId != "general_test_basic")
                .OrderBy(c => rarityOrder.TryGetValue(c.rarity ?? "N", out var o) ? o : 4)
                .ThenBy(c => c.cardId)
                .ToList();

            foreach (var card in sorted)
            {
                var row = BuildCardRow(card);
                _cardList.Add(row);
            }
        }

        public void Hide() => _root.style.display = DisplayStyle.None;

        private VisualElement BuildCardRow(CommanderCardView card)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;

            // 稀有度色块
            var rarityColor = card.rarity switch
            {
                "SSR" => new Color(1f, 0.84f, 0f),
                "SR" => new Color(0.75f, 0.55f, 1f),
                "R" => new Color(0.3f, 0.69f, 0.95f),
                _ => new Color(0.6f, 0.6f, 0.6f)
            };
            var rarityBlock = new VisualElement();
            rarityBlock.style.width = 4;
            rarityBlock.style.height = 40;
            rarityBlock.style.backgroundColor = rarityColor;
            rarityBlock.style.marginRight = 8;
            row.Add(rarityBlock);

            // 名称 + 稀有度
            var nameCol = new VisualElement();
            nameCol.style.flexGrow = 1;
            var nameLabel = new Label($"{card.name} [{card.rarity}]");
            nameLabel.style.fontSize = 14;
            nameLabel.style.color = Color.white;
            nameCol.Add(nameLabel);

            // 技能描述
            if (!string.IsNullOrEmpty(card.skillDescription))
            {
                var skillLabel = new Label(card.skillDescription);
                skillLabel.style.fontSize = 11;
                skillLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                nameCol.Add(skillLabel);
            }
            row.Add(nameCol);

            // 星级
            if (card.owned)
            {
                var starLabel = new Label($"★{card.starLevel}");
                starLabel.style.fontSize = 14;
                starLabel.style.color = new Color(1f, 0.84f, 0f);
                starLabel.style.marginRight = 8;
                row.Add(starLabel);
            }

            // 按钮
            string btnText;
            bool btnEnabled;
            if (card.isMaxStar)
            {
                btnText = "已满星";
                btnEnabled = false;
            }
            else if (card.owned)
            {
                btnText = $"升星 ({card.starUpCost})";
                btnEnabled = card.canAfford;
            }
            else
            {
                btnText = $"解锁 ({card.unlockCost})";
                btnEnabled = card.canAfford;
            }

            var btn = new Button(() => OnUnlock?.Invoke(card.cardId)) { text = btnText };
            btn.SetEnabled(btnEnabled);
            btn.style.width = 90;
            row.Add(btn);

            return row;
        }
    }
}

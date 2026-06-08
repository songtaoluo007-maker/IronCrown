// ============================================================================
// Presentation/GachaPanelController.cs — P2.1 改造为将领解锁面板
// 列出所有将军卡(稀有度色+名+技能+战功点价+已拥有/星级),点"解锁/升星"
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using IronCrown.Contracts;
using IronCrown.Domain;

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

        /// <summary>显示将领解锁面板</summary>
        public void Show(List<CommanderConfig> allCards, int meritPoints,
            Dictionary<string, CommanderState> commanders, string ownerCountry, EconomyConfig eco)
        {
            _root.style.display = DisplayStyle.Flex;
            if (_meritLabel != null) _meritLabel.text = $"战功点: {meritPoints}";

            if (_cardList == null) return;
            _cardList.Clear();

            // 按稀有度排序: SSR→SR→R→N
            var rarityOrder = new Dictionary<string, int> { { "SSR", 0 }, { "SR", 1 }, { "R", 2 }, { "N", 3 } };
            var sorted = allCards
                .Where(c => c.id != "general_test_basic")
                .OrderBy(c => rarityOrder.TryGetValue(c.rarity ?? "N", out var o) ? o : 4)
                .ThenBy(c => c.id)
                .ToList();

            foreach (var card in sorted)
            {
                var owned = commanders.Values
                    .FirstOrDefault(c => c.ownerCountry == ownerCountry && c.generalCardId == card.id);

                int cost = GetDisplayCost(card, owned, eco);
                bool canAfford = meritPoints >= cost;
                bool isMaxStar = owned != null && owned.starLevel >= eco.maxStarLevel;

                var row = BuildCardRow(card, owned, cost, canAfford, isMaxStar);
                _cardList.Add(row);
            }
        }

        public void Hide() => _root.style.display = DisplayStyle.None;

        private int GetDisplayCost(CommanderConfig card, CommanderState owned, EconomyConfig eco)
        {
            int baseCost = card.rarity switch
            {
                "SSR" => eco.meritUnlockCostSSR,
                "SR" => eco.meritUnlockCostSR,
                "R" => eco.meritUnlockCostR,
                _ => eco.meritUnlockCostN
            };

            if (owned != null)
            {
                int mult = (owned.starLevel + 1) * eco.meritStarUpMultiplier / 100;
                return baseCost * mult;
            }
            return baseCost;
        }

        private VisualElement BuildCardRow(CommanderConfig card, CommanderState owned,
            int cost, bool canAfford, bool isMaxStar)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.91f, 0.92f, 0.93f);

            // 稀有度标签
            var rarityLabel = new Label(card.rarity ?? "N");
            rarityLabel.style.width = 36;
            rarityLabel.style.fontSize = 11;
            rarityLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            rarityLabel.style.color = Color.white;
            rarityLabel.style.borderTopLeftRadius = 4;
            rarityLabel.style.borderBottomLeftRadius = 4;
            rarityLabel.style.borderTopRightRadius = 4;
            rarityLabel.style.borderBottomRightRadius = 4;
            rarityLabel.style.backgroundColor = card.rarity switch
            {
                "SSR" => new Color(1f, 0.84f, 0f),
                "SR" => new Color(0.75f, 0f, 0.75f),
                "R" => new Color(0f, 0.5f, 1f),
                _ => new Color(0.6f, 0.6f, 0.6f)
            };
            row.Add(rarityLabel);

            // 名称
            var nameLabel = new Label(card.name);
            nameLabel.style.marginLeft = 8;
            nameLabel.style.fontSize = 13;
            nameLabel.style.flexGrow = 1;
            row.Add(nameLabel);

            // 星级（已拥有）
            if (owned != null)
            {
                var starLabel = new Label($"★{owned.starLevel}");
                starLabel.style.marginLeft = 4;
                starLabel.style.fontSize = 12;
                starLabel.style.color = new Color(1f, 0.84f, 0f);
                row.Add(starLabel);
            }

            // 解锁/升星按钮
            var btn = new Button();
            btn.style.marginLeft = 8;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.paddingTop = 4;
            btn.style.paddingBottom = 4;
            btn.style.borderTopLeftRadius = 6;
            btn.style.borderBottomLeftRadius = 6;
            btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomRightRadius = 6;

            if (isMaxStar)
            {
                btn.text = "满星(+5经验)";
                btn.SetEnabled(true);
                btn.style.backgroundColor = new Color(0.95f, 0.9f, 0.7f);
                btn.style.color = new Color(0.5f, 0.4f, 0.2f);
            }
            else if (owned != null)
            {
                btn.text = $"升星 ⭐{cost}";
                btn.SetEnabled(canAfford);
                btn.style.backgroundColor = canAfford ? new Color(0.2f, 0.68f, 0.33f) : new Color(0.9f, 0.9f, 0.9f);
                btn.style.color = canAfford ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }
            else
            {
                btn.text = $"解锁 ⭐{cost}";
                btn.SetEnabled(canAfford);
                btn.style.backgroundColor = canAfford ? new Color(0.1f, 0.45f, 0.91f) : new Color(0.9f, 0.9f, 0.9f);
                btn.style.color = canAfford ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }

            var cardId = card.id;
            btn.clicked += () => OnUnlock?.Invoke(cardId);
            row.Add(btn);

            return row;
        }
    }
}

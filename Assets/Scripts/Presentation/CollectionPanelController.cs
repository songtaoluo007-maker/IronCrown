// ============================================================================
// Presentation/CollectionPanelController.cs — C17 收藏页
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using IronCrown.Contracts;

namespace IronCrown.Presentation
{
    public sealed class CollectionPanelController
    {
        private readonly VisualElement _root;
        private readonly ScrollView _commanderList;
        private readonly Button _btnClose;

        public CollectionPanelController(VisualElement root)
        {
            _root = root.Q<VisualElement>("collection-panel");
            _commanderList = _root.Q<ScrollView>("commander-list");
            _btnClose = _root.Q<Button>("btn-close");
            _btnClose.clicked += Hide;
        }

        public void Show(List<CommanderView> commanders)
        {
            _root.style.display = DisplayStyle.Flex;
            _commanderList.Clear();

            // 按稀有度排序: SSR > SR > R > N，同级按星级降序
            var sorted = commanders.OrderByDescending(c => StarToSortKey(c.starLevel))
                                   .ThenByDescending(c => c.victories)
                                   .ToList();

            if (sorted.Count == 0)
            {
                _commanderList.Add(new Label("暂无将领，去抽卡吧！") { style = { fontSize = 16 } });
                return;
            }

            foreach (var cmdr in sorted)
            {
                var row = new VisualElement();
                row.AddToClassList("commander-row");

                var stars = new string('★', cmdr.starLevel) + new string('☆', 5 - cmdr.starLevel);
                var rankStr = cmdr.rankName ?? "";

                var nameLabel = new Label($"{cmdr.name} [{rankStr}] {stars}");
                nameLabel.AddToClassList("cmdr-name");
                row.Add(nameLabel);

                var statsLabel = new Label($"攻{cmdr.baseAttack} 防{cmdr.baseDefense} | 指挥 {cmdr.commandedDivisions}/{cmdr.maxDivisions} 师 | 胜场 {cmdr.victories}");
                statsLabel.AddToClassList("cmdr-stats");
                row.Add(statsLabel);

                if (!string.IsNullOrEmpty(cmdr.buffDescription))
                {
                    var buffLabel = new Label(cmdr.buffDescription);
                    buffLabel.AddToClassList("cmdr-buff");
                    row.Add(buffLabel);
                }

                _commanderList.Add(row);
            }
        }

        public void Hide() => _root.style.display = DisplayStyle.None;

        private static int StarToSortKey(int star) => star; // 简化，后续加稀有度权重
    }
}

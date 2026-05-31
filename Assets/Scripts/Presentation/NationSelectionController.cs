// ============================================================================
// Presentation/NationSelectionController.cs — 选国覆盖面板控制器
// 程序化创建 UI，不依赖额外 UXML 文件
// ============================================================================

using IronCrown.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IronCrown.Presentation
{
    public sealed class NationSelectionController
    {
        private readonly GameSessionService _session;
        private VisualElement _overlay;

        public NationSelectionController(GameSessionService session)
        {
            _session = session;
        }

        public void Bind(VisualElement root)
        {
            // 已选国则不显示
            if (!string.IsNullOrEmpty(_session.PlayerCountryId))
                return;

            // 创建覆盖面板
            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 0.95f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;

            var title = new Label("选择你的国家");
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 24;
            _overlay.Add(title);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexWrap = Wrap.Wrap;
            container.style.justifyContent = Justify.Center;
            container.style.width = Length.Percent(80);

            var view = _session.GetWorldView();
            if (view != null && view.countries != null)
            {
                foreach (var c in view.countries)
                {
                    var card = CreateCard(c);
                    container.Add(card);
                }
            }

            _overlay.Add(container);
            root.Add(_overlay);
        }

        private VisualElement CreateCard(Contracts.CountryView c)
        {
            var card = new VisualElement();
            card.style.width = 240;
            card.style.height = 160;
            card.style.marginLeft = 12;
            card.style.marginRight = 12;
            card.style.marginTop = 12;
            card.style.marginBottom = 12;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.borderTopWidth = 2;
            card.style.borderBottomWidth = 2;
            card.style.borderLeftWidth = 2;
            card.style.borderRightWidth = 2;
            card.style.borderTopColor = new Color(1, 1, 1, 0.3f);
            card.style.borderBottomColor = new Color(1, 1, 1, 0.3f);
            card.style.borderLeftColor = new Color(1, 1, 1, 0.3f);
            card.style.borderRightColor = new Color(1, 1, 1, 0.3f);
            card.style.backgroundColor = new Color(0.16f, 0.17f, 0.2f, 0.9f);

            // 配色边框
            if (ColorUtility.TryParseHtmlString(c.color, out var color))
                card.style.borderLeftColor = color;

            var nameLabel = new Label(c.name);
            nameLabel.style.fontSize = 18;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = Color.white;
            nameLabel.style.marginBottom = 8;
            card.Add(nameLabel);

            var idLabel = new Label($"ID: {c.id}");
            idLabel.style.fontSize = 12;
            idLabel.style.color = new Color(0.8f, 0.82f, 0.86f, 0.8f);
            idLabel.style.marginBottom = 4;
            card.Add(idLabel);

            var capitalLabel = new Label($"首都: {c.capitalProvinceId ?? "无"}");
            capitalLabel.style.fontSize = 12;
            capitalLabel.style.color = new Color(0.8f, 0.82f, 0.86f, 0.8f);
            capitalLabel.style.marginBottom = 4;
            card.Add(capitalLabel);

            var treasuryLabel = new Label($"初始国库: {c.treasury}");
            treasuryLabel.style.fontSize = 12;
            treasuryLabel.style.color = new Color(0.8f, 0.82f, 0.86f, 0.8f);
            card.Add(treasuryLabel);

            // 悬停效果
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                card.style.borderTopColor = new Color(1, 1, 1, 0.8f);
                card.style.borderBottomColor = new Color(1, 1, 1, 0.8f);
                card.style.borderRightColor = new Color(1, 1, 1, 0.8f);
                card.style.backgroundColor = new Color(0.22f, 0.24f, 0.27f, 0.95f);
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                card.style.borderTopColor = new Color(1, 1, 1, 0.3f);
                card.style.borderBottomColor = new Color(1, 1, 1, 0.3f);
                card.style.borderRightColor = new Color(1, 1, 1, 0.3f);
                if (ColorUtility.TryParseHtmlString(c.color, out var clr))
                    card.style.borderLeftColor = clr;
                card.style.backgroundColor = new Color(0.16f, 0.17f, 0.2f, 0.9f);
            });

            var countryId = c.id;
            card.RegisterCallback<ClickEvent>(_ =>
            {
                _session.SetPlayerCountry(countryId);
                if (_overlay != null)
                    _overlay.style.display = DisplayStyle.None;
            });

            return card;
        }
    }
}

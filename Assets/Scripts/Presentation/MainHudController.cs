// ============================================================================
// Presentation/Views/MainHudController.cs — HUD 控制器（纯逻辑）
// 经 GameSessionService + WorldView 驱动，不持有运行时状态
// ============================================================================

using System.Text;
using IronCrown.Application;
using IronCrown.Contracts;
using UnityEngine.UIElements;

namespace IronCrown.Presentation
{
    /// <summary>
    /// 主 HUD 控制器。构造注入 GameSessionService + IEventPublisher。
    /// 通过 Bind(root) 绑定 UI Toolkit 元素，Render() 刷新显示。
    /// </summary>
    public sealed class MainHudController
    {
        private readonly GameSessionService _session;
        private readonly IEventPublisher _events;

        private Label _turnLabel;
        private Button _advanceBtn;
        private VisualElement _countryList;

        public MainHudController(GameSessionService session, IEventPublisher events)
        {
            _session = session;
            _events = events;
        }

        /// <summary>绑定 UI 元素并首次渲染</summary>
        public void Bind(VisualElement root)
        {
            _turnLabel = root.Q<Label>("turn-label");
            _advanceBtn = root.Q<Button>("advance-btn");
            _countryList = root.Q<ScrollView>("country-list");

            if (_advanceBtn != null)
                _advanceBtn.RegisterCallback<ClickEvent>(OnAdvanceClicked);

            _events.Subscribe<TurnStartEvent>(_ => Render());
            _events.Subscribe<TurnEndEvent>(_ => Render());

            Render();
        }

        /// <summary>取消绑定</summary>
        public void Unbind()
        {
            if (_advanceBtn != null)
                _advanceBtn.UnregisterCallback<ClickEvent>(OnAdvanceClicked);
        }

        private void OnAdvanceClicked(ClickEvent evt)
        {
            _session.AdvancePhase();
            Render();
        }

        /// <summary>刷新全部 UI</summary>
        public void Render()
        {
            var vm = _session.GetWorldView();
            if (vm == null) return;

            // 顶栏
            if (_turnLabel != null)
                _turnLabel.text = FormatHeader(vm);

            // 国家列表
            if (_countryList == null) return;
            _countryList.Clear();

            foreach (var c in vm.countries)
            {
                var row = new Label(FormatCountryRow(c));
                row.AddToClassList("country-row");
                _countryList.Add(row);
            }
        }

        // ===== 纯展示逻辑（可单元测试） =====

        public static string FormatHeader(WorldView w)
        {
            return $"回合 {w.turn} · {w.phase}";
        }

        public static string FormatCountryRow(CountryView c)
        {
            var sb = new StringBuilder();
            sb.Append(c.name);
            sb.Append("  |  国库: ");
            sb.Append(c.treasury);
            sb.Append("  |  稳定: ");
            sb.Append(c.stability);
            sb.Append("  |  装备: ");
            sb.Append(c.equipmentStockpile);

            if (c.resources != null && c.resources.Count > 0)
            {
                sb.Append("  |  ");
                bool first = true;
                foreach (var kv in c.resources)
                {
                    if (!first) sb.Append(" / ");
                    sb.Append(kv.Key);
                    sb.Append(": ");
                    sb.Append(kv.Value);
                    first = false;
                }
            }

            return sb.ToString();
        }
    }
}

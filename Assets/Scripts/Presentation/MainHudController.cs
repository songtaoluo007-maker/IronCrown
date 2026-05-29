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
    public sealed class MainHudController
    {
        private readonly GameSessionService _session;
        private readonly IEventPublisher _events;

        private Label _turnLabel;
        private Label _playerLabel;
        private Label _statusLabel;
        private Button _advanceBtn;
        private Button _buildCivilianBtn;
        private Button _buildMilitaryBtn;
        private VisualElement _countryList;

        public MainHudController(GameSessionService session, IEventPublisher events)
        {
            _session = session;
            _events = events;
        }

        public void Bind(VisualElement root)
        {
            _turnLabel = root.Q<Label>("turn-label");
            _playerLabel = root.Q<Label>("player-label");
            _statusLabel = root.Q<Label>("status-label");
            _advanceBtn = root.Q<Button>("advance-btn");
            _buildCivilianBtn = root.Q<Button>("build-civilian-btn");
            _buildMilitaryBtn = root.Q<Button>("build-military-btn");
            _countryList = root.Q<ScrollView>("country-list");

            if (_advanceBtn != null)
                _advanceBtn.RegisterCallback<ClickEvent>(_ => Advance());
            if (_buildCivilianBtn != null)
                _buildCivilianBtn.RegisterCallback<ClickEvent>(_ => BuildCivilian());
            if (_buildMilitaryBtn != null)
                _buildMilitaryBtn.RegisterCallback<ClickEvent>(_ => BuildMilitary());

            _events.Subscribe<TurnStartEvent>(_ => Render());
            _events.Subscribe<TurnEndEvent>(_ => Render());

            Render();
        }

        public void Unbind()
        {
            if (_advanceBtn != null)
                _advanceBtn.UnregisterCallback<ClickEvent>(_ => Advance());
            if (_buildCivilianBtn != null)
                _buildCivilianBtn.UnregisterCallback<ClickEvent>(_ => BuildCivilian());
            if (_buildMilitaryBtn != null)
                _buildMilitaryBtn.UnregisterCallback<ClickEvent>(_ => BuildMilitary());
        }

        public void Advance()
        {
            _session.AdvancePhase();
            Render();
        }

        public void BuildCivilian()
        {
            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.BuildCivilianFactory,
                countryId = _session.PlayerCountryId
            });
            if (result.accepted)
                ShowStatus("已下令建造民用厂");
            else
                ShowStatus($"被拒: {result.reason}");
            Render();
        }

        public void BuildMilitary()
        {
            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.BuildMilitaryFactory,
                countryId = _session.PlayerCountryId
            });
            if (result.accepted)
                ShowStatus("已下令建造军用厂");
            else
                ShowStatus($"被拒: {result.reason}");
            Render();
        }

        public void SelectCountry(string countryId)
        {
            _session.SetPlayerCountry(countryId);
            Render();
        }

        private void ShowStatus(string msg)
        {
            if (_statusLabel != null)
                _statusLabel.text = msg;
        }

        public void Render()
        {
            var vm = _session.GetWorldView();
            if (vm == null) return;

            if (_turnLabel != null)
                _turnLabel.text = FormatHeader(vm);

            if (_playerLabel != null)
                _playerLabel.text = $"玩家: {vm.playerCountryId ?? "(无)"}";

            if (_countryList == null) return;
            _countryList.Clear();

            foreach (var c in vm.countries)
            {
                bool isPlayer = c.id == vm.playerCountryId;
                var row = new Label(FormatCountryRow(c, isPlayer));
                row.AddToClassList("country-row");
                row.focusable = true;
                row.pickingMode = PickingMode.Position;
                if (isPlayer)
                    row.AddToClassList("country-row-player");
                var countryId = c.id;
                row.RegisterCallback<ClickEvent>(_ => SelectCountry(countryId));
                _countryList.Add(row);
            }
        }

        public static string FormatHeader(WorldView w)
        {
            return $"回合 {w.turn} · {w.phase}";
        }

        public static string FormatCountryRow(CountryView c, bool isPlayer = false)
        {
            var sb = new StringBuilder();
            if (isPlayer) sb.Append("★ ");
            sb.Append(c.name);
            sb.Append("  |  国库: ");
            sb.Append(c.treasury);
            sb.Append("  |  稳定: ");
            sb.Append(c.stability);
            sb.Append("  |  装备: ");
            sb.Append(c.equipmentStockpile);

            if (c.constructionQueueCount > 0)
            {
                sb.Append("  |  在建: ");
                sb.Append(c.constructionQueueCount);
            }

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

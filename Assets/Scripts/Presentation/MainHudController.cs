// ============================================================================
// Presentation/Views/MainHudController.cs — HUD 控制器（纯逻辑）
// 经 GameSessionService + WorldView 驱动，不持有运行时状态
// ============================================================================

using System.Text;
using IronCrown.Application;
using IronCrown.Contracts;
using UnityEngine;
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
        private Button _taxUpBtn;
        private Button _taxDownBtn;
        private Button _civilUpBtn;
        private Button _civilDownBtn;
        private Label _taxLevelLabel;
        private Label _civilLevelLabel;
        private VisualElement _mapArea;
        private Label _provinceDetailLabel;
        private VisualElement _countryList;

        // Stored callbacks for proper Unregister
        private EventCallback<ClickEvent> _onAdvance;
        private EventCallback<ClickEvent> _onBuildCivilian;
        private EventCallback<ClickEvent> _onBuildMilitary;
        private EventCallback<ClickEvent> _onTaxUp;
        private EventCallback<ClickEvent> _onTaxDown;
        private EventCallback<ClickEvent> _onCivilUp;
        private EventCallback<ClickEvent> _onCivilDown;

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
            _taxUpBtn = root.Q<Button>("tax-up-btn");
            _taxDownBtn = root.Q<Button>("tax-down-btn");
            _civilUpBtn = root.Q<Button>("civil-up-btn");
            _civilDownBtn = root.Q<Button>("civil-down-btn");
            _taxLevelLabel = root.Q<Label>("tax-level-label");
            _civilLevelLabel = root.Q<Label>("civil-level-label");
            _mapArea = root.Q<VisualElement>("map-area");
            _provinceDetailLabel = root.Q<Label>("province-detail-label");
            _countryList = root.Q<ScrollView>("country-list");

            _onAdvance = _ => Advance();
            _onBuildCivilian = _ => BuildCivilian();
            _onBuildMilitary = _ => BuildMilitary();
            _onTaxUp = _ => SetTax(1);
            _onTaxDown = _ => SetTax(-1);
            _onCivilUp = _ => SetCivil(1);
            _onCivilDown = _ => SetCivil(-1);

            if (_advanceBtn != null) _advanceBtn.RegisterCallback(_onAdvance);
            if (_buildCivilianBtn != null) _buildCivilianBtn.RegisterCallback(_onBuildCivilian);
            if (_buildMilitaryBtn != null) _buildMilitaryBtn.RegisterCallback(_onBuildMilitary);
            if (_taxUpBtn != null) _taxUpBtn.RegisterCallback(_onTaxUp);
            if (_taxDownBtn != null) _taxDownBtn.RegisterCallback(_onTaxDown);
            if (_civilUpBtn != null) _civilUpBtn.RegisterCallback(_onCivilUp);
            if (_civilDownBtn != null) _civilDownBtn.RegisterCallback(_onCivilDown);

            _events.Subscribe<TurnStartEvent>(_ => Render());
            _events.Subscribe<TurnEndEvent>(_ => Render());

            Render();
        }

        public void Unbind()
        {
            if (_advanceBtn != null) _advanceBtn.UnregisterCallback(_onAdvance);
            if (_buildCivilianBtn != null) _buildCivilianBtn.UnregisterCallback(_onBuildCivilian);
            if (_buildMilitaryBtn != null) _buildMilitaryBtn.UnregisterCallback(_onBuildMilitary);
            if (_taxUpBtn != null) _taxUpBtn.UnregisterCallback(_onTaxUp);
            if (_taxDownBtn != null) _taxDownBtn.UnregisterCallback(_onTaxDown);
            if (_civilUpBtn != null) _civilUpBtn.UnregisterCallback(_onCivilUp);
            if (_civilDownBtn != null) _civilDownBtn.UnregisterCallback(_onCivilDown);
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

        public void SetTax(int delta)
        {
            var view = _session.GetWorldView();
            if (view == null) return;
            var player = view.countries.Find(c => c.id == view.playerCountryId);
            if (player == null) return;
            int newLevel = System.Math.Clamp(player.taxLevel + delta, 0, 2);
            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.SetTaxLevel,
                countryId = _session.PlayerCountryId,
                level = newLevel
            });
            if (result.accepted)
            {
                string[] names = { "低", "中", "高" };
                ShowStatus($"税率: {names[newLevel]}");
            }
            else
                ShowStatus($"被拒: {result.reason}");
            Render();
        }

        public void SetCivil(int delta)
        {
            var view = _session.GetWorldView();
            if (view == null) return;
            var player = view.countries.Find(c => c.id == view.playerCountryId);
            if (player == null) return;
            int newLevel = System.Math.Clamp(player.civilLevel + delta, 0, 2);
            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.SetCivilLevel,
                countryId = _session.PlayerCountryId,
                level = newLevel
            });
            if (result.accepted)
            {
                string[] names = { "紧缩", "正常", "宽裕" };
                ShowStatus($"民生: {names[newLevel]}");
            }
            else
                ShowStatus($"被拒: {result.reason}");
            Render();
        }

        public void SelectCountry(string countryId)
        {
            _session.SetPlayerCountry(countryId);
            Render();
        }

        public void SelectProvince(string provinceId)
        {
            _session.SelectProvince(provinceId);
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

            // 内政档位显示
            var playerView = vm.countries.Find(c => c.id == vm.playerCountryId);
            if (playerView != null)
            {
                string[] taxNames = { "低", "中", "高" };
                string[] civilNames = { "紧缩", "正常", "宽裕" };
                if (_taxLevelLabel != null)
                    _taxLevelLabel.text = taxNames[System.Math.Clamp(playerView.taxLevel, 0, 2)];
                if (_civilLevelLabel != null)
                    _civilLevelLabel.text = civilNames[System.Math.Clamp(playerView.civilLevel, 0, 2)];
            }

            // 地图渲染
            RenderMap(vm);

            // 省份详情
            RenderProvinceDetail(vm);

            // 国家列表
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

        private void RenderMap(WorldView vm)
        {
            if (_mapArea == null || vm.provinces == null) return;
            _mapArea.Clear();

            const int cellSize = 110;

            foreach (var p in vm.provinces)
            {
                var tile = new VisualElement();
                tile.AddToClassList("province-tile");
                tile.style.position = Position.Absolute;
                tile.style.left = p.gridX * cellSize;
                tile.style.top = p.gridY * cellSize;

                // 解析国家配色
                if (UnityEngine.ColorUtility.TryParseHtmlString(p.ownerColor, out var color))
                    tile.style.backgroundColor = color;

                bool isSelected = p.id == vm.selectedProvinceId;
                if (isSelected)
                    tile.AddToClassList("province-tile-selected");

                var label = new Label(p.name);
                label.AddToClassList("province-tile-label");
                tile.Add(label);

                // 驻军标记
                if (p.garrisonCount > 0)
                {
                    var garrisonBadge = new Label($"⚔{p.garrisonCount}");
                    garrisonBadge.AddToClassList("province-garrison-badge");
                    tile.Add(garrisonBadge);
                }

                var provinceId = p.id;
                tile.RegisterCallback<ClickEvent>(_ => SelectProvince(provinceId));
                _mapArea.Add(tile);
            }
        }

        private void RenderProvinceDetail(WorldView vm)
        {
            if (_provinceDetailLabel == null) return;

            if (string.IsNullOrEmpty(vm.selectedProvinceId))
            {
                _provinceDetailLabel.text = "点击地图省份查看";
                return;
            }

            var pv = vm.provinces.Find(p => p.id == vm.selectedProvinceId);
            if (pv == null)
            {
                _provinceDetailLabel.text = "省份未找到";
                return;
            }

            var sb = new StringBuilder();
            sb.Append(pv.name);
            if (pv.isCapital) sb.Append(" [首都]");
            sb.Append($"  |  {pv.terrain}");
            sb.Append($"  |  归属: {pv.ownerCountry}");
            sb.Append($"  |  基建: {pv.infrastructure}");
            sb.Append($"  |  人口: {FormatPopulation(pv.population)}");
            sb.Append($"  |  胜利点: {pv.victoryPoint}");
            if (pv.resourceOutput != null && pv.resourceOutput.Length > 0)
                sb.Append($"  |  产出: {string.Join(", ", pv.resourceOutput)}");
            if (pv.neighbors != null && pv.neighbors.Length > 0)
                sb.Append($"  |  邻接: {string.Join(", ", pv.neighbors)}");
            sb.Append($"  |  驻军: {pv.garrisonCount} 支");

            _provinceDetailLabel.text = sb.ToString();
        }

        private static string FormatPopulation(int pop)
        {
            if (pop >= 1000000) return $"{pop / 1000000.0:F1}M";
            if (pop >= 1000) return $"{pop / 1000}K";
            return pop.ToString();
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

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
        private Button _buildInfantryBtn;
        private Button _taxUpBtn;
        private Button _taxDownBtn;
        private Button _civilUpBtn;
        private Button _civilDownBtn;
        private Label _taxLevelLabel;
        private Label _civilLevelLabel;
        private VisualElement _mapArea;
        private Label _provinceDetailLabel;
        private VisualElement _countryList;

        // C5: 外交
        private Button _offerPeaceBtn;

        // C15a: 将领
        private Button _recruitCommanderBtn;
        private Label _warExhaustionLabel;
        private Label _gachaTicketsLabel; // C17

        // C9b: HUD 国家状况
        private Label _treasuryLabel;
        private Label _stabilityLabel;
        private Label _warSupportLabel;

        // Stored callbacks for proper Unregister
        private EventCallback<ClickEvent> _onAdvance;
        private EventCallback<ClickEvent> _onBuildCivilian;
        private EventCallback<ClickEvent> _onBuildMilitary;
        private EventCallback<ClickEvent> _onBuildInfantry;
        private EventCallback<ClickEvent> _onTaxUp;
        private EventCallback<ClickEvent> _onTaxDown;
        private EventCallback<ClickEvent> _onCivilUp;
        private EventCallback<ClickEvent> _onCivilDown;
        private EventCallback<ClickEvent> _onOfferPeace;
        private EventCallback<ClickEvent> _onRecruitCommander;

        // C17: 抽卡/商城/收藏
        private Button _gachaDrawBtn;
        private Button _collectionBtn;
        private Button _shopBtn;
        private EventCallback<ClickEvent> _onGachaDraw;
        private EventCallback<ClickEvent> _onCollection;
        private EventCallback<ClickEvent> _onShop;

        // C15a: 将领任命
        private Button _assignCommanderBtn;
        private Button _unassignCommanderBtn;
        private VisualElement _commanderActions;
        private EventCallback<ClickEvent> _onAssignCommander;
        private EventCallback<ClickEvent> _onUnassignCommander;

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
            _buildInfantryBtn = root.Q<Button>("build-infantry-btn");
            _taxUpBtn = root.Q<Button>("tax-up-btn");
            _taxDownBtn = root.Q<Button>("tax-down-btn");
            _civilUpBtn = root.Q<Button>("civil-up-btn");
            _civilDownBtn = root.Q<Button>("civil-down-btn");
            _taxLevelLabel = root.Q<Label>("tax-level-label");
            _civilLevelLabel = root.Q<Label>("civil-level-label");
            _mapArea = root.Q<VisualElement>("map-area");
            _provinceDetailLabel = root.Q<Label>("province-detail-label");
            _countryList = root.Q<ScrollView>("country-list");

            // C5: 外交
            _offerPeaceBtn = root.Q<Button>("offer-peace-btn");
            _recruitCommanderBtn = root.Q<Button>("recruit-commander-btn");
            _warExhaustionLabel = root.Q<Label>("war-exhaustion-label");
            _gachaTicketsLabel = root.Q<Label>("gacha-tickets-label");

            // C9b: HUD 国家状况
            _treasuryLabel = root.Q<Label>("treasury-label");
            _stabilityLabel = root.Q<Label>("stability-label");
            _warSupportLabel = root.Q<Label>("war-support-label");

            _onAdvance = _ => Advance();
            _onBuildCivilian = _ => BuildCivilian();
            _onBuildMilitary = _ => BuildMilitary();
            _onBuildInfantry = _ => BuildInfantry();
            _onTaxUp = _ => SetTax(1);
            _onTaxDown = _ => SetTax(-1);
            _onCivilUp = _ => SetCivil(1);
            _onCivilDown = _ => SetCivil(-1);
            _onOfferPeace = _ => OfferPeace();
            _onRecruitCommander = _ => RecruitCommander();

            // C17: 抽卡/商城/收藏
            _gachaDrawBtn = root.Q<Button>("gacha-draw-btn");
            _collectionBtn = root.Q<Button>("collection-btn");
            _shopBtn = root.Q<Button>("shop-btn");
            _onGachaDraw = _ => OnGachaDraw();
            _onCollection = _ => OnCollectionOpen();
            _onShop = _ => OnShopOpen();

            // C15a: 将领任命
            _commanderActions = root.Q<VisualElement>("commander-actions");
            _assignCommanderBtn = root.Q<Button>("assign-commander-btn");
            _unassignCommanderBtn = root.Q<Button>("unassign-commander-btn");
            _onAssignCommander = _ => AssignCommanderToUnit();
            _onUnassignCommander = _ => UnassignCommanderFromUnit();

            if (_advanceBtn != null) _advanceBtn.RegisterCallback(_onAdvance);
            if (_buildCivilianBtn != null) _buildCivilianBtn.RegisterCallback(_onBuildCivilian);
            if (_buildMilitaryBtn != null) _buildMilitaryBtn.RegisterCallback(_onBuildMilitary);
            if (_buildInfantryBtn != null) _buildInfantryBtn.RegisterCallback(_onBuildInfantry);
            if (_taxUpBtn != null) _taxUpBtn.RegisterCallback(_onTaxUp);
            if (_taxDownBtn != null) _taxDownBtn.RegisterCallback(_onTaxDown);
            if (_civilUpBtn != null) _civilUpBtn.RegisterCallback(_onCivilUp);
            if (_civilDownBtn != null) _civilDownBtn.RegisterCallback(_onCivilDown);
            if (_offerPeaceBtn != null) _offerPeaceBtn.RegisterCallback(_onOfferPeace);
            if (_recruitCommanderBtn != null) _recruitCommanderBtn.RegisterCallback(_onRecruitCommander);
            if (_gachaDrawBtn != null) _gachaDrawBtn.RegisterCallback(_onGachaDraw);
            if (_collectionBtn != null) _collectionBtn.RegisterCallback(_onCollection);
            if (_shopBtn != null) _shopBtn.RegisterCallback(_onShop);
            if (_assignCommanderBtn != null) _assignCommanderBtn.RegisterCallback(_onAssignCommander);
            if (_unassignCommanderBtn != null) _unassignCommanderBtn.RegisterCallback(_onUnassignCommander);

            _events.Subscribe<TurnStartEvent>(_ => Render());
            _events.Subscribe<TurnEndEvent>(_ => Render());
            _events.Subscribe<BattleInitiatedEvent>(e =>
            {
                ShowStatus($"⚔ {e.attackerUnitId} 开赴 {e.provinceId} 战场");
                Render();
            });
            _events.Subscribe<BattleConcludedEvent>(e =>
            {
                if (e.winnerKind == "Attacker")
                    ShowStatus($"⚔ 攻克 {e.provinceId}！（{e.turnsElapsed} 回合）");
                else if (e.winnerKind == "Defender")
                    ShowStatus($"⚔ 攻势受挫于 {e.provinceId}（{e.turnsElapsed} 回合）");
                else
                    ShowStatus($"⚔ {e.provinceId} 两败俱伤");
                Render();
            });
            _events.Subscribe<ProvinceOccupiedEvent>(e =>
            {
                ShowStatus($"旗 进驻 {e.provinceId}（无抵抗）");
                Render();
            });
            _events.Subscribe<GameOverEvent>(e =>
            {
                if (e.result == "Victory")
                    ShowStatus("🎉 胜利！占领所有敌方首都");
                else
                    ShowStatus("💀 失败！首都已失守");
                if (_advanceBtn != null) _advanceBtn.SetEnabled(false);
                if (_statusLabel != null) _statusLabel.AddToClassList("status-game-over");
                Render();
            });

            // C5: 停战事件
            _events.Subscribe<PeaceOfferedEvent>(e =>
            {
                if (e.accepted)
                    ShowStatus($"☮ 停战达成：{e.fromCountry} ↔ {e.toCountry}");
                else
                    ShowStatus($"停战被拒：{e.reason}");
                Render();
            });
            _events.Subscribe<PeaceConcludedEvent>(e =>
            {
                ShowStatus($"☮ 和平降临：{e.countryA} ↔ {e.countryB}（回合 {e.atTurn}）");
                Render();
            });

            // C13: 溃退事件
            _events.Subscribe<UnitRetreatedEvent>(e =>
            {
                ShowStatus($"🏃 {e.unitId} 溃退至 {e.retreatProvinceId}（恢复 {1} 回合）");
                Render();
            });

            // C14: 补给耗尽消灭事件
            _events.Subscribe<UnitDestroyedEvent>(e =>
            {
                if (e.cause == "supply_starved")
                {
                    ShowStatus($"💀 {e.unitId} 补给耗尽，全军覆没");
                    Render();
                }
            });

            Render();
        }

        public void Unbind()
        {
            if (_advanceBtn != null) _advanceBtn.UnregisterCallback(_onAdvance);
            if (_buildCivilianBtn != null) _buildCivilianBtn.UnregisterCallback(_onBuildCivilian);
            if (_buildMilitaryBtn != null) _buildMilitaryBtn.UnregisterCallback(_onBuildMilitary);
            if (_buildInfantryBtn != null) _buildInfantryBtn.UnregisterCallback(_onBuildInfantry);
            if (_taxUpBtn != null) _taxUpBtn.UnregisterCallback(_onTaxUp);
            if (_taxDownBtn != null) _taxDownBtn.UnregisterCallback(_onTaxDown);
            if (_civilUpBtn != null) _civilUpBtn.UnregisterCallback(_onCivilUp);
            if (_civilDownBtn != null) _civilDownBtn.UnregisterCallback(_onCivilDown);
            if (_offerPeaceBtn != null) _offerPeaceBtn.UnregisterCallback(_onOfferPeace);
            if (_recruitCommanderBtn != null) _recruitCommanderBtn.UnregisterCallback(_onRecruitCommander);
            if (_gachaDrawBtn != null) _gachaDrawBtn.UnregisterCallback(_onGachaDraw);
            if (_collectionBtn != null) _collectionBtn.UnregisterCallback(_onCollection);
            if (_shopBtn != null) _shopBtn.UnregisterCallback(_onShop);
            if (_assignCommanderBtn != null) _assignCommanderBtn.UnregisterCallback(_onAssignCommander);
            if (_unassignCommanderBtn != null) _unassignCommanderBtn.UnregisterCallback(_onUnassignCommander);
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

        public void BuildInfantry()
        {
            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.BuildUnit,
                countryId = _session.PlayerCountryId,
                unitType = "infantry_division_basic"
            });
            if (result.accepted)
                ShowStatus("已下令训练步兵师");
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

        public void RecruitCommander()
        {
            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.RecruitCommander,
                countryId = _session.PlayerCountryId,
                configId = "general_test_basic"
            });
            if (result.accepted)
                ShowStatus("已招募将领");
            else
                ShowStatus($"被拒: {result.reason}");
            Render();
        }

        // ================================================================
        // C17: 抽卡 / 收藏 / 商城
        // ================================================================

        private void OnGachaDraw()
        {
            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.DrawCard,
                countryId = _session.PlayerCountryId
            });
            if (result.accepted)
                ShowStatus("抽卡成功！");
            else
                ShowStatus($"被拒: {result.reason}");
            Render();
        }

        private void OnCollectionOpen()
        {
            var view = _session.GetWorldView();
            if (view == null) return;
            var commanders = view.commanders ?? new System.Collections.Generic.List<CommanderView>();
            ShowStatus($"将领收藏: {commanders.Count} 位");
        }

        private void OnShopOpen()
        {
            var view = _session.GetWorldView();
            if (view == null) return;
            var player = view.countries?.Find(c => c.id == _session.PlayerCountryId);
            if (player == null) return;
            ShowStatus($"商城 | 券: {player.gachaTickets}");
        }

        // ================================================================
        // C15a: 将领任命 / 解除
        // ================================================================

        private void AssignCommanderToUnit()
        {
            var view = _session.GetWorldView();
            if (view == null) return;

            // 找当前选中的部队
            if (string.IsNullOrEmpty(view.selectedUnitId))
            {
                ShowStatus("请先选中一支部队");
                return;
            }

            var selUnit = view.units?.Find(u => u.id == view.selectedUnitId);
            if (selUnit == null) return;

            // 已有将领
            if (!string.IsNullOrEmpty(selUnit.commanderId))
            {
                ShowStatus($"该师已有将领: {selUnit.commanderName}");
                return;
            }

            // 找一个空闲将领（未满编的）
            var available = view.commanders?.FindAll(c =>
                c.ownerCountry == _session.PlayerCountryId
                && c.isActive
                && c.commandedDivisions < c.maxDivisions);

            if (available == null || available.Count == 0)
            {
                ShowStatus("没有可用将领（全部满编或不存在）");
                return;
            }

            var cmdr = available[0];
            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.AssignCommander,
                commanderId = cmdr.id,
                unitId = selUnit.id
            });

            if (result.accepted)
                ShowStatus($"{cmdr.name} 已任命至该师");
            else
                ShowStatus($"任命失败: {result.reason}");
            Render();
        }

        private void UnassignCommanderFromUnit()
        {
            var view = _session.GetWorldView();
            if (view == null) return;

            if (string.IsNullOrEmpty(view.selectedUnitId))
            {
                ShowStatus("请先选中一支部队");
                return;
            }

            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.UnassignCommander,
                unitId = view.selectedUnitId
            });

            if (result.accepted)
                ShowStatus("将领已解除");
            else
                ShowStatus($"解除失败: {result.reason}");
            Render();
        }

        public void OfferPeace()
        {
            var view = _session.GetWorldView();
            if (view == null || view.warRelations == null) return;

            var playerWar = view.warRelations.Find(w =>
                w.countryA == view.playerCountryId || w.countryB == view.playerCountryId);
            if (playerWar == null)
            {
                ShowStatus("当前无交战国家");
                Render();
                return;
            }

            string targetId = playerWar.countryA == view.playerCountryId
                ? playerWar.countryB : playerWar.countryA;

            _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.OfferPeace,
                countryId = _session.PlayerCountryId,
                targetCountryId = targetId
            });
        }

        public void SelectCountry(string countryId)
        {
            _session.SetPlayerCountry(countryId);
            Render();
        }

        public void SelectProvince(string provinceId)
        {
            OnProvinceClick(provinceId);
        }

        private void OnProvinceClick(string provinceId)
        {
            var vm = _session.GetWorldView();
            if (vm == null) return;

            var clickedProvince = vm.provinces.Find(p => p.id == provinceId);
            if (clickedProvince == null) return;

            // 已选中部队 -> 检查是否点邻省移动
            if (!string.IsNullOrEmpty(vm.selectedUnitId) && vm.units != null)
            {
                var selectedUnit = vm.units.Find(u => u.id == vm.selectedUnitId);
                if (selectedUnit != null)
                {
                    var currentProvince = vm.provinces.Find(p => p.id == selectedUnit.currentProvinceId);
                    bool isNeighbor = currentProvince != null && currentProvince.neighbors != null
                        && System.Array.Exists(currentProvince.neighbors, n => n == provinceId);
                    bool isFriendly = clickedProvince.controllerCountry == selectedUnit.ownerCountry;
                    bool isEnemy = isNeighbor && !isFriendly && clickedProvince.controllerCountry != null;

                    if (isNeighbor && (isFriendly || isEnemy) && selectedUnit.movesLeft > 0)
                    {
                        // 发移动/攻击命令（GameSessionService 自动分流）
                        var result = _session.IssueCommand(new GameCommand
                        {
                            commandType = CommandType.MoveUnit,
                            countryId = _session.PlayerCountryId,
                            unitId = vm.selectedUnitId,
                            targetProvinceId = provinceId
                        });
                        if (result.accepted)
                        {
                            if (isEnemy)
                                ShowStatus(vm.selectedUnitId + " 开赴 " + clickedProvince.name + " 战场");
                            else
                                ShowStatus(vm.selectedUnitId + " -> " + clickedProvince.name + " (剩余 " + (selectedUnit.movesLeft - 1) + ")");
                            _session.SelectProvince(provinceId);
                        }
                        else
                        {
                            ShowStatus("被拒: " + result.reason);
                        }
                        Render();
                        return;
                    }
                    else if (!isNeighbor)
                    {
                        // 点非邻省 -> 清空部队选中，切省
                        _session.SelectUnit(null);
                    }
                }
            }

            // 选省
            _session.SelectProvince(provinceId);

            // 该省有己方部队 -> 自动选中（按 controllerCountry 判断归属）
            if (clickedProvince.garrisonUnitIds != null && clickedProvince.garrisonUnitIds.Length > 0
                && clickedProvince.controllerCountry == vm.playerCountryId)
            {
                // 循环选：当前已选第 i 支 -> 选下一支（只选己方部队）
                var playerUnitIds = System.Array.FindAll(clickedProvince.garrisonUnitIds,
                    uid => vm.units != null && vm.units.Exists(u => u.id == uid && u.ownerCountry == vm.playerCountryId));
                if (playerUnitIds.Length > 0)
                {
                    string current = vm.selectedUnitId;
                    int idx = System.Array.IndexOf(playerUnitIds, current);
                    int next = (idx + 1) % playerUnitIds.Length;
                    _session.SelectUnit(playerUnitIds[next]);
                }
                else
                {
                    _session.SelectUnit(null);
                }
            }
            else
            {
                _session.SelectUnit(null);
            }

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
                if (_warExhaustionLabel != null)
                    _warExhaustionLabel.text = playerView.warExhaustion.ToString();
                if (_treasuryLabel != null)
                    _treasuryLabel.text = $"💰{playerView.treasury}";
                if (_stabilityLabel != null)
                    _stabilityLabel.text = $"🏛{playerView.stability}";
                if (_warSupportLabel != null)
                    _warSupportLabel.text = $"⚔{playerView.warSupport}";
                if (_gachaTicketsLabel != null)
                    _gachaTicketsLabel.text = $"🎫{playerView.gachaTickets}";
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
                bool isAtWar = vm.warRelations != null && vm.warRelations.Exists(w =>
                    (w.countryA == vm.playerCountryId && w.countryB == c.id) ||
                    (w.countryB == vm.playerCountryId && w.countryA == c.id));
                var row = new Label(FormatCountryRow(c, isPlayer, isAtWar));
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

            // 计算可移动/可攻击目标省
            string[] moveTargets = null;
            string[] attackTargets = null;
            if (!string.IsNullOrEmpty(vm.selectedUnitId) && vm.units != null)
            {
                var selUnit = vm.units.Find(u => u.id == vm.selectedUnitId);
                if (selUnit != null && selUnit.movesLeft > 0 && !selUnit.isInBattle)
                {
                    var selProv = vm.provinces.Find(p => p.id == selUnit.currentProvinceId);
                    if (selProv != null && selProv.neighbors != null)
                    {
                        var moveList = new System.Collections.Generic.List<string>();
                        var attackList = new System.Collections.Generic.List<string>();
                        foreach (var nId in selProv.neighbors)
                        {
                            var np = vm.provinces.Find(p => p.id == nId);
                            if (np == null) continue;
                            if (np.controllerCountry == selUnit.ownerCountry)
                                moveList.Add(nId);
                            else
                                attackList.Add(nId);
                        }
                        moveTargets = moveList.ToArray();
                        attackTargets = attackList.ToArray();
                    }
                }
            }

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

                // 移动目标高亮（绿）
                if (moveTargets != null && System.Array.Exists(moveTargets, t => t == p.id))
                    tile.AddToClassList("province-tile-move-target");

                // 攻击目标高亮（红）
                if (attackTargets != null && System.Array.Exists(attackTargets, t => t == p.id))
                    tile.AddToClassList("province-tile-attack-target");

                // 战斗中省份标记
                if (p.hasActiveBattle)
                {
                    tile.AddToClassList("province-tile-in-battle");
                    var battle = vm.activeBattles?.Find(b => b.provinceId == p.id);
                    string battleText = "⚔战";
                    if (battle != null)
                    {
                        int atkPct = battle.attackerMaxOrg > 0 ? battle.attackerOrg * 100 / battle.attackerMaxOrg : 0;
                        int defPct = battle.defenderMaxOrg > 0 ? battle.defenderOrg * 100 / battle.defenderMaxOrg : 0;
                        battleText = $"⚔{atkPct}%vs{defPct}%";
                    }
                    var battleBadge = new Label(battleText);
                    battleBadge.AddToClassList("province-battle-badge");
                    tile.Add(battleBadge);
                }

                var label = new Label(p.name);
                label.AddToClassList("province-tile-label");
                tile.Add(label);

                // 驻军标记
                if (p.garrisonCount > 0)
                {
                    var garrisonBadge = new Label($"⚔{p.garrisonCount}");
                    garrisonBadge.AddToClassList("province-garrison-badge");
                    tile.Add(garrisonBadge);

                    // C14: 补给状态标记（检查该省是否有己方被切断/混乱部队）
                    if (vm.units != null)
                    {
                        bool hasCutoff = false, hasDisorganized = false;
                        foreach (var u in vm.units)
                        {
                            if (u.currentProvinceId == p.id && u.ownerCountry == vm.playerCountryId)
                            {
                                if (u.isCutoff) hasCutoff = true;
                                else if (u.isDisorganized) hasDisorganized = true;
                            }
                        }
                        if (hasCutoff)
                        {
                            var cutoffBadge = new Label("⛔");
                            cutoffBadge.AddToClassList("province-supply-badge");
                            tile.Add(cutoffBadge);
                        }
                        else if (hasDisorganized)
                        {
                            var disorgBadge = new Label("🟡");
                            disorgBadge.AddToClassList("province-supply-badge");
                            tile.Add(disorgBadge);
                        }
                    }
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
            sb.Append($"  |  补给: {pv.supplyCapacity}");
            if (pv.resourceOutput != null && pv.resourceOutput.Length > 0)
                sb.Append($"  |  产出: {string.Join(", ", pv.resourceOutput)}");
            if (pv.neighbors != null && pv.neighbors.Length > 0)
                sb.Append($"  |  邻接: {string.Join(", ", pv.neighbors)}");
            sb.Append($"  |  驻军: {pv.garrisonCount} 师");

            // 被占领省信息
            if (pv.isOccupied)
            {
                sb.Append($"  |  法理: {pv.ownerCountry} / 控制: {pv.controllerCountry}");
                sb.Append($"  |  抵抗: {pv.resistance}/100");
            }

            // 战斗中详情
            if (pv.hasActiveBattle && vm.activeBattles != null)
            {
                var battle = vm.activeBattles.Find(b => b.provinceId == pv.id);
                if (battle != null)
                {
                    int atkPct = battle.attackerMaxOrg > 0 ? battle.attackerOrg * 100 / battle.attackerMaxOrg : 0;
                    int defPct = battle.defenderMaxOrg > 0 ? battle.defenderOrg * 100 / battle.defenderMaxOrg : 0;
                    sb.Append($"  |  ⚔ 战斗: 攻 {battle.attackerUnitIds.Count} 师 [{battle.attackerOrg}/{battle.attackerMaxOrg} 组织 {atkPct}%]");
                    sb.Append($" vs 守 {battle.defenderUnitIds.Count} 师 [{battle.defenderOrg}/{battle.defenderMaxOrg} 组织 {defPct}%]");
                    sb.Append($" — {battle.turnsElapsed} 回合");
                }
            }

            // 选中部队详情
            if (!string.IsNullOrEmpty(vm.selectedUnitId) && vm.units != null)
            {
                var selUnit = vm.units.Find(u => u.id == vm.selectedUnitId);
                if (selUnit != null && selUnit.currentProvinceId == pv.id)
                {
                    string divName = selUnit.divisionTemplateName ?? selUnit.id;
                    sb.Append($"  |  已选: {divName}");
                    sb.Append($" (移动力 {selUnit.movesLeft}/{selUnit.speed})");
                    if (!string.IsNullOrEmpty(selUnit.brigadeSummary))
                        sb.Append($"\n    编制: {selUnit.brigadeSummary}");
                    int manpowerPct = selUnit.maxManpower > 0 ? selUnit.manpower * 100 / selUnit.maxManpower : 0;
                    int equipPct = selUnit.maxEquipment > 0 ? selUnit.equipment * 100 / selUnit.maxEquipment : 0;
                    int orgPct = selUnit.maxOrganization > 0 ? selUnit.organization * 100 / selUnit.maxOrganization : 0;
                    sb.Append($"\n    人力: {selUnit.manpower}/{selUnit.maxManpower} ({manpowerPct}%)");
                    sb.Append($"  装备: {selUnit.equipment}/{selUnit.maxEquipment} ({equipPct}%)");
                    sb.Append($"  组织: {selUnit.organization}/{selUnit.maxOrganization} ({orgPct}%)");
                    if (selUnit.tacticalLevel > 0)
                        sb.Append($"\n    战役等级: {selUnit.tacticalLevel}/4 (经验 {selUnit.tacticalExp}/100)");
                    sb.Append($"  士气: {selUnit.morale}/100");
                    if (selUnit.isRecovering)
                        sb.Append($"  🔴 溃退恢复中: 剩余 {selUnit.recoveryTurnsLeft} 回合");
                    if (selUnit.isCutoff)
                        sb.Append($"  ⛔ 补给切断: {selUnit.cutoffTurns}/4 回合");
                    else if (selUnit.isDisorganized)
                        sb.Append("  🟡 补给不足（混乱）");
                    else
                        sb.Append("  🟢 补给正常");
                    if (selUnit.isInBattle)
                        sb.Append("  ⚔ 战斗中");
                    // C15a: 将领信息
                    if (!string.IsNullOrEmpty(selUnit.commanderName))
                        sb.Append($"\n    将领: {selUnit.commanderName} ({selUnit.commanderRank})");
                }
            }

            // C15a: 玩家将领列表
            if (vm.commanders != null && vm.commanders.Count > 0)
            {
                var playerCmdrs = vm.commanders.FindAll(c => c.ownerCountry == vm.playerCountryId);
                if (playerCmdrs.Count > 0)
                {
                    sb.Append("\n\n=== 将领 ===");
                    foreach (var cmdr in playerCmdrs)
                    {
                        sb.Append($"\n  {cmdr.name} [{cmdr.rankName}] 攻+{cmdr.rankAttackBonusPct}% 防+{cmdr.rankDefenseBonusPct}% 指挥{cmdr.maxDivisions}师");
                        sb.Append($" (胜场:{cmdr.victories} 包围:{cmdr.encirclements})");
                        if (cmdr.canPromote) sb.Append(" 可晋升!");
                    }
                }
            }

            _provinceDetailLabel.text = sb.ToString();

            // C15a: 将领任命按钮显隐
            if (_commanderActions != null)
            {
                bool showActions = false;
                if (!string.IsNullOrEmpty(vm.selectedUnitId) && vm.units != null)
                {
                    var su = vm.units.Find(u => u.id == vm.selectedUnitId);
                    if (su != null && su.ownerCountry == vm.playerCountryId)
                    {
                        if (string.IsNullOrEmpty(su.commanderId))
                        {
                            // 无将领 → 显示“任命将领”
                            showActions = true;
                            if (_assignCommanderBtn != null) _assignCommanderBtn.style.display = DisplayStyle.Flex;
                            if (_unassignCommanderBtn != null) _unassignCommanderBtn.style.display = DisplayStyle.None;
                        }
                        else
                        {
                            // 有将领 → 显示“解除将领”
                            showActions = true;
                            if (_assignCommanderBtn != null) _assignCommanderBtn.style.display = DisplayStyle.None;
                            if (_unassignCommanderBtn != null) _unassignCommanderBtn.style.display = DisplayStyle.Flex;
                        }
                    }
                }
                _commanderActions.style.display = showActions ? DisplayStyle.Flex : DisplayStyle.None;
            }
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

        public static string FormatCountryRow(CountryView c, bool isPlayer = false, bool isAtWar = false)
        {
            var sb = new StringBuilder();
            if (isPlayer) sb.Append("★ ");
            sb.Append(c.name);
            sb.Append("  |  国库: ");
            sb.Append(c.treasury);
            sb.Append("  |  稳定: ");
            sb.Append(c.stability);
            sb.Append("  |  人力: ");
            sb.Append(c.manpower);
            sb.Append("  |  装备: ");
            sb.Append(c.equipmentStockpile);

            if (c.warExhaustion > 0)
            {
                sb.Append("  |  疲惫: ");
                sb.Append(c.warExhaustion);
            }

            if (isAtWar)
                sb.Append("  |  ⚔ 交战中");

            if (c.constructionQueueCount > 0)
            {
                sb.Append("  |  在建: ");
                sb.Append(c.constructionQueueCount);
            }

            if (c.unitCount > 0)
            {
                sb.Append("  |  部队: ");
                sb.Append(c.unitCount);
            }

            if (c.unitProductionQueueCount > 0)
            {
                sb.Append("  |  在训: ");
                sb.Append(c.unitProductionQueueCount);
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

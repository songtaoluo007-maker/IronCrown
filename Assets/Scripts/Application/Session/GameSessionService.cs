// ============================================================================
// Application/Session/GameSessionService.cs — 游戏会话门面
// UI 唯一合法入口（规则 4），持有运行时 WorldState
// ============================================================================

using System;
using IronCrown.Contracts;
using IronCrown.Domain;
using IronCrown.Simulation;

namespace IronCrown.Application
{
    public sealed class GameSessionService
    {
        private readonly ITurnClock _clock;
        private readonly IConfigRegistry _config;
        private readonly WorldInitializer _initializer;
        private readonly TurnResolver _turnResolver;
        private readonly ConstructionResolver _construction;
        private readonly UnitProductionResolver _unitProduction;
        private readonly MovementResolver _movement;
        private readonly BattleResolver _battle;
        private readonly PeaceResolver _peace;
        private readonly CommanderResolver _commander; // C15a
        private readonly GachaResolver _gacha;         // C16
        private readonly ShopResolver _shop;           // C17
        private readonly IEventPublisher _events;
        private readonly ISaveRepository _save;
        private readonly IRandom _rng;
        private readonly ReadModelBuilder _builder;
        private readonly IAppLogger _logger;

        private WorldState _world;
        private int _initialSeed = 12345;
        private string _playerCountryId;
        private string _selectedProvinceId;

        public string PlayerCountryId => _playerCountryId;

        public GameSessionService(
            ITurnClock clock,
            IConfigRegistry config,
            WorldInitializer initializer,
            TurnResolver turnResolver,
            ConstructionResolver construction,
            UnitProductionResolver unitProduction,
            MovementResolver movement,
            BattleResolver battle,
            PeaceResolver peace,
            IEventPublisher events,
            ISaveRepository save,
            IRandom rng,
            ReadModelBuilder builder,
            IAppLogger logger,
            CommanderResolver commander = null,
            GachaResolver gacha = null,
            ShopResolver shop = null)
        {
            _clock = clock;
            _config = config;
            _initializer = initializer;
            _turnResolver = turnResolver;
            _construction = construction;
            _unitProduction = unitProduction;
            _movement = movement;
            _battle = battle;
            _peace = peace;
            _commander = commander;
            _gacha = gacha;
            _shop = shop;
            _events = events;
            _save = save;
            _rng = rng;
            _builder = builder;
            _logger = logger;

            // 订阅 GameOver 事件 → 切时钟
            _events.Subscribe<GameOverEvent>(_ => _clock.SetGameOver());
        }

        public void NewGame(int? seed = null, string playerCountryId = null)
        {
            if (seed.HasValue)
                _initialSeed = seed.Value;

            _rng.Reset(_initialSeed);
            _config.LoadAll();
            _clock.Reset(60);
            _world = _initializer.CreateNewGame(_config);

            // 选国：默认取按 id 升序第一个
            _playerCountryId = playerCountryId;
            if (string.IsNullOrEmpty(_playerCountryId) && _world.countries.Count > 0)
            {
                string minId = null;
                foreach (var id in _world.countries.Keys)
                {
                    if (minId == null || string.Compare(id, minId, StringComparison.Ordinal) < 0)
                        minId = id;
                }
                _playerCountryId = minId;
            }

            _logger.Info($"[Session] New game: {_world.countries.Count} countries, player={_playerCountryId}");
            _world.playerCountryId = _playerCountryId;
        }

        public void SetPlayerCountry(string countryId)
        {
            if (_world != null && _world.countries.ContainsKey(countryId))
            {
                _playerCountryId = countryId;
                _world.playerCountryId = countryId;
            }
        }

        public CommandResult IssueCommand(GameCommand cmd)
        {
            if (_world == null) return CommandResult.Reject("no world");
            if (_clock.CurrentPhase == GamePhase.GameOver) return CommandResult.Reject("游戏已结束");
            if (string.IsNullOrEmpty(_playerCountryId)) return CommandResult.Reject("no player country");
            if (cmd.countryId != _playerCountryId) return CommandResult.Reject("非玩家国");

            if (!_world.countries.TryGetValue(cmd.countryId, out var country))
                return CommandResult.Reject("国家不存在");

            var eco = _config.Get<EconomyConfig>("global");
            if (eco == null) return CommandResult.Reject("经济配置未加载");

            switch (cmd.commandType)
            {
                case CommandType.BuildCivilianFactory:
                    if (!_construction.TryBuild(country, "civilian", eco))
                        return CommandResult.Reject("资本不足");
                    _logger.Info($"[Session] {cmd.countryId} 开始建造民用厂");
                    return CommandResult.Accept();

                case CommandType.BuildMilitaryFactory:
                    if (!_construction.TryBuild(country, "military", eco))
                        return CommandResult.Reject("资本不足");
                    _logger.Info($"[Session] {cmd.countryId} 开始建造军用厂");
                    return CommandResult.Accept();

                case CommandType.SetTaxLevel:
                    if (cmd.level < 0 || cmd.level > 2)
                        return CommandResult.Reject("档位越界");
                    country.taxLevel = cmd.level;
                    _logger.Info($"[Session] {cmd.countryId} 税率设为 {cmd.level}");
                    return CommandResult.Accept();

                case CommandType.SetCivilLevel:
                    if (cmd.level < 0 || cmd.level > 2)
                        return CommandResult.Reject("档位越界");
                    country.civilLevel = cmd.level;
                    _logger.Info($"[Session] {cmd.countryId} 民生设为 {cmd.level}");
                    return CommandResult.Accept();

                case CommandType.BuildUnit:
                    var unitResult = _unitProduction.TryEnqueue(country, cmd.unitType, _config, eco);
                    if (unitResult.accepted)
                        _logger.Info($"[Session] {cmd.countryId} 下令训练 {cmd.unitType}");
                    return unitResult;

                case CommandType.MoveUnit:
                    // 统一战斗锁定检查
                    foreach (var b in _world.activeBattles)
                    {
                        if (b.attackerUnitIds.Contains(cmd.unitId) || b.defenderUnitIds.Contains(cmd.unitId))
                            return CommandResult.Reject("部队正在战斗中");
                    }

                    if (!_world.units.TryGetValue(cmd.unitId, out var mvUnit))
                        return CommandResult.Reject("部队不存在");
                    if (!_world.provinces.TryGetValue(cmd.targetProvinceId, out var mvTarget))
                        return CommandResult.Reject("目标省份不存在");

                    // C13: 溃退中禁止进攻敌方省
                    if (mvUnit.recoveryTurnsLeft > 0 && mvTarget.controllerCountry != mvUnit.ownerCountry)
                        return CommandResult.Reject("溃退中无法进攻（仅可移动到己方省）");

                    if (mvTarget.controllerCountry == mvUnit.ownerCountry)
                    {
                        // 己方省 → 和平移动
                        var moveFrom = mvUnit.currentProvinceId;
                        var moveResult = _movement.TryMove(_world, cmd.unitId, cmd.targetProvinceId, _playerCountryId);
                        if (moveResult.accepted)
                        {
                            _world.selectedUnitId = cmd.unitId;
                            var movedUnit = _world.units[cmd.unitId];
                            _events.Publish(new UnitMovedEvent
                            {
                                unitId = cmd.unitId,
                                fromProvinceId = moveFrom,
                                toProvinceId = movedUnit.currentProvinceId,
                                movesLeftAfter = movedUnit.movesLeft
                            });
                            _logger.Info($"[Session] {cmd.unitId} 移动到 {cmd.targetProvinceId}（剩余 {movedUnit.movesLeft}）");
                        }
                        return moveResult;
                    }
                    else
                    {
                        // 敌方省 → 战斗
                        var battleResult = _battle.InitiateAttack(_world, cmd.unitId, cmd.targetProvinceId, _playerCountryId);
                        if (battleResult.accepted)
                        {
                            _world.selectedUnitId = cmd.unitId;
                            _logger.Info($"[Session] {cmd.unitId} 开赴 {cmd.targetProvinceId} 战场");
                        }
                        return battleResult;
                    }

                case CommandType.OfferPeace:
                    return _peace.OfferPeace(_world, cmd.countryId, cmd.targetCountryId, eco);

                case CommandType.AcceptPeace:
                    return _peace.AcceptPeace(_world, cmd.countryId, cmd.targetCountryId, eco);

                case CommandType.RejectPeace:
                    return _peace.RejectPeace(_world, cmd.countryId, cmd.targetCountryId, eco);

                // === C15a: 将领系统 ===
                case CommandType.RecruitCommander:
                    if (string.IsNullOrEmpty(cmd.configId))
                        return CommandResult.Reject("缺少将领配置ID");
                    var newCmdr = _commander.RecruitCommander(country, cmd.configId);
                    if (newCmdr == null)
                        return CommandResult.Reject("资源不足或配置不存在");
                    _world.commanders[newCmdr.id] = newCmdr;
                    _logger.Info($"[Session] {cmd.countryId} 招募将领 {newCmdr.name} ({newCmdr.RankName})");
                    return CommandResult.Accept();

                case CommandType.AssignCommander:
                    if (string.IsNullOrEmpty(cmd.commanderId) || string.IsNullOrEmpty(cmd.unitId))
                        return CommandResult.Reject("缺少将领ID或师ID");
                    if (!_commander.AssignDivision(_world, cmd.unitId, cmd.commanderId))
                        return CommandResult.Reject("分配失败（容量满/将领不存在/师不存在）");
                    _logger.Info($"[Session] 将领 {cmd.commanderId} 指挥师 {cmd.unitId}");
                    return CommandResult.Accept();

                case CommandType.UnassignCommander:
                    if (string.IsNullOrEmpty(cmd.unitId))
                        return CommandResult.Reject("缺少师ID");
                    _commander.UnassignDivision(_world, cmd.unitId);
                    _logger.Info($"[Session] 师 {cmd.unitId} 解除将领指挥");
                    return CommandResult.Accept();

                case CommandType.DrawCard:
                    if (_gacha == null)
                        return CommandResult.Reject("抽卡系统未初始化");
                    var playerCountry = _world.countries.TryGetValue(_playerCountryId, out var pc) ? pc : null;
                    if (playerCountry == null)
                        return CommandResult.Reject("找不到玩家国家");
                    var ecoDraw = _config.Get<EconomyConfig>("global");
                    if (ecoDraw == null)
                        return CommandResult.Reject("经济配置未加载");
                    var drawn = _gacha.DrawCard(playerCountry, _world, _rng, _config, ecoDraw);
                    if (drawn == null)
                        return CommandResult.Reject("券不足或卡池为空");
                    _logger.Info($"[Session] 抽卡: {drawn.name} (星级 {drawn.starLevel})");
                    return CommandResult.Accept();

                case CommandType.Buy10DrawBundle:
                    if (_shop == null)
                        return CommandResult.Reject("商城未初始化");
                    var shopCountry1 = _world.countries.TryGetValue(_playerCountryId, out var sc1) ? sc1 : null;
                    if (shopCountry1 == null)
                        return CommandResult.Reject("找不到玩家国家");
                    var ecoShop1 = _config.Get<EconomyConfig>("global");
                    if (ecoShop1 == null)
                        return CommandResult.Reject("经济配置未加载");
                    if (!_shop.BuyBundle(shopCountry1, ecoShop1))
                        return CommandResult.Reject("券不足");
                    _logger.Info($"[Session] 购买10连券包，净增{ecoShop1.shopBundle10DrawsGrants - ecoShop1.shopBundle10DrawsCost}券");
                    return CommandResult.Accept();

                case CommandType.BuySsrTicket:
                    if (_shop == null)
                        return CommandResult.Reject("商城未初始化");
                    var shopCountry2 = _world.countries.TryGetValue(_playerCountryId, out var sc2) ? sc2 : null;
                    if (shopCountry2 == null)
                        return CommandResult.Reject("找不到玩家国家");
                    var ecoShop2 = _config.Get<EconomyConfig>("global");
                    if (ecoShop2 == null)
                        return CommandResult.Reject("经济配置未加载");
                    var ssrCmdr = _shop.BuySsrTicket(shopCountry2, _world, _rng, _config, ecoShop2);
                    if (ssrCmdr == null)
                        return CommandResult.Reject("SSR券购买失败");
                    _logger.Info($"[Session] 购买SSR保底券: {ssrCmdr.name}");
                    return CommandResult.Accept();

                case CommandType.BuySpecificCardTicket:
                    if (_shop == null)
                        return CommandResult.Reject("商城未初始化");
                    if (string.IsNullOrEmpty(cmd.targetCardId))
                        return CommandResult.Reject("缺少目标卡ID");
                    var shopCountry3 = _world.countries.TryGetValue(_playerCountryId, out var sc3) ? sc3 : null;
                    if (shopCountry3 == null)
                        return CommandResult.Reject("找不到玩家国家");
                    var ecoShop3 = _config.Get<EconomyConfig>("global");
                    if (ecoShop3 == null)
                        return CommandResult.Reject("经济配置未加载");
                    var specificCmdr = _shop.BuySpecificCardTicket(shopCountry3, _world, _config, ecoShop3, cmd.targetCardId);
                    if (specificCmdr == null)
                        return CommandResult.Reject("特定卡券购买失败");
                    _logger.Info($"[Session] 购买特定卡券: {specificCmdr.name}");
                    return CommandResult.Accept();

                default:
                    return CommandResult.Reject("未知命令");
            }
        }

        public void AdvancePhase()
        {
            if (_world == null) return;

            if (_clock.CurrentPhase == GamePhase.GameOver)
            {
                _logger.Info("[Session] Game over");
                return;
            }

            if (_clock.CurrentPhase == GamePhase.TurnStart)
            {
                _turnResolver.ExecuteTurn(_world);
            }

            _clock.AdvancePhase();
            _logger.Info($"[Session] Turn {_clock.CurrentTurn} - Phase: {_clock.CurrentPhase}");
        }

        public bool Save(string slot)
        {
            if (_world == null) return false;
            var gameState = SaveMapper.ToSave(_world, _initialSeed, _rng.State, _clock.CurrentPhase);
            gameState.playerCountryId = _playerCountryId;
            _save.Save(slot, gameState);
            return true;
        }

        public bool Load(string slot)
        {
            var gameState = _save.Load(slot);
            if (gameState == null) return false;

            _world = SaveMapper.ToRuntime(gameState);
            _initialSeed = gameState.seed;
            _playerCountryId = gameState.playerCountryId;
            _world.playerCountryId = _playerCountryId;

            // C11: 读档后重算师属性（brigrades → baseAttack/...）
            foreach (var unit in _world.units.Values)
                unit.RecalculateFromBrigades(_config);
            _rng.Reset(gameState.seed);
            _rng.RestoreState(gameState.rngState);
            var phase = Enum.Parse<GamePhase>(gameState.phase);
            _clock.Restore(gameState.turnNumber, phase);

            _logger.Info($"[Session] Loaded save, turn {gameState.turnNumber}, player={_playerCountryId}");
            return true;
        }

        public WorldView GetWorldView()
        {
            if (_world == null) return null;
            return _builder.BuildWorldView(_world, _clock, _playerCountryId, _selectedProvinceId, _config);
        }

        public void SelectProvince(string provinceId)
        {
            if (_world == null) return;
            if (provinceId != null && !_world.provinces.ContainsKey(provinceId))
                return;
            _selectedProvinceId = provinceId;
        }

        public void SelectUnit(string unitId)
        {
            if (_world == null) return;
            if (unitId != null && !_world.units.ContainsKey(unitId))
                return;
            _world.selectedUnitId = unitId;
        }
    }
}

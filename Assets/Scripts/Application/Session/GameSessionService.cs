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
            ISaveRepository save,
            IRandom rng,
            ReadModelBuilder builder,
            IAppLogger logger)
        {
            _clock = clock;
            _config = config;
            _initializer = initializer;
            _turnResolver = turnResolver;
            _construction = construction;
            _unitProduction = unitProduction;
            _save = save;
            _rng = rng;
            _builder = builder;
            _logger = logger;
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
    }
}

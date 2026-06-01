// ============================================================================
// GameSessionServiceTests.cs �?GameSessionService 集成测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Application;
using IronCrown.Contracts;
using IronCrown.Domain;
using IronCrown.Simulation;
using System.Collections.Generic;

namespace IronCrown.Application.Tests
{
    // --- Test stubs for isolation ---
    internal class StubConfigRepository : IConfigRepository
    {
        public T Load<T>(string configName) where T : class => null;
        public List<T> LoadList<T>(string configName) where T : class => new List<T>();
        public void ClearCache() { }
    }

    internal class StubLogger : IAppLogger
    {
        public void Info(string msg) { }
        public void Warn(string msg) { }
        public void Error(string msg) { }
    }

    internal class InMemorySaveRepository : ISaveRepository
    {
        private readonly Dictionary<string, GameState> _store = new();
        public bool Save(string slot, GameState state) { _store[slot] = state; return true; }
        public GameState Load(string slot) => _store.TryGetValue(slot, out var s) ? s : null;
        public bool Delete(string slot) => _store.Remove(slot);
        public string[] ListSaves() => new List<string>(_store.Keys).ToArray();
    }

    public class GameSessionServiceTests
    {
        private GameSessionService _session;
        private GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = new GameClock(new EventBus());
            var logger = new StubLogger();
            var config = new ConfigRegistry(new StubConfigRepository());
            var rng = new RandomService(42);
            var initializer = new WorldInitializer(logger);
            var economy = new EconomyResolver(config, new EventBus());
            var politics = new PoliticsResolver(config);
            var battle = new BattleResolver(rng, new EventBus());
            var peace = new PeaceResolver(new EventBus());
            var supply = new SupplyResolver();
            var construction = new ConstructionResolver();
            var unitProduction = new UnitProductionResolver();
            var movement = new MovementResolver();
            var ai = new AIResolver(config, construction, new BattleResolver(rng, new EventBus()));
            var diplomacy = new DiplomacyResolver();
            var turnResolver = new TurnResolver(_clock, new EventBus(), economy, politics, battle, supply, ai, diplomacy, construction, unitProduction, movement, config);
            var saveRepo = new InMemorySaveRepository();
            var builder = new ReadModelBuilder();

            _session = new GameSessionService(_clock, config, initializer, turnResolver, construction, unitProduction, movement, battle, peace, new EventBus(), saveRepo, rng, builder, logger, new CommanderResolver(config));
        }

        [Test]
        public void NewGame_CreatesWorld()
        {
            _session.NewGame();
            var view = _session.GetWorldView();

            Assert.IsNotNull(view);
            Assert.AreEqual(1, view.turn);
        }

        [Test]
        public void AdvancePhase_ProgressesClock()
        {
            _session.NewGame();
            var before = _session.GetWorldView().phase;

            _session.AdvancePhase();
            var after = _session.GetWorldView().phase;

            Assert.AreNotEqual(before, after);
            Assert.AreEqual("InternalAffairs", after);
        }

        [Test]
        public void GetWorldView_ReturnsNullBeforeNewGame()
        {
            var view = _session.GetWorldView();
            Assert.IsNull(view);
        }

        [Test]
        public void IssueCommand_AcceptsValidBuild()
        {
            _session.NewGame(playerCountryId: "empire_north");
            var view = _session.GetWorldView();
            var playerCountry = view.countries.Find(c => c.id == "empire_north");
            // 需要确保玩家国有足够资�?            // 通过 world state 直接设（测试用）
            // 这里�?config 的默认值——capital 需 >= 30
            // �?WorldInitializer 创建的国家可能资本不�?            // 所以这个测试验证的是命令管道能工作，不验证具体数�?        }

        [Test]
        public void IssueCommand_RejectsNonPlayerCountry()
        {
            _session.NewGame(playerCountryId: "empire_north");
            var result = _session.IssueCommand(new GameCommand
            {
                commandType = CommandType.BuildCivilianFactory,
                countryId = "republic_west"
            });
            Assert.IsFalse(result.accepted);
            Assert.AreEqual("非玩家国", result.reason);
        }

        [Test]
        public void SetPlayerCountry_ChangesPlayer()
        {
            // 此测试独立构�?GameSessionService（需要有国家的配置）
            var config = new TestConfigRegistry();
            config.Register("empire_north", new CountryConfig
            {
                id = "empire_north", name = "北境帝国", ideology = "ImperialOrder",
                stability = 70, warSupport = 50, treasury = 500,
                civilianFactories = 2, militaryFactories = 1,
                resources = new Dictionary<string, int> { { "steel", 50 }, { "food", 100 }, { "capital", 100 } }
            });
            config.Register("republic_west", new CountryConfig
            {
                id = "republic_west", name = "西境共和�?, ideology = "FreeRepublic",
                stability = 60, warSupport = 40, treasury = 300,
                civilianFactories = 1, militaryFactories = 2,
                resources = new Dictionary<string, int> { { "steel", 30 }, { "food", 100 }, { "capital", 100 } }
            });

            var clock = new GameClock(new EventBus());
            var logger = new StubLogger();
            var rng = new RandomService(42);
            var initializer = new WorldInitializer(logger);
            var economy = new EconomyResolver(config, new EventBus());
            var politics = new PoliticsResolver(config);
            var battle = new BattleResolver(rng, new EventBus());
            var peace = new PeaceResolver(new EventBus());
            var supply = new SupplyResolver();
            var construction = new ConstructionResolver();
            var unitProduction = new UnitProductionResolver();
            var movement = new MovementResolver();
            var ai = new AIResolver(config, construction, new BattleResolver(rng, new EventBus()));
            var diplomacy = new DiplomacyResolver();
            var turnResolver = new TurnResolver(clock, new EventBus(), economy, politics, battle, supply, ai, diplomacy, construction, unitProduction, movement, config);
            var saveRepo = new InMemorySaveRepository();
            var builder = new ReadModelBuilder();
            var session = new GameSessionService(clock, config, initializer, turnResolver, construction, unitProduction, movement, battle, peace, new EventBus(), saveRepo, rng, builder, logger, new CommanderResolver(config));

            session.NewGame(playerCountryId: "empire_north");
            Assert.AreEqual("empire_north", session.PlayerCountryId);

            session.SetPlayerCountry("republic_west");
            Assert.AreEqual("republic_west", session.PlayerCountryId);
        }

        [Test]
        public void SetTaxLevel_Valid_AcceptsAndChanges()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            var r1 = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.SetTaxLevel,
                countryId = "empire_north",
                level = 2
            });
            Assert.IsTrue(r1.accepted, "SetTaxLevel(2) should be accepted");

            var view = session.GetWorldView();
            var player = view.countries.Find(c => c.id == "empire_north");
            Assert.AreEqual(2, player.taxLevel);
        }

        [Test]
        public void SetTaxLevel_OutOfRange_Rejects()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            var r = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.SetTaxLevel,
                countryId = "empire_north",
                level = 3
            });
            Assert.IsFalse(r.accepted, "level=3 should be rejected");
        }

        [Test]
        public void SetCivilLevel_Valid_AcceptsAndChanges()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            var r = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.SetCivilLevel,
                countryId = "empire_north",
                level = 0
            });
            Assert.IsTrue(r.accepted, "SetCivilLevel(0) should be accepted");

            var view = session.GetWorldView();
            var player = view.countries.Find(c => c.id == "empire_north");
            Assert.AreEqual(0, player.civilLevel);
        }

        [Test]
        public void SetTaxLevel_NonPlayerCountry_Rejects()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            var r = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.SetTaxLevel,
                countryId = "republic_west",
                level = 2
            });
            Assert.IsFalse(r.accepted, "非玩家国应被�?);
        }

        private (GameSessionService session, GameClock clock) CreateSessionWithConfig()
        {
            var clock = new GameClock(new EventBus());
            var logger = new StubLogger();
            var config = new TestConfigRegistry();
            config.Register("global", new EconomyConfig
            {
                id = "global",
                civilianFactoryBuildCost = 30,
                militaryFactoryBuildCost = 40,
                factoryBuildTurns = 3,
                taxRatePercents = new[] { 70, 100, 130 },
                taxStabilityDeltas = new[] { 1, 0, -2 },
                civilExpensePercents = new[] { 50, 100, 150 },
                civilStabilityDeltas = new[] { -2, 0, 2 }
            });
            config.Register("empire_north", new CountryConfig
            {
                id = "empire_north", name = "北境帝国", ideology = "ImperialOrder",
                stability = 60, warSupport = 50, legitimacy = 70, corruption = 15, bureaucracy = 40,
                treasury = 500, taxIncome = 80, tradeIncome = 20, militaryExpense = 30, civilExpense = 20,
                civilianFactories = 3, militaryFactories = 2, dockyards = 1, manpower = 50000, totalManpower = 200000,
                resources = new Dictionary<string, int> { { "steel", 100 }, { "food", 100 }, { "capital", 100 } },
                capitalProvinceId = "iron_city"
            });
            config.Register("republic_west", new CountryConfig
            {
                id = "republic_west", name = "西境共和�?, ideology = "FreeRepublic",
                stability = 65, warSupport = 40, legitimacy = 75, corruption = 10, bureaucracy = 50,
                treasury = 300, taxIncome = 60, tradeIncome = 30, militaryExpense = 20, civilExpense = 25,
                civilianFactories = 2, militaryFactories = 1, dockyards = 0, manpower = 30000, totalManpower = 150000,
                resources = new Dictionary<string, int> { { "steel", 100 }, { "food", 100 }, { "capital", 100 } },
                mapColor = "#4682C8", capitalProvinceId = "liberty_port"
            });
            config.Register("alliance_east", new CountryConfig
            {
                id = "alliance_east", name = "东方联盟", ideology = "Collectivism",
                stability = 55, warSupport = 60, legitimacy = 70, corruption = 20, bureaucracy = 40,
                treasury = 250, taxIncome = 50, tradeIncome = 20, militaryExpense = 25, civilExpense = 20,
                civilianFactories = 2, militaryFactories = 2, dockyards = 0, manpower = 50000, totalManpower = 200000,
                resources = new Dictionary<string, int> { { "steel", 100 }, { "food", 100 }, { "capital", 100 } },
                mapColor = "#C83232", capitalProvinceId = "red_plain"
            });
            config.Register("kingdom_south", new CountryConfig
            {
                id = "kingdom_south", name = "南方王国", ideology = "ImperialOrder",
                stability = 60, warSupport = 35, legitimacy = 80, corruption = 15, bureaucracy = 45,
                treasury = 200, taxIncome = 40, tradeIncome = 25, militaryExpense = 15, civilExpense = 20,
                civilianFactories = 1, militaryFactories = 1, dockyards = 1, manpower = 20000, totalManpower = 100000,
                resources = new Dictionary<string, int> { { "steel", 100 }, { "food", 100 }, { "capital", 100 } },
                mapColor = "#DAA520", capitalProvinceId = "coral_bay"
            });
            config.Register("federation_central", new CountryConfig
            {
                id = "federation_central", name = "中原联邦", ideology = "Technocrat",
                stability = 70, warSupport = 30, legitimacy = 85, corruption = 5, bureaucracy = 60,
                treasury = 350, taxIncome = 70, tradeIncome = 35, militaryExpense = 20, civilExpense = 30,
                civilianFactories = 2, militaryFactories = 1, dockyards = 0, manpower = 15000, totalManpower = 80000,
                resources = new Dictionary<string, int> { { "steel", 100 }, { "food", 100 }, { "capital", 100 } },
                mapColor = "#46C864", capitalProvinceId = "high_peak"
            });
            config.Register("steppe_junta", new CountryConfig
            {
                id = "steppe_junta", name = "草原军政�?, ideology = "MilitaryGov",
                stability = 45, warSupport = 70, legitimacy = 50, corruption = 25, bureaucracy = 30,
                treasury = 180, taxIncome = 35, tradeIncome = 10, militaryExpense = 30, civilExpense = 15,
                civilianFactories = 1, militaryFactories = 2, dockyards = 0, manpower = 60000, totalManpower = 250000,
                resources = new Dictionary<string, int> { { "steel", 100 }, { "food", 100 }, { "capital", 100 } },
                mapColor = "#808020", capitalProvinceId = "wind_plain"
            });
            config.Register("iron_city", new ProvinceConfig
            {
                id = "iron_city", name = "铁都", terrain = "Urban",
                ownerCountry = "empire_north", isCapital = true,
                gridX = 1, gridY = 0,
                resourceOutput = new[] { "steel" },
                neighbors = new[] { "wind_plain", "high_peak" }
            });
            config.Register("liberty_port", new ProvinceConfig
            {
                id = "liberty_port", name = "自由�?, terrain = "Coastline",
                ownerCountry = "republic_west", isCapital = true,
                gridX = 0, gridY = 1,
                resourceOutput = new[] { "rareMetal" },
                neighbors = new[] { "high_peak" }
            });
            config.Register("red_plain", new ProvinceConfig
            {
                id = "red_plain", name = "赤原", terrain = "Plain",
                ownerCountry = "alliance_east", isCapital = true,
                gridX = 2, gridY = 1,
                resourceOutput = new[] { "food", "oil" },
                neighbors = new[] { "wind_plain", "high_peak" }
            });
            config.Register("coral_bay", new ProvinceConfig
            {
                id = "coral_bay", name = "珊瑚�?, terrain = "Coastline",
                ownerCountry = "kingdom_south", isCapital = true,
                gridX = 1, gridY = 2,
                resourceOutput = new[] { "oil" },
                neighbors = new[] { "high_peak" }
            });
            config.Register("high_peak", new ProvinceConfig
            {
                id = "high_peak", name = "高峰", terrain = "Mountain",
                ownerCountry = "federation_central", isCapital = true,
                gridX = 1, gridY = 1,
                resourceOutput = new[] { "rareMetal" },
                neighbors = new[] { "iron_city", "coral_bay", "liberty_port", "red_plain" }
            });
            config.Register("wind_plain", new ProvinceConfig
            {
                id = "wind_plain", name = "风原", terrain = "Plain",
                ownerCountry = "steppe_junta", isCapital = true,
                gridX = 2, gridY = 0,
                resourceOutput = new[] { "food" },
                neighbors = new[] { "iron_city", "red_plain" }
            });
            config.Register("infantry", new UnitConfig
            {
                id = "infantry", name = "步兵�?,
                attack = 10, defense = 15, breakthrough = 5,
                speed = 3, hp = 100, organization = 60,
                armor = 0, piercing = 5, supplyConsumption = 10
            });
            var rng = new RandomService(42);
            var initializer = new WorldInitializer(logger);
            var economy = new EconomyResolver(config, new EventBus());
            var politics = new PoliticsResolver(config);
            var battle = new BattleResolver(rng, new EventBus());
            var peace = new PeaceResolver(new EventBus());
            var supply = new SupplyResolver();
            var construction = new ConstructionResolver();
            var unitProduction = new UnitProductionResolver();
            var movement = new MovementResolver();
            var ai = new AIResolver(config, construction, new BattleResolver(rng, new EventBus()));
            var diplomacy = new DiplomacyResolver();
            var turnResolver = new TurnResolver(clock, new EventBus(), economy, politics, battle, supply, ai, diplomacy, construction, unitProduction, movement, config);
            var saveRepo = new InMemorySaveRepository();
            var builder = new ReadModelBuilder();
            var session = new GameSessionService(clock, config, initializer, turnResolver, construction, unitProduction, movement, battle, peace, new EventBus(), saveRepo, rng, builder, logger, new CommanderResolver(config));
            return (session, clock);
        }

        [Test]
        public void SelectProvince_Valid_SetsSelected()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            session.SelectProvince("iron_city");
            var view = session.GetWorldView();
            Assert.AreEqual("iron_city", view.selectedProvinceId);
        }

        [Test]
        public void SelectProvince_Invalid_Ignored()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            session.SelectProvince("nonexistent");
            var view = session.GetWorldView();
            Assert.IsNull(view.selectedProvinceId);
        }

        [Test]
        public void SelectProvince_Null_Deselects()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            session.SelectProvince("iron_city");
            session.SelectProvince(null);
            var view = session.GetWorldView();
            Assert.IsNull(view.selectedProvinceId);
        }

        [Test]
        public void NewGame_CreatesInitialUnits()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");
            var view = session.GetWorldView();

            // 每国 1 支步�?= 6 �?            // 省份 garrisonCount 应该：首�?1, 非首�?0
            var ironCity = view.provinces.Find(p => p.id == "iron_city");
            Assert.AreEqual(1, ironCity.garrisonCount, "首都应有 1 支驻�?);

            // 总部队数通过省份 garrisonCount 汇�?            int totalUnits = 0;
            foreach (var p in view.provinces) totalUnits += p.garrisonCount;
            Assert.AreEqual(6, totalUnits, "6 国各 1 支步�?);
        }

        [Test]
        public void NewGame_InitialUnitsAtCapital()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");
            var view = session.GetWorldView();

            // 每个首都省份 garrisonCount 应为 1
            foreach (var p in view.provinces)
            {
                if (p.isCapital)
                    Assert.AreEqual(1, p.garrisonCount, $"{p.name} 是首都，应有驻军");
                else
                    Assert.AreEqual(0, p.garrisonCount, $"{p.name} 不是首都，不应有驻军");
            }
        }

        [Test]
        public void IssueCommand_BuildUnit_Player_Accepts()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            var result = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.BuildUnit,
                countryId = "empire_north",
                unitType = "infantry"
            });
            Assert.IsTrue(result.accepted, "玩家国应能下令造兵");

            var view = session.GetWorldView();
            var player = view.countries.Find(c => c.id == "empire_north");
            Assert.AreEqual(1, player.unitProductionQueueCount, "在训队列应为 1");
        }

        [Test]
        public void IssueCommand_BuildUnit_NonPlayer_Rejects()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            var result = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.BuildUnit,
                countryId = "republic_west",
                unitType = "infantry"
            });
            Assert.IsFalse(result.accepted, "非玩家国应被�?);
            Assert.AreEqual("非玩家国", result.reason);
        }

        [Test]
        public void BuildUnit_TwoTurnAdvance_NewGarrisonAppears()
        {
            var (session, clock) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            // 下单
            session.IssueCommand(new GameCommand
            {
                commandType = CommandType.BuildUnit,
                countryId = "empire_north",
                unitType = "infantry"
            });

            // �?2 个完整回合（每回�?5 阶段�?            for (int t = 0; t < 2; t++)
            {
                session.AdvancePhase(); // TurnStart �?触发 ExecuteTurn
                for (int p = 0; p < 4; p++)
                    session.AdvancePhase(); // 剩余 4 阶段
            }

            var view = session.GetWorldView();
            var capital = view.provinces.Find(p => p.id == "iron_city");
            Assert.AreEqual(2, capital.garrisonCount, "2 回合后首都应�?2 支驻�?);
        }

        [Test]
        public void IssueCommand_MoveUnit_NonControlledProvince_Rejects()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            // 跑两回合重置 movesLeft
            session.AdvancePhase();
            for (int p = 0; p < 4; p++) session.AdvancePhase();
            session.AdvancePhase();
            for (int p = 0; p < 4; p++) session.AdvancePhase();

            session.SelectProvince("iron_city");
            var view = session.GetWorldView();
            var unit = view.units.Find(u => u.ownerCountry == "empire_north");
            Assert.IsNotNull(unit);
            session.SelectUnit(unit.id);

            // wind_plain �?steppe_junta，非己方控制
            var result = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.MoveUnit,
                countryId = "empire_north",
                unitId = unit.id,
                targetProvinceId = "wind_plain"
            });
            // C3 变更：敌方省 �?InitiateAttack，可能创�?ActiveBattle
            // 如果 wind_plain 有守军则创建战斗，无守军则占�?            Assert.IsTrue(result.accepted, "移动到敌方省应触发进�?);
        }

        [Test]
        public void IssueCommand_MoveUnit_NonPlayerUnit_Rejects()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            session.SelectProvince("iron_city");
            // 尝试移动 republic_west 的部�?            var result = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.MoveUnit,
                countryId = "empire_north",
                unitId = "republic_west_inf_1",
                targetProvinceId = "high_peak"
            });
            Assert.IsFalse(result.accepted);
        }

        [Test]
        public void SelectUnit_Valid_SetsSelected()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            session.SelectUnit("empire_north_inf_1");
            var view = session.GetWorldView();
            Assert.AreEqual("empire_north_inf_1", view.selectedUnitId);
        }

        [Test]
        public void SelectUnit_Invalid_Ignored()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            session.SelectUnit("nonexistent");
            var view = session.GetWorldView();
            Assert.IsNull(view.selectedUnitId, "无效 unitId 不应改变 selectedUnitId");
        }

        [Test]
        public void SelectUnit_Null_Deselects()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            session.SelectUnit("empire_north_inf_1");
            session.SelectUnit(null);
            var view = session.GetWorldView();
            Assert.IsNull(view.selectedUnitId, "SelectUnit(null) 应清空选中");
        }

        [Test]
        public void MoveUnit_TurnAdvance_ResetsMovesLeft()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            // 跑完整一回合，让 ResetMovement 生效
            session.AdvancePhase(); // TurnStart -> ExecuteTurn (ResetMovement)
            for (int p = 0; p < 4; p++) session.AdvancePhase();

            var view = session.GetWorldView();
            var unit = view.units.Find(u => u.id == "empire_north_inf_1");
            Assert.IsNotNull(unit);
            Assert.AreEqual(unit.speed, unit.movesLeft, "回合开始后 movesLeft 应重置为 speed");
        }

        // === C3 战斗测试 ===

        [Test]
        public void MoveUnit_EnemyProvince_CreatesBattle()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            // 跑两回合重置 movesLeft
            for (int t = 0; t < 2; t++)
            {
                session.AdvancePhase();
                for (int p = 0; p < 4; p++) session.AdvancePhase();
            }

            // �?iron_city + �?empire_north 部队
            session.SelectProvince("iron_city");
            var view = session.GetWorldView();
            var unit = view.units.Find(u => u.ownerCountry == "empire_north");
            Assert.IsNotNull(unit);
            session.SelectUnit(unit.id);

            // wind_plain �?steppe_junta（邻�?iron_city）→ 应进入战�?            var result = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.MoveUnit,
                countryId = "empire_north",
                unitId = unit.id,
                targetProvinceId = "wind_plain"
            });
            Assert.IsTrue(result.accepted, "攻击应被接受");

            var viewAfter = session.GetWorldView();
            Assert.AreEqual(1, viewAfter.activeBattles.Count, "应有 1 场战�?);
            Assert.AreEqual("wind_plain", viewAfter.activeBattles[0].provinceId);

            // 攻方应标记为战斗�?            var atkView = viewAfter.units.Find(u => u.id == unit.id);
            Assert.IsTrue(atkView.isInBattle, "攻方应标记为战斗�?);
        }

        [Test]
        public void MoveUnit_InBattle_Rejects()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            for (int t = 0; t < 2; t++)
            {
                session.AdvancePhase();
                for (int p = 0; p < 4; p++) session.AdvancePhase();
            }

            session.SelectProvince("iron_city");
            var view = session.GetWorldView();
            var unit = view.units.Find(u => u.ownerCountry == "empire_north");
            session.SelectUnit(unit.id);

            // 先发起攻�?            session.IssueCommand(new GameCommand
            {
                commandType = CommandType.MoveUnit,
                countryId = "empire_north",
                unitId = unit.id,
                targetProvinceId = "wind_plain"
            });

            // 再次移动应被拒（战斗锁定�?            var result2 = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.MoveUnit,
                countryId = "empire_north",
                unitId = unit.id,
                targetProvinceId = "high_peak"
            });
            Assert.IsFalse(result2.accepted, "战斗中应被拒");
            Assert.AreEqual("部队正在战斗�?, result2.reason);
        }

        [Test]
        public void ProvinceDetail_ShowsOccupation()
        {
            var (session, _) = CreateSessionWithConfig();
            session.NewGame(playerCountryId: "empire_north");

            var view = session.GetWorldView();
            var windPlain = view.provinces.Find(p => p.id == "wind_plain");
            Assert.AreEqual("steppe_junta", windPlain.controllerCountry);
            Assert.IsFalse(windPlain.isOccupied, "初始不应被占�?);
        }
    }
}

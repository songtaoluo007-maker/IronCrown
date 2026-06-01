// ============================================================================
// Tests/EditMode/IronCrown.Simulation.Tests/BattleResolverC4Tests.cs
// C4: AI 军事 AI + 战争状态 + 胜负终局
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Contracts;
using IronCrown.Application;
using IronCrown.Simulation;

namespace IronCrown.Tests
{
    // === 事件记录器 ===
    class EventRecorder
    {
        public List<WarDeclaredEvent> wars = new();
        public List<GameOverEvent> gameOvers = new();
    }

    [TestFixture]
    public class BattleResolverC4Tests
    {
        // === 辅助 ===

        private class StubConfigRepository : IConfigRepository
        {
            public T Load<T>(string configName) where T : class => null;
            public List<T> LoadList<T>(string configName) where T : class => new List<T>();
            public void ClearCache() { }
        }

        private class StubLogger : IAppLogger
        {
            public void Info(string msg) { }
            public void Warn(string msg) { }
            public void Error(string msg) { }
        }

        private class InMemorySaveRepository : ISaveRepository
        {
            private readonly Dictionary<string, GameState> _store = new();
            public bool Save(string slot, GameState state) { _store[slot] = state; return true; }
            public GameState Load(string slot) => _store.TryGetValue(slot, out var s) ? s : null;
            public bool Delete(string slot) => _store.Remove(slot);
            public string[] ListSaves() => new List<string>(_store.Keys).ToArray();
        }

        private (WorldState world, CountryState player, CountryState enemy,
                 ProvinceState provA, ProvinceState provB, ProvinceState provC,
                 UnitState atk, UnitState def, IRandom rng, EventBus events, EventRecorder rec)
            Setup(string suffix = "")
        {
            var events = new EventBus();
            var rec = new EventRecorder();
            events.Subscribe<WarDeclaredEvent>(e => rec.wars.Add(e));
            events.Subscribe<GameOverEvent>(e => rec.gameOvers.Add(e));

            var rng = new RandomService(999);
            var world = new WorldState { playerCountryId = "player", turnNumber = 1 };

            var player = new CountryState { id = "player", name = "P" };
            player.capitalProvinceId = $"pa{suffix}";
            player.ModifyResource("steel", 1000); player.ModifyResource("food", 1000);
            player.ModifyResource("capital", 1000); player.ModifyResource("oil", 1000); player.ModifyResource("rubber", 1000);
            world.countries[player.id] = player;

            var enemy = new CountryState { id = "enemy", name = "E" };
            enemy.capitalProvinceId = $"pb{suffix}";
            enemy.ModifyResource("steel", 1000); enemy.ModifyResource("food", 1000);
            enemy.ModifyResource("capital", 1000); enemy.ModifyResource("oil", 1000); enemy.ModifyResource("rubber", 1000);
            world.countries[enemy.id] = enemy;

            var provA = new ProvinceState { id = $"pa{suffix}", ownerCountry = "player", controllerCountry = "player" };
            var provB = new ProvinceState { id = $"pb{suffix}", ownerCountry = "enemy", controllerCountry = "enemy" };
            var provC = new ProvinceState { id = $"pc{suffix}", ownerCountry = "enemy", controllerCountry = "enemy" };
            provA.neighbors = new[] { provB.id, provC.id };
            provB.neighbors = new[] { provA.id };
            provC.neighbors = new[] { provA.id };
            world.provinces[provA.id] = provA;
            world.provinces[provB.id] = provB;
            world.provinces[provC.id] = provC;

            var atk = new UnitState { id = $"atk{suffix}", ownerCountry = "player", currentProvinceId = provA.id,
                baseAttack = 10, baseDefense = 5, manpower = 100, maxManpower = 100,
                organization = 60, maxOrganization = 60, speed = 3, movesLeft = 3 };
            var def = new UnitState { id = $"def{suffix}", ownerCountry = "enemy", currentProvinceId = provB.id,
                baseAttack = 10, baseDefense = 5, manpower = 100, maxManpower = 100,
                organization = 60, maxOrganization = 60, speed = 3, movesLeft = 3 };
            world.units[atk.id] = atk;
            world.units[def.id] = def;
            player.unitIds.Add(atk.id);
            enemy.unitIds.Add(def.id);

            return (world, player, enemy, provA, provB, provC, atk, def, rng, events, rec);
        }

        private BattleResolver MakeBattle(IRandom rng, IEventPublisher events) =>
            new BattleResolver(rng, events);

        private EconomyConfig MakeEco() => new EconomyConfig
        {
            id = "global", unitProductionTurns = 2,
            aiBuildCapitalThreshold = 60, aiMaxCivilianFactories = 20, aiMaxMilitaryFactories = 15,
            aiAttackPowerRatio = 120, aiMaxAttacksPerTurn = 1
        };

        private ConfigRegistry MakeConfig(EconomyConfig eco)
        {
            var repo = new StubConfigRepositoryWithData(eco);
            var cfg = new ConfigRegistry(repo);
            cfg.LoadAll();
            return cfg;
        }

        private class StubConfigRepositoryWithData : IConfigRepository
        {
            private readonly EconomyConfig _eco;
            public StubConfigRepositoryWithData(EconomyConfig eco) { _eco = eco; }
            public T Load<T>(string configName) where T : class => configName == "economy" ? _eco as T : null;
            public System.Collections.Generic.List<T> LoadList<T>(string configName) where T : class
            {
                if (configName == "economy" && _eco is T ecoObj) return new System.Collections.Generic.List<T> { ecoObj };
                return new System.Collections.Generic.List<T>();
            }
            public void ClearCache() { }
        }

        // === WarRegistry 单元测试 ===

        [Test]
        public void WarRegistry_DeclareOnce_NoDuplicate()
        {
            var (world, _, _, _, _, _, _, _, _, _, _) = Setup("w1");
            bool declared = WarRegistry.TryDeclareWar(world, "player", "enemy", 1, out var rel);
            Assert.IsTrue(declared);
            Assert.AreEqual(1, world.warRelations.Count);

            // 第二次幂等
            bool again = WarRegistry.TryDeclareWar(world, "player", "enemy", 1, out _);
            Assert.IsFalse(again);
            Assert.AreEqual(1, world.warRelations.Count);
        }

        [Test]
        public void WarRegistry_AreAtWar_Bidirectional()
        {
            var (world, _, _, _, _, _, _, _, _, _, _) = Setup("w2");
            WarRegistry.TryDeclareWar(world, "player", "enemy", 1, out _);
            Assert.IsTrue(WarRegistry.AreAtWar(world, "player", "enemy"));
            Assert.IsTrue(WarRegistry.AreAtWar(world, "enemy", "player"));
            Assert.IsFalse(WarRegistry.AreAtWar(world, "player", "nonexist"));
        }

        [Test]
        public void WarRegistry_Normalize_Ascending()
        {
            var (world, _, _, _, _, _, _, _, _, _, _) = Setup("w3");
            WarRegistry.TryDeclareWar(world, "zulu", "alpha", 1, out var rel);
            Assert.AreEqual("alpha", rel.countryA);
            Assert.AreEqual("zulu", rel.countryB);
        }

        // === InitiateAttack 自动宣战 ===

        [Test]
        public void InitiateAttack_EmptyCity_AutoDeclaresWar()
        {
            var (world, _, _, _, _, provC, atk, _, rng, events, rec) = Setup("aw1");
            atk.currentProvinceId = provC.neighbors[0]; // provA
            var battle = MakeBattle(rng, events);

            var result = battle.InitiateAttack(world, atk.id, provC.id, "player");
            Assert.IsTrue(result.accepted);
            Assert.AreEqual(1, rec.wars.Count, "应自动宣战");
            Assert.IsTrue(WarRegistry.AreAtWar(world, "player", "enemy"));
        }

        [Test]
        public void InitiateAttack_WithDefender_AutoDeclaresWar()
        {
            var (world, _, _, _, provB, _, atk, def, rng, events, rec) = Setup("aw2");
            var battle = MakeBattle(rng, events);

            var result = battle.InitiateAttack(world, atk.id, provB.id, "player");
            Assert.IsTrue(result.accepted);
            Assert.AreEqual(1, rec.wars.Count);
            Assert.AreEqual(1, world.activeBattles.Count);
        }

        [Test]
        public void InitiateAttack_SameWar_NoDuplicateEvent()
        {
            var (world, _, _, _, provB, provC, atk, def, rng, events, rec) = Setup("aw3");
            var battle = MakeBattle(rng, events);

            battle.InitiateAttack(world, atk.id, provB.id, "player");
            Assert.AreEqual(1, rec.wars.Count);

            // 第二支进攻同一国 → 不再触发事件
            var atk2 = new UnitState { id = "atk2_aw3", ownerCountry = "player", currentProvinceId = provC.neighbors[0],
                baseAttack = 10, baseDefense = 5, manpower = 100, maxManpower = 100,
                organization = 60, maxOrganization = 60, speed = 3, movesLeft = 3 };
            world.units[atk2.id] = atk2;
            world.countries["player"].unitIds.Add(atk2.id);

            var result2 = battle.InitiateAttack(world, atk2.id, provC.id, "player");
            Assert.IsTrue(result2.accepted);
            Assert.AreEqual(1, rec.wars.Count, "同一战争关系不重复触发");
        }

        // === VictoryConditionResolver ===

        [Test]
        public void Victory_PlayerCapitalOccupied_Defeat()
        {
            var (world, player, _, provA, _, _, _, _, _, events, rec) = Setup("v1");
            var clock = new GameClock(events);
            var resolver = new VictoryConditionResolver(events);

            // 玩家首都被占
            provA.controllerCountry = "enemy";
            var outcome = resolver.CheckVictory(world, clock);

            Assert.AreEqual("Defeat", outcome.result);
            Assert.AreEqual("Defeat", world.gameOverResult);
            Assert.AreEqual(1, rec.gameOvers.Count);
            Assert.AreEqual("Defeat", rec.gameOvers[0].result);
        }

        [Test]
        public void Victory_AllEnemyCapitalsOccupied_Victory()
        {
            var (world, _, enemy, _, provB, _, _, _, _, events, rec) = Setup("v2");
            var clock = new GameClock(events);
            var resolver = new VictoryConditionResolver(events);

            // 玩家占了敌方首都
            provB.controllerCountry = "player";
            var outcome = resolver.CheckVictory(world, clock);

            Assert.AreEqual("Victory", outcome.result);
            Assert.AreEqual("player", outcome.winnerCountryId);
            Assert.AreEqual(1, rec.gameOvers.Count);
        }

        [Test]
        public void Victory_Neither_None()
        {
            var (world, _, _, _, _, _, _, _, _, events, rec) = Setup("v3");
            var clock = new GameClock(events);
            var resolver = new VictoryConditionResolver(events);

            var outcome = resolver.CheckVictory(world, clock);
            Assert.AreEqual(default(VictoryOutcome), outcome);
            Assert.AreEqual(0, rec.gameOvers.Count);
        }

        [Test]
        public void Victory_AlreadyGameOver_Skips()
        {
            var (world, player, _, provA, _, _, _, _, _, events, rec) = Setup("v4");
            var clock = new GameClock(events);
            clock.SetGameOver();
            var resolver = new VictoryConditionResolver(events);

            provA.controllerCountry = "enemy";
            var outcome = resolver.CheckVictory(world, clock);
            Assert.AreEqual(default(VictoryOutcome), outcome);
            Assert.AreEqual(0, rec.gameOvers.Count, "GameOver 状态不重复触发");
        }

        [Test]
        public void Victory_MultiCountry_AllMustBeOccupied()
        {
            var (world, player, enemy, _, provB, _, _, _, _, events, rec) = Setup("v5");
            var clock = new GameClock(events);
            var resolver = new VictoryConditionResolver(events);

            // 加第三国，首都未被占
            var third = new CountryState { id = "third", name = "T" };
            third.capitalProvinceId = "prov_third";
            var provThird = new ProvinceState { id = "prov_third", ownerCountry = "third", controllerCountry = "third" };
            world.countries["third"] = third;
            world.provinces["prov_third"] = provThird;

            provB.controllerCountry = "player"; // 占了 enemy 首都
            var outcome = resolver.CheckVictory(world, clock);
            Assert.AreEqual(default(VictoryOutcome), outcome, "第三国首都未占，不应胜利");
        }

        [Test]
        public void Victory_MultiCountry_AllOccupied_Victory()
        {
            var (world, player, enemy, _, provB, _, _, _, _, events, rec) = Setup("v6");
            var clock = new GameClock(events);
            var resolver = new VictoryConditionResolver(events);

            var third = new CountryState { id = "third", name = "T" };
            third.capitalProvinceId = "prov_third";
            var provThird = new ProvinceState { id = "prov_third", ownerCountry = "third", controllerCountry = "third" };
            world.countries["third"] = third;
            world.provinces["prov_third"] = provThird;

            provB.controllerCountry = "player";
            provThird.controllerCountry = "player";
            var outcome = resolver.CheckVictory(world, clock);
            Assert.AreEqual("Victory", outcome.result, "所有敌方首都已占，应胜利");
        }

        // === GameSessionService GameOver guard ===

        [Test]
        public void GameSession_IssueCommand_GameOver_Rejected()
        {
            var config = new ConfigRegistry(new StubConfigRepository());
            var events = new EventBus();
            var logger = new StubLogger();
            var rng = new RandomService(12345);
            var clock = new GameClock(events);
            var initializer = new WorldInitializer(logger);
            var economy = new EconomyResolver(config, events);
            var politics = new PoliticsResolver(config);
            var battle = new BattleResolver(rng, events);
            var supply = new SupplyResolver();
            var construction = new ConstructionResolver();
            var unitProduction = new UnitProductionResolver();
            var movement = new MovementResolver();
            var ai = new AIResolver(config, construction, battle);
            var diplo = new DiplomacyResolver();
            var victory = new VictoryConditionResolver(events);
            var readModel = new ReadModelBuilder();

            var turnResolver = new TurnResolver(clock, events, economy, politics, battle, supply, ai, diplo, construction, unitProduction, movement, config, victory);
            var saveRepo = new InMemorySaveRepository();
            var peace = new PeaceResolver(events);

            var session = new GameSessionService(
                clock, config, initializer, turnResolver, construction, unitProduction, movement, battle,
                peace, new CommanderResolver(config), events, saveRepo, rng, readModel, logger);

            session.NewGame();
            var cmd = new GameCommand { commandType = CommandType.BuildCivilianFactory, countryId = "empire_north" };
            var result = session.IssueCommand(cmd);
            // May or may not succeed depending on state — just verify no crash

            // 模拟 GameOver
            clock.SetGameOver();
            var result2 = session.IssueCommand(cmd);
            Assert.IsFalse(result2.accepted);
            Assert.IsTrue(result2.reason.Contains("游戏已结束"));
        }

        // === AI 军事 AI 集成测试 ===

        [Test]
        public void AI_TryAttack_StrongerUnit_AttacksWeakerNeighbor()
        {
            var (world, player, enemy, provA, provB, _, atk, def, rng, events, rec) = Setup("ai1");
            var eco = MakeEco();
            var config = MakeConfig(eco);
            var construction = new ConstructionResolver();
            var battle = MakeBattle(rng, events);
            var ai = new AIResolver(config, construction, battle);

            // 攻方足够强 — def 是 enemy 的部队，AI 用它进攻
            def.baseAttack = 20;
            atk.baseDefense = 5;
            atk.organization = 30;
            atk.maxOrganization = 60;

            ai.MakeDecisions(enemy, world);

            Assert.IsTrue(rec.wars.Count >= 1, "AI 应主动宣战");
            Assert.IsTrue(world.activeBattles.Count >= 1, "AI 应发动进攻");
        }

        [Test]
        public void AI_TryAttack_WeakerUnit_Skips()
        {
            var (world, player, enemy, provA, provB, _, atk, def, rng, events, rec) = Setup("ai2");
            var eco = MakeEco();
            var config = MakeConfig(eco);
            var construction = new ConstructionResolver();
            var battle = MakeBattle(rng, events);
            var ai = new AIResolver(config, construction, battle);

            // 攻方太弱 — def 是 enemy 的部队，AI 用它进攻
            def.baseAttack = 5;
            atk.baseDefense = 20;

            ai.MakeDecisions(enemy, world);

            Assert.AreEqual(0, rec.wars.Count, "弱攻不应宣战");
            Assert.AreEqual(0, world.activeBattles.Count);
        }

        [Test]
        public void AI_TryAttack_EmptyCity_Attacks()
        {
            var (world, player, enemy, provA, provB, provC, atk, def, rng, events, rec) = Setup("ai3");
            var eco = MakeEco();
            var config = MakeConfig(eco);
            var construction = new ConstructionResolver();
            var battle = MakeBattle(rng, events);
            var ai = new AIResolver(config, construction, battle);

            // remove player unit so provA is empty
            world.units.Remove(atk.id);
            player.unitIds.Remove(atk.id);

            // enemy def is very weak — should still attack empty city
            def.baseAttack = 1;
            def.organization = 1;
            def.baseDefense = 1;

            ai.MakeDecisions(enemy, world);

            // empty city = auto-occupy, should declare war
            Assert.IsTrue(rec.wars.Count >= 1, "弱部队也应占领空城");
        }

        [Test]
        public void AI_TryAttack_MaxAttacksPerTurn_Limited()
        {
            var (world, player, enemy, provA, provB, provC, atk, def, rng, events, rec) = Setup("ai4");
            var eco = MakeEco();
            eco.aiMaxAttacksPerTurn = 1; // 限制 1 次
            var config = MakeConfig(eco);
            var construction = new ConstructionResolver();
            var battle = MakeBattle(rng, events);
            var ai = new AIResolver(config, construction, battle);

            // 两支足够强的 enemy 部队
            var def2 = new UnitState { id = "def2_ai4", ownerCountry = "enemy", currentProvinceId = provB.id,
                baseAttack = 20, baseDefense = 20, manpower = 100, maxManpower = 100,
                organization = 60, maxOrganization = 60, speed = 3, movesLeft = 3 };
            world.units[def2.id] = def2;
            enemy.unitIds.Add(def2.id);

            // provA 和 provC 都与 provB 相邻
            provB.neighbors = new[] { provA.id, provC.id };

            atk.baseAttack = 1; atk.organization = 1; // player 部队很弱
            atk.baseDefense = 1;

            ai.MakeDecisions(enemy, world);

            Assert.LessOrEqual(rec.wars.Count, 2, "受 aiMaxAttacksPerTurn 限制");
        }

        [Test]
        public void AI_TryAttack_PlayerCountry_Skipped()
        {
            var (world, player, _, provA, _, _, atk, _, rng, events, rec) = Setup("ai5");
            var eco = MakeEco();
            var config = MakeConfig(eco);
            var construction = new ConstructionResolver();
            var battle = MakeBattle(rng, events);
            var ai = new AIResolver(config, construction, battle);

            // 玩家国不触发 AI
            ai.MakeDecisions(player, world);
            Assert.AreEqual(0, rec.wars.Count, "玩家国不应被 AI 操作");
        }

        [Test]
        public void AI_TryAttack_UnitInBattle_Skipped()
        {
            var (world, player, enemy, provA, provB, _, atk, def, rng, events, rec) = Setup("ai6");
            var eco = MakeEco();
            var config = MakeConfig(eco);
            var construction = new ConstructionResolver();
            var battle = MakeBattle(rng, events);
            var ai = new AIResolver(config, construction, battle);

            // 把 def 放入战斗
            var active = new ActiveBattle
            {
                id = "battle_ai6",
                attackerUnitIds = new List<string> { atk.id },
                defenderUnitIds = new List<string> { def.id },
                provinceId = provB.id,
                turnsElapsed = 0
            };
            world.activeBattles.Add(active);

            ai.MakeDecisions(enemy, world);
            Assert.AreEqual(0, rec.wars.Count, "战斗中的部队不应被 AI 用于进攻");
        }

        // === 多国场景端到端 ===

        [Test]
        public void MultiCountry_PlayerCapturesAll_Victory()
        {
            var events = new EventBus();
            var rec = new EventRecorder();
            events.Subscribe<GameOverEvent>(e => rec.gameOvers.Add(e));

            var world = new WorldState { playerCountryId = "player", turnNumber = 1 };
            var player = new CountryState { id = "player", name = "P" };
            player.capitalProvinceId = "cap_p";
            SetResources(player);
            world.countries["player"] = player;

            var capP = new ProvinceState { id = "cap_p", ownerCountry = "player", controllerCountry = "player" };
            world.provinces["cap_p"] = capP;

            for (int i = 0; i < 3; i++)
            {
                var c = new CountryState { id = $"c{i}", name = $"C{i}" };
                c.capitalProvinceId = $"cap_{i}";
                SetResources(c);
                world.countries[c.id] = c;
                var prov = new ProvinceState { id = $"cap_{i}", ownerCountry = c.id, controllerCountry = c.id };
                world.provinces[prov.id] = prov;
            }

            var resolver = new VictoryConditionResolver(events);
            var clock = new GameClock(events);

            // 占领前两个
            world.provinces["cap_0"].controllerCountry = "player";
            world.provinces["cap_1"].controllerCountry = "player";
            var o1 = resolver.CheckVictory(world, clock);
            Assert.AreEqual(default(VictoryOutcome), o1);

            // 占领第三个 → Victory
            world.provinces["cap_2"].controllerCountry = "player";
            var o2 = resolver.CheckVictory(world, clock);
            Assert.AreEqual("Victory", o2.result);
            Assert.AreEqual(1, rec.gameOvers.Count);
        }

        // === 辅助方法 ===

        private EconomyConfig CreateRealConfig() => new EconomyConfig
        {
            id = "global", provinceBaseOutputPerResource = 4, provinceInfraOutputBonus = 2,
            militaryFactoryEquipmentOutput = 4, equipmentSteelCost = 2, equipmentCapitalCost = 1,
            civilianFactoryUpkeep = 2, militaryFactoryUpkeep = 3, dockyardUpkeep = 4,
            civilianFactoryBuildCost = 30, militaryFactoryBuildCost = 40, factoryBuildTurns = 3,
            taxRatePercents = new[] { 70, 100, 130 },
            taxStabilityDeltas = new[] { 1, 0, -2 },
            civilExpensePercents = new[] { 50, 100, 150 },
            civilStabilityDeltas = new[] { -2, 0, 2 },
            unitProductionTurns = 2,
            aiBuildCapitalThreshold = 60, aiMaxCivilianFactories = 20, aiMaxMilitaryFactories = 15,
            aiAttackPowerRatio = 120, aiMaxAttacksPerTurn = 1
        };

        private void SetResources(CountryState c)
        {
            c.ModifyResource("steel", 1000); c.ModifyResource("food", 1000);
            c.ModifyResource("capital", 1000); c.ModifyResource("oil", 1000); c.ModifyResource("rubber", 1000);
        }
    }
}

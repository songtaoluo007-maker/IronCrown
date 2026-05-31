// ============================================================================
// Tests/BattleResolverC5Tests.cs — C5 战争代价集成测试
// 验证 WarTollResolver + PeaceResolver + BattleResolver 战争代价逻辑
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Tests
{
    /// <summary>简易确定性 RNG（测试用）</summary>
    internal class DeterministicRng : IRandom
    {
        private int _seed;
        public DeterministicRng(int seed = 42) { _seed = seed; }
        public int Seed => _seed;
        public ulong State => (ulong)_seed;
        public double NextDouble() { _seed = (_seed * 1103515245 + 12345) & 0x7fffffff; return (_seed % 1000) / 1000.0; }
        public int Next(int maxExclusive) { return (int)(NextDouble() * maxExclusive); }
        public int Range(int minInclusive, int maxExclusive) { return minInclusive + Next(maxExclusive - minInclusive); }
        public bool Roll(int percentChance) { return Next(100) < percentChance; }
        public double RangeDouble(double min, double max) { return min + NextDouble() * (max - min); }
        public void Reset() { _seed = 42; }
        public void Reset(int newSeed) { _seed = newSeed; }
        public void RestoreState(ulong state) { _seed = (int)state; }
    }

    /// <summary>简易配置注册表（测试用）</summary>
    internal class TestConfigRegistryC5 : IConfigRegistry
    {
        private readonly EconomyConfig _eco;
        public TestConfigRegistryC5(EconomyConfig eco) { _eco = eco; }
        public T Get<T>(string id) where T : class
        {
            if (typeof(T) == typeof(EconomyConfig) && id == "global") return _eco as T;
            return null;
        }
        public IReadOnlyList<T> All<T>() where T : class
        {
            if (typeof(T) == typeof(EconomyConfig)) return new[] { _eco as T } as IReadOnlyList<T>;
            return new List<T>();
        }
        public bool Has<T>(string id) where T : class => Get<T>(id) != null;
        public void LoadAll() { }
    }

    [TestFixture]
    public class BattleResolverC5Tests
    {
        private EconomyConfig _eco;
        private TestConfigRegistryC5 _config;

        [SetUp]
        public void SetUp()
        {
            _eco = new EconomyConfig
            {
                warStabilityPenaltyPerTurn = 1,
                warExhaustionPerTurn = 1,
                warSupportPenaltyPerLoss = 5,
                warSupportBonusPerVictory = 5,
                warSupportPenaltyPerCapitalLoss = 15,
                aiPeaceAcceptExhaustionThreshold = 30,
                aiPeaceAcceptPowerRatioPct = 80
            };
            _config = new TestConfigRegistryC5(_eco);
        }

        // ── WarTollResolver ──

        [Test]
        public void WarToll_AtWarCountries_LoseStabilityAndGainExhaustion()
        {
            var world = CreateWorld(out var countryA, out var countryB);
            world.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });

            var resolver = new WarTollResolver();
            resolver.ApplyTurnToll(world, _eco);

            Assert.AreEqual(99, countryA.stability);
            Assert.AreEqual(99, countryB.stability);
            Assert.AreEqual(1, countryA.warExhaustion);
            Assert.AreEqual(1, countryB.warExhaustion);
        }

        [Test]
        public void WarToll_NonWarCountries_Unaffected()
        {
            var world = CreateWorld(out var countryA, out var countryB);
            // 不添加战争关系

            var resolver = new WarTollResolver();
            resolver.ApplyTurnToll(world, _eco);

            Assert.AreEqual(100, countryA.stability);
            Assert.AreEqual(100, countryB.stability);
            Assert.AreEqual(0, countryA.warExhaustion);
            Assert.AreEqual(0, countryB.warExhaustion);
        }

        [Test]
        public void WarToll_Deterministic_SameResult()
        {
            var world1 = CreateWorld(out var a1, out var b1);
            world1.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });

            var world2 = CreateWorld(out var a2, out var b2);
            world2.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });

            var resolver = new WarTollResolver();
            resolver.ApplyTurnToll(world1, _eco);
            resolver.ApplyTurnToll(world2, _eco);

            Assert.AreEqual(a1.stability, a2.stability);
            Assert.AreEqual(a1.warExhaustion, a2.warExhaustion);
        }

        [Test]
        public void WarToll_ClampedAtZero()
        {
            var world = CreateWorld(out var countryA, out var countryB);
            countryA.stability = 0; // 已经最低
            world.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });

            var resolver = new WarTollResolver();
            resolver.ApplyTurnToll(world, _eco);

            Assert.AreEqual(0, countryA.stability); // 不会变负
        }

        // ── BattleResolver C5 战斗代价 ──

        [Test]
        public void BattleToll_AttackerWin_AttackerGainsSupport_DefenderLoses()
        {
            var world = CreateWorld(out var atk, out var def);
            atk.warSupport = 95; // avoid clamp at 100
            var battle = CreateBattle(world, atk, def, out var atkUnit, out var defUnit);

            // 制造攻方必胜：守方只有 1 org
            defUnit.organization = 1;

            var resolver = new BattleResolver(new DeterministicRng(42), new NoOpEventPublisher(), _config);
            resolver.TickBattles(world);

            Assert.AreEqual(100, atk.warSupport); // 95 + 5
            Assert.AreEqual(95, def.warSupport);  // 100 - 5
        }

        [Test]
        public void BattleToll_DefenderWin_DefenderGainsSupport_AttackerLoses()
        {
            var world = CreateWorld(out var atk, out var def);
            def.warSupport = 95; // avoid clamp at 100
            var battle = CreateBattle(world, atk, def, out var atkUnit, out var defUnit);

            // 制造守方必胜：攻方只有 1 org
            atkUnit.organization = 1;

            var resolver = new BattleResolver(new DeterministicRng(42), new NoOpEventPublisher(), _config);
            resolver.TickBattles(world);

            Assert.AreEqual(95, atk.warSupport);  // 100 - 5
            Assert.AreEqual(100, def.warSupport); // 95 + 5
        }

        // ── PeaceResolver ──

        [Test]
        public void Peace_AcceptsWhenExhausted()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });
            ai.warExhaustion = 60; // >= 30*2 = 60 → 无条件接受

            var events = new NoOpEventPublisher();
            var resolver = new PeaceResolver(events);

            var result = resolver.OfferPeace(world, "A", "B", _eco);
            Assert.IsTrue(result.accepted);
            Assert.AreEqual(0, world.warRelations.Count); // 战争已结束
        }

        [Test]
        public void Peace_RejectsWhenStrongAndFresh()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });
            ai.warExhaustion = 10; // 太低
            // AI 工厂比玩家多 → 实力强
            ai.civilianFactories = 20;
            player.civilianFactories = 5;

            var events = new NoOpEventPublisher();
            var resolver = new PeaceResolver(events);

            var result = resolver.OfferPeace(world, "A", "B", _eco);
            Assert.IsFalse(result.accepted);
            Assert.AreEqual(1, world.warRelations.Count); // 战争仍在
        }

        [Test]
        public void Peace_AcceptsWhenExhaustedAndWeak()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });
            ai.warExhaustion = 35; // >= 30
            // AI 弱 → 实力 ≤ 玩家 80%
            ai.civilianFactories = 2;
            player.civilianFactories = 10;

            var events = new NoOpEventPublisher();
            var resolver = new PeaceResolver(events);

            var result = resolver.OfferPeace(world, "A", "B", _eco);
            Assert.IsTrue(result.accepted);
        }

        [Test]
        public void Peace_RejectsNotAtWar()
        {
            var world = CreateWorld(out var player, out var ai);
            // 不添加战争关系

            var events = new NoOpEventPublisher();
            var resolver = new PeaceResolver(events);

            var result = resolver.OfferPeace(world, "A", "B", _eco);
            Assert.IsFalse(result.accepted);
            Assert.AreEqual("双方未处于战争状态", result.reason);
        }

        [Test]
        public void Peace_RejectsSelfPeace()
        {
            var world = CreateWorld(out var player, out var ai);

            var events = new NoOpEventPublisher();
            var resolver = new PeaceResolver(events);

            var result = resolver.OfferPeace(world, "A", "A", _eco);
            Assert.IsFalse(result.accepted);
            Assert.AreEqual("不能与自己停战", result.reason);
        }

        [Test]
        public void Peace_HalvesExhaustionOnAccept()
        {
            var world = CreateWorld(out var player, out var ai);
            world.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });
            player.warExhaustion = 40;
            ai.warExhaustion = 60;

            var events = new NoOpEventPublisher();
            var resolver = new PeaceResolver(events);

            resolver.OfferPeace(world, "A", "B", _eco);

            Assert.AreEqual(20, player.warExhaustion); // 40/2
            Assert.AreEqual(30, ai.warExhaustion);     // 60/2
        }

        // ── Capital Loss ──

        [Test]
        public void CapitalLoss_ReducesWarSupport()
        {
            var world = CreateWorld(out var atk, out var def);
            def.capitalProvinceId = "P1";

            var battle = CreateBattleAt(world, atk, def, "P1", out var atkUnit, out var defUnit);
            defUnit.organization = 1; // 必败

            var resolver = new BattleResolver(new DeterministicRng(42), new NoOpEventPublisher(), _config);
            resolver.TickBattles(world);

            // 首都被占：-15 warSupport + -5 战败 = -20
            Assert.AreEqual(80, def.warSupport); // 100 - 15 - 5
        }

        // ── WarRegistry.TryEndWar ──

        [Test]
        public void TryEndWar_RemovesRelation()
        {
            var world = CreateWorld(out _, out _);
            world.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });

            bool removed = WarRegistry.TryEndWar(world, "A", "B", out var rel);
            Assert.IsTrue(removed);
            Assert.AreEqual(0, world.warRelations.Count);
            Assert.AreEqual("A", rel.countryA);
            Assert.AreEqual("B", rel.countryB);
        }

        [Test]
        public void TryEndWar_NotFound_ReturnsFalse()
        {
            var world = CreateWorld(out _, out _);

            bool removed = WarRegistry.TryEndWar(world, "A", "B", out _);
            Assert.IsFalse(removed);
        }

        [Test]
        public void TryEndWar_ReverseOrder_Works()
        {
            var world = CreateWorld(out _, out _);
            world.warRelations.Add(new WarRelation { countryA = "A", countryB = "B" });

            bool removed = WarRegistry.TryEndWar(world, "B", "A", out _);
            Assert.IsTrue(removed);
        }

        // ── 辅助 ──

        private WorldState CreateWorld(out CountryState a, out CountryState b)
        {
            a = new CountryState { id = "A", name = "Alpha", stability = 100, warSupport = 100, civilianFactories = 5, militaryFactories = 3 };
            b = new CountryState { id = "B", name = "Beta", stability = 100, warSupport = 100, civilianFactories = 5, militaryFactories = 3 };
            a.resources["capital"] = 100;
            b.resources["capital"] = 100;

            var world = new WorldState
            {
                countries = new Dictionary<string, CountryState> { { "A", a }, { "B", b } },
                provinces = new Dictionary<string, ProvinceState>(),
                units = new Dictionary<string, UnitState>(),
                activeBattles = new List<ActiveBattle>(),
                warRelations = new List<WarRelation>()
            };

            // 两个邻接省份
            var p1 = new ProvinceState { id = "P1", controllerCountry = "B", neighbors = new[] { "P2" } };
            var p2 = new ProvinceState { id = "P2", controllerCountry = "A", neighbors = new[] { "P1" } };
            world.provinces["P1"] = p1;
            world.provinces["P2"] = p2;

            return world;
        }

        private ActiveBattle CreateBattle(WorldState world, CountryState atk, CountryState def,
            out UnitState atkUnit, out UnitState defUnit)
        {
            return CreateBattleAt(world, atk, def, "P1", out atkUnit, out defUnit);
        }

        private ActiveBattle CreateBattleAt(WorldState world, CountryState atk, CountryState def, string provinceId,
            out UnitState atkUnit, out UnitState defUnit)
        {
            atkUnit = new UnitState
            {
                id = "U_A",
                ownerCountry = "A",
                currentProvinceId = "P2",
                organization = 100, maxOrganization = 100,
                baseAttack = 10, baseDefense = 5,
                experience = 0, armor = 0, piercing = 0
            };
            defUnit = new UnitState
            {
                id = "U_B",
                ownerCountry = "B",
                currentProvinceId = provinceId,
                organization = 100, maxOrganization = 100,
                baseAttack = 10, baseDefense = 5,
                experience = 0, armor = 0, piercing = 0
            };
            world.units["U_A"] = atkUnit;
            world.units["U_B"] = defUnit;
            atk.unitIds.Add("U_A");
            def.unitIds.Add("U_B");

            var battle = new ActiveBattle
            {
                id = "U_A_vs_U_B",
                attackerUnitId = "U_A",
                defenderUnitId = "U_B",
                provinceId = provinceId,
                turnsElapsed = 0
            };
            world.activeBattles.Add(battle);
            return battle;
        }

        [Test]
        public void InitiateAttack_DuringTruce_Rejects()
        {
            var world = CreateWorld(out var atk, out var def);
            var unit = new UnitState
            {
                id = "unitA_1", unitType = "infantry", ownerCountry = "A",
                currentProvinceId = "P1", movesLeft = 2,
                manpower = 100, maxManpower = 100, equipment = 100, maxEquipment = 100,
                organization = 60, maxOrganization = 60,
                baseAttack = 10, baseDefense = 15, baseBreakthrough = 5, speed = 3
            };
            world.units[unit.id] = unit;
            world.provinces["P2"] = new ProvinceState
            {
                id = "P2", name = "Target", ownerCountry = "B", controllerCountry = "B",
                infrastructure = 1, neighbors = new string[] { "P1" }
            };
            var p1N = new System.Collections.Generic.List<string>(world.provinces["P1"].neighbors); p1N.Add("P2"); world.provinces["P1"].neighbors = p1N.ToArray();

            WarRegistry.SetTruce(world, "A", "B", 20);
            world.turnNumber = 10;

            var resolver = new BattleResolver(new DeterministicRng(42), new NoOpEventPublisher(), _config);
            var result = resolver.InitiateAttack(world, "unitA_1", "P2", "A");
            Assert.IsFalse(result.accepted, "和平期内开战应被拒");
            Assert.IsTrue(result.reason.Contains("和平期"), $"应提示和平期，实际: {result.reason}");
        }

        [Test]
        public void InitiateAttack_AfterTruceExpires_Succeeds()
        {
            var world = CreateWorld(out var atk, out var def);
            var unit = new UnitState
            {
                id = "unitA_1", unitType = "infantry", ownerCountry = "A",
                currentProvinceId = "P1", movesLeft = 2,
                manpower = 100, maxManpower = 100, equipment = 100, maxEquipment = 100,
                organization = 60, maxOrganization = 60,
                baseAttack = 10, baseDefense = 15, baseBreakthrough = 5, speed = 3
            };
            world.units[unit.id] = unit;
            world.provinces["P2"] = new ProvinceState
            {
                id = "P2", name = "Target", ownerCountry = "B", controllerCountry = "B",
                infrastructure = 1, neighbors = new string[] { "P1" }
            };
            var p1N = new System.Collections.Generic.List<string>(world.provinces["P1"].neighbors); p1N.Add("P2"); world.provinces["P1"].neighbors = p1N.ToArray();

            WarRegistry.SetTruce(world, "A", "B", 10);
            world.turnNumber = 10; // currentTurn >= untilTurn → 和平期已过

            var resolver = new BattleResolver(new DeterministicRng(42), new NoOpEventPublisher(), _config);
            var result = resolver.InitiateAttack(world, "unitA_1", "P2", "A");
            Assert.IsTrue(result.accepted, "和平期过后应可开战");
        }
    }

    /// <summary>丢弃事件的发布者（测试用）</summary>
    internal class NoOpEventPublisher : IEventPublisher
    {
        public void Publish<T>(T evt) { }
        public void Subscribe<T>(Action<T> handler) { }
        public void Unsubscribe<T>(Action<T> handler) { }
        public void Clear() { }
    }
}


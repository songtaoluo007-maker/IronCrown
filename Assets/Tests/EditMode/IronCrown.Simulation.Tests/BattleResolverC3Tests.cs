// ============================================================================
// BattleResolverC3Tests.cs — C3 战斗系统测试
// InitiateAttack + TickBattles + 占领 + 战斗锁定 + 存档往返
// ============================================================================

using NUnit.Framework;
using IronCrown.Contracts;
using IronCrown.Domain;
using IronCrown.Simulation;
using System.Collections.Generic;
using System.Linq;

namespace IronCrown.Simulation.Tests
{
    public class BattleResolverC3Tests
    {
        private EventBus _events;
        private DeterministicRng _rng;

        [SetUp]
        public void SetUp()
        {
            _events = new EventBus();
            _rng = new DeterministicRng(12345);
        }

        private (WorldState world, UnitState attacker, UnitState defender, ProvinceState target) BuildWorldWithUnits(
            string attackerOwner = "empire", string defenderOwner = "republic",
            int atkMovesLeft = 1, int defMovesLeft = 1)
        {
            var world = new WorldState();

            var atkCountry = new CountryState { id = attackerOwner, name = attackerOwner };
            var defCountry = new CountryState { id = defenderOwner, name = defenderOwner };
            world.countries[attackerOwner] = atkCountry;
            world.countries[defenderOwner] = defCountry;

            var origin = new ProvinceState
            {
                id = "origin", name = "Origin", terrain = TerrainType.Plain,
                ownerCountry = attackerOwner, controllerCountry = attackerOwner,
                isCapital = true, neighbors = new[] { "target" }
            };
            var target = new ProvinceState
            {
                id = "target", name = "Target", terrain = TerrainType.Plain,
                ownerCountry = defenderOwner, controllerCountry = defenderOwner,
                isCapital = true, neighbors = new[] { "origin" }
            };
            world.provinces["origin"] = origin;
            world.provinces["target"] = target;

            var attacker = CreateUnit("atk_1", attackerOwner, "origin", atkMovesLeft);
            var defender = CreateUnit("def_1", defenderOwner, "target", defMovesLeft);
            world.units[attacker.id] = attacker;
            world.units[defender.id] = defender;
            atkCountry.unitIds.Add(attacker.id);
            defCountry.unitIds.Add(defender.id);

            return (world, attacker, defender, target);
        }

        private static UnitState CreateUnit(string id, string owner, string province, int movesLeft = 1)
        {
            return new UnitState
            {
                id = id, unitType = "infantry", ownerCountry = owner,
                currentProvinceId = province, movesLeft = movesLeft, speed = 3,
                manpower = 100, maxManpower = 100, equipment = 100, maxEquipment = 100,
                organization = 60, maxOrganization = 60,
                baseAttack = 10, baseDefense = 15, baseBreakthrough = 5
            };
        }

        [Test]
        public void InitiateAttack_WithDefender_CreatesActiveBattle()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            var result = resolver.InitiateAttack(world, "atk_1", "target", "empire");

            Assert.IsTrue(result.accepted);
            Assert.AreEqual(1, world.activeBattles.Count);
            Assert.AreEqual("atk_1", world.activeBattles[0].attackerUnitId);
            Assert.AreEqual("def_1", world.activeBattles[0].defenderUnitId);
            Assert.AreEqual("target", world.activeBattles[0].provinceId);
            Assert.AreEqual(0, world.activeBattles[0].turnsElapsed);
            Assert.AreEqual(0, atk.movesLeft, "发起攻击应消耗移动力");
        }

        [Test]
        public void InitiateAttack_EmptyProvince_InstantOccupation()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            world.units.Remove("def_1");
            world.countries["republic"].unitIds.Remove("def_1");

            var result = resolver.InitiateAttack(world, "atk_1", "target", "empire");

            Assert.IsTrue(result.accepted);
            Assert.AreEqual(0, world.activeBattles.Count, "空城不应创建战斗");
            Assert.AreEqual("empire", target.controllerCountry, "应立即占领");
            Assert.AreEqual("target", atk.currentProvinceId, "攻方应进驻");
        }

        [Test]
        public void InitiateAttack_NonAdjacent_Rejects()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            world.provinces["far"] = new ProvinceState
            {
                id = "far", name = "Far", terrain = TerrainType.Plain,
                ownerCountry = "republic", controllerCountry = "republic",
                neighbors = new string[0]
            };
            var result = resolver.InitiateAttack(world, "atk_1", "far", "empire");

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("非邻接省份", result.reason);
        }

        [Test]
        public void InitiateAttack_NotOwned_Rejects()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            var result = resolver.InitiateAttack(world, "atk_1", "target", "republic");

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("非己方部队", result.reason);
        }

        [Test]
        public void InitiateAttack_NoMovesLeft_Rejects()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits(atkMovesLeft: 0);

            var result = resolver.InitiateAttack(world, "atk_1", "target", "empire");

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("移动力不足", result.reason);
        }

        [Test]
        public void InitiateAttack_AlreadyInBattle_Rejects()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            resolver.InitiateAttack(world, "atk_1", "target", "empire");

            var def2 = CreateUnit("def_2", "republic", "target");
            world.units["def_2"] = def2;
            world.countries["republic"].unitIds.Add("def_2");

            var result = resolver.InitiateAttack(world, "atk_1", "target", "empire");
            Assert.IsFalse(result.accepted);
            Assert.AreEqual("部队正在战斗中", result.reason);
        }

        [Test]
        public void InitiateAttack_OwnedProvince_Rejects()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            target.controllerCountry = "empire";

            var result = resolver.InitiateAttack(world, "atk_1", "target", "empire");

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("非敌方控制省份", result.reason);
        }

        [Test]
        public void TickBattles_AttackerWins_OccupiesProvince()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            resolver.InitiateAttack(world, "atk_1", "target", "empire");
            def.organization = 1;

            resolver.TickBattles(world);

            Assert.AreEqual(0, world.activeBattles.Count, "战斗应结束");
            Assert.IsFalse(world.units.ContainsKey("def_1"), "守方应被消灭");
            Assert.AreEqual("empire", target.controllerCountry, "应占领目标省");
            Assert.AreEqual("target", atk.currentProvinceId, "攻方应进驻");
        }

        [Test]
        public void TickBattles_DefenderWins_AttackerDestroyed()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            resolver.InitiateAttack(world, "atk_1", "target", "empire");
            atk.organization = 1;

            resolver.TickBattles(world);

            Assert.AreEqual(0, world.activeBattles.Count);
            Assert.IsFalse(world.units.ContainsKey("atk_1"), "攻方应被消灭");
            Assert.AreEqual("republic", target.controllerCountry, "守方应守住");
        }

        [Test]
        public void TickBattles_Draw_BothDestroyed()
        {
            var resolver = new BattleResolver(new FixedResultRng(0.8), _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            atk.organization = 1;
            def.organization = 1;

            resolver.InitiateAttack(world, "atk_1", "target", "empire");
            resolver.TickBattles(world);

            Assert.IsFalse(world.units.ContainsKey("atk_1") || world.units.ContainsKey("def_1"),
                "平局双方应都被消灭");
            Assert.AreEqual("republic", target.controllerCountry, "平局省不变");
        }

        [Test]
        public void TickBattles_AttackerWins_ClearsOtherDefenders()
        {
            var resolver = new BattleResolver(_rng, _events);
            var (world, atk, def, target) = BuildWorldWithUnits();

            var def2 = CreateUnit("def_2", "republic", "target");
            world.units["def_2"] = def2;
            world.countries["republic"].unitIds.Add("def_2");

            resolver.InitiateAttack(world, "atk_1", "target", "empire");
            def.organization = 1;

            resolver.TickBattles(world);

            Assert.IsFalse(world.units.ContainsKey("def_2"), "非参战守方也应被消灭（清场）");
        }

        [Test]
        public void ActiveBattle_SortedById()
        {
            var resolver = new BattleResolver(_rng, _events);
            var world = new WorldState();

            world.countries["a"] = new CountryState { id = "a" };
            world.countries["b"] = new CountryState { id = "b" };
            world.countries["c"] = new CountryState { id = "c" };

            var p1 = new ProvinceState { id = "p1", ownerCountry = "b", controllerCountry = "b", neighbors = new[] { "p2" } };
            var p2 = new ProvinceState { id = "p2", ownerCountry = "b", controllerCountry = "b", neighbors = new[] { "p1", "p3" } };
            var p3 = new ProvinceState { id = "p3", ownerCountry = "c", controllerCountry = "c", neighbors = new[] { "p2" } };
            world.provinces["p1"] = p1;
            world.provinces["p2"] = p2;
            world.provinces["p3"] = p3;

            var u1 = CreateUnit("z_unit", "a", "p1");
            var u2 = CreateUnit("a_unit", "b", "p2");
            var u3 = CreateUnit("m_unit", "a", "p2");
            world.units["z_unit"] = u1;
            world.units["a_unit"] = u2;
            world.units["m_unit"] = u3;
            world.countries["a"].unitIds.AddRange(new[] { "z_unit", "m_unit" });
            world.countries["b"].unitIds.Add("a_unit");

            resolver.InitiateAttack(world, "z_unit", "p2", "a");
            resolver.InitiateAttack(world, "m_unit", "p3", "a");

            Assert.AreEqual(2, world.activeBattles.Count);
            Assert.IsTrue(string.Compare(world.activeBattles[0].id, world.activeBattles[1].id, System.StringComparison.Ordinal) < 0);
        }
    }

    internal class FixedResultRng : IRandom
    {
        private readonly double _fixedValue;
        public int State { get; set; }

        public FixedResultRng(double fixedValue) { _fixedValue = fixedValue; }
        public int Next(int max) => (int)(_fixedValue * max);
        public void Reset(int seed) { }
        public void RestoreState(int state) { }
    }
}

// ============================================================================
// SpatialIndexTests.cs - P2.5 Spatial Index Tests
// R1 fix: use session.IssueCommand(MoveUnit), no manual index manipulation
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;
using IronCrown.Application;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace IronCrown.Simulation.Tests
{
    public class SpatialIndexTests
    {
        private WorldState _world;

        [SetUp]
        public void SetUp()
        {
            _world = new WorldState();
        }

        private void AddProvince(string id, TerrainType terrain, int gridX, int gridY, params string[] neighbors)
        {
            _world.provinces[id] = new ProvinceState
            {
                id = id, name = id, terrain = terrain,
                gridX = gridX, gridY = gridY,
                neighbors = neighbors
            };
        }

        private void AddUnit(string id, string owner, string provinceId, int movesLeft = 3)
        {
            _world.units[id] = new UnitState
            {
                id = id, unitType = "infantry", ownerCountry = owner,
                currentProvinceId = provinceId,
                movesLeft = movesLeft, speed = 3,
                organization = 60
            };
        }

        private void VerifyIndexConsistency()
        {
            foreach (var prov in _world.provinces.Values)
            {
                var indexUnits = _world.GetUnitsInProvince(prov.id);
                var traversalUnits = _world.units.Values
                    .Where(u => u.currentProvinceId == prov.id)
                    .Select(u => u.id)
                    .ToList();

                Assert.AreEqual(traversalUnits.Count, indexUnits.Count,
                    string.Format("Count mismatch for province {0}", prov.id));
                foreach (var uid in traversalUnits)
                {
                    Assert.IsTrue(indexUnits.Contains(uid),
                        string.Format("Unit {0} missing from index for province {1}", uid, prov.id));
                }
            }
        }

        [Test]
        public void EmptyState_NoProvinces()
        {
            _world.RebuildProvinceUnitIndex();
            Assert.AreEqual(0, _world.GetUnitsInProvince("nonexistent").Count);
        }

        [Test]
        public void SingleUnit_Indexed()
        {
            AddProvince("p1", TerrainType.Plain, 0, 0);
            AddUnit("u1", "c1", "p1");
            _world.RebuildProvinceUnitIndex();
            Assert.IsTrue(_world.GetUnitsInProvince("p1").Contains("u1"));
        }

        [Test]
        public void MultipleUnits_SameProvince()
        {
            AddProvince("p1", TerrainType.Plain, 0, 0);
            AddUnit("u1", "c1", "p1");
            AddUnit("u2", "c2", "p1");
            _world.RebuildProvinceUnitIndex();
            var units = _world.GetUnitsInProvince("p1");
            Assert.AreEqual(2, units.Count);
        }

        [Test]
        public void MultipleProvinces_Isolated()
        {
            AddProvince("p1", TerrainType.Plain, 0, 0);
            AddProvince("p2", TerrainType.Forest, 1, 0);
            AddUnit("u1", "c1", "p1");
            AddUnit("u2", "c2", "p2");
            _world.RebuildProvinceUnitIndex();
            Assert.IsTrue(_world.GetUnitsInProvince("p1").Contains("u1"));
            Assert.IsFalse(_world.GetUnitsInProvince("p1").Contains("u2"));
            Assert.IsTrue(_world.GetUnitsInProvince("p2").Contains("u2"));
            Assert.IsFalse(_world.GetUnitsInProvince("p2").Contains("u1"));
        }

        [Test]
        public void Rebuild_ClearsOldState()
        {
            AddProvince("p1", TerrainType.Plain, 0, 0);
            AddUnit("u1", "c1", "p1");
            _world.RebuildProvinceUnitIndex();
            Assert.IsTrue(_world.GetUnitsInProvince("p1").Contains("u1"));

            _world.units["u1"].currentProvinceId = "p2";
            _world.provinces["p2"] = new ProvinceState { id = "p2", name = "p2", terrain = TerrainType.Forest };
            _world.RebuildProvinceUnitIndex();

            Assert.IsFalse(_world.GetUnitsInProvince("p1").Contains("u1"));
            Assert.IsTrue(_world.GetUnitsInProvince("p2").Contains("u1"));
        }

        [Test]
        public void DeadUnit_StillIndexed_ByDesign()
        {
            // RebuildProvinceUnitIndex indexes ALL units regardless of organization.
            // Dead units (organization=0) are kept in the index until explicitly removed.
            AddProvince("p1", TerrainType.Plain, 0, 0);
            AddUnit("u1", "c1", "p1");
            _world.units["u1"].organization = 0;
            _world.RebuildProvinceUnitIndex();
            Assert.IsTrue(_world.GetUnitsInProvince("p1").Contains("u1"),
                "Units with org=0 are still indexed (by design)");
        }

        [Test]
        public void MixedOwnerUnits_SameProvince()
        {
            AddProvince("p1", TerrainType.Plain, 0, 0, "p2");
            AddProvince("p2", TerrainType.Forest, 1, 0, "p1");
            AddUnit("u1", "c1", "p1");
            AddUnit("u2", "c2", "p1");
            _world.RebuildProvinceUnitIndex();
            Assert.AreEqual(2, _world.GetUnitsInProvince("p1").Count);
        }

        [Test]
        public void LargeScale_1000Units()
        {
            for (int i = 0; i < 10; i++)
                AddProvince("p" + i, TerrainType.Plain, i, 0);
            for (int i = 0; i < 1000; i++)
                AddUnit("u" + i, "c1", "p" + (i % 10));
            _world.RebuildProvinceUnitIndex();
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(100, _world.GetUnitsInProvince("p" + i).Count);
        }

        // =================================================================
        // R1: real IssueCommand(MoveUnit) driven test
        // =================================================================

        [Test]
        public void Index_AfterMoveCommand_MatchesTraversal()
        {
            // Create real session
            var session = TestSessionFactory.Create();
            session.NewGame(12345);

            var world = session.DebugWorld;
            Assert.IsNotNull(world, "DebugWorld should not be null after NewGame");

            // TestConfigRegistry is empty so NewGame creates no countries.
            // Inject minimal data: country, economy config, 2 provinces, 1 unit.
            var playerCountry = "test_country";
            world.countries[playerCountry] = new CountryState
            {
                id = playerCountry,
                treasury = 1000,
                stability = 50,
                warSupport = 50
            };

            // Register economy config so IssueCommand passes the eco != null check
            var testEco = new EconomyConfig();
            var cfgField = typeof(GameSessionService).GetField("_config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cfg = (TestConfigRegistry)cfgField.GetValue(session);
            cfg.Register("global", testEco);
            world.provinces["prov_a"] = new ProvinceState
            {
                id = "prov_a", name = "A",
                terrain = TerrainType.Plain, gridX = 0, gridY = 0,
                neighbors = new[] { "prov_b" },
                ownerCountry = playerCountry, controllerCountry = playerCountry
            };
            world.provinces["prov_b"] = new ProvinceState
            {
                id = "prov_b", name = "B",
                terrain = TerrainType.Plain, gridX = 1, gridY = 0,
                neighbors = new[] { "prov_a" },
                ownerCountry = playerCountry, controllerCountry = playerCountry
            };
            var unitId = "unit_1";
            world.units[unitId] = new UnitState
            {
                id = unitId, unitType = "infantry",
                ownerCountry = playerCountry,
                currentProvinceId = "prov_a",
                movesLeft = 3, speed = 3,
                manpower = 1000, maxManpower = 1000,
                equipment = 100, maxEquipment = 100,
                organization = 60, maxOrganization = 60
            };

            // Set player country and rebuild index
            session.SetPlayerCountry(playerCountry);
            world.RebuildProvinceUnitIndex();

            // Verify preconditions
            Assert.IsTrue(world.GetUnitsInProvince("prov_a").Contains(unitId),
                "Unit should be in prov_a before move");

            // R1 core: drive move via IssueCommand
            var result = session.IssueCommand(new GameCommand
            {
                commandType = CommandType.MoveUnit,
                countryId = playerCountry,
                unitId = unitId,
                targetProvinceId = "prov_b"
            });
            Assert.IsTrue(result.accepted, "MoveUnit should be accepted: " + result.reason);

            // Verify: unit removed from source province
            Assert.IsFalse(world.GetUnitsInProvince("prov_a").Contains(unitId),
                "Unit should NOT be in prov_a after move");

            // Verify: unit added to target province
            Assert.IsTrue(world.GetUnitsInProvince("prov_b").Contains(unitId),
                "Unit SHOULD be in prov_b after move");

            // Verify: full index consistency
            VerifyIndexConsistency();
        }
    }
}

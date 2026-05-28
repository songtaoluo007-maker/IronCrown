// ============================================================================
// Tests/EditMode/SaveMapperTests.cs — SaveMapper 往返测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Application;

namespace IronCrown.Application.Tests
{
    public class SaveMapperTests
    {
        [Test]
        public void RoundTrip_CountryData_Preserved()
        {
            var world = new WorldState();
            var c = new CountryState
            {
                id = "c1",
                treasury = 500,
                stability = 75,
                warSupport = 60
            };
            c.activePolicies.Add("policy_a");
            c.completedTechs.Add("tech_x");
            world.countries["c1"] = c;

            var save = SaveMapper.ToSave(world, 42, GamePhase.TurnStart);
            var restored = SaveMapper.ToRuntime(save);

            Assert.IsTrue(restored.countries.ContainsKey("c1"));
            Assert.AreEqual(500, restored.countries["c1"].treasury);
            Assert.AreEqual(75, restored.countries["c1"].stability);
            Assert.AreEqual(60, restored.countries["c1"].warSupport);
            Assert.IsTrue(restored.countries["c1"].activePolicies.Contains("policy_a"));
            Assert.IsTrue(restored.countries["c1"].completedTechs.Contains("tech_x"));
        }

        [Test]
        public void RoundTrip_ProvinceData_Preserved()
        {
            var world = new WorldState();
            var p = new ProvinceState
            {
                id = "p1",
                ownerCountry = "c1",
                controllerCountry = "c1",
                resistance = 10,
                compliance = 80
            };
            world.provinces["p1"] = p;

            var save = SaveMapper.ToSave(world, 42, GamePhase.TurnStart);
            var restored = SaveMapper.ToRuntime(save);

            Assert.IsTrue(restored.provinces.ContainsKey("p1"));
            Assert.AreEqual("c1", restored.provinces["p1"].ownerCountry);
            Assert.AreEqual(10, restored.provinces["p1"].resistance);
            Assert.AreEqual(80, restored.provinces["p1"].compliance);
        }

        [Test]
        public void RoundTrip_UnitData_Preserved()
        {
            var world = new WorldState();
            var u = new UnitState
            {
                id = "u1",
                unitType = "infantry",
                ownerCountry = "c1",
                currentProvinceId = "p1",
                manpower = 1000,
                equipment = 500,
                organization = 80
            };
            world.units["u1"] = u;

            var save = SaveMapper.ToSave(world, 42, GamePhase.TurnStart);
            var restored = SaveMapper.ToRuntime(save);

            Assert.IsTrue(restored.units.ContainsKey("u1"));
            Assert.AreEqual("infantry", restored.units["u1"].unitType);
            Assert.AreEqual(1000, restored.units["u1"].manpower);
            Assert.AreEqual(80, restored.units["u1"].organization);
        }
    }
}

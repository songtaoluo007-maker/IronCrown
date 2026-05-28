// ============================================================================
// Tests/EditMode/WorldStateTests.cs — WorldState 字典操作测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;

namespace IronCrown.Domain.Tests
{
    public class WorldStateTests
    {
        [Test]
        public void AddCountry_RetrieveById()
        {
            var world = new WorldState();
            var c = new CountryState { id = "test", name = "Testland" };
            world.countries["test"] = c;

            Assert.IsTrue(world.countries.ContainsKey("test"));
            Assert.AreEqual("Testland", world.countries["test"].name);
        }

        [Test]
        public void AddUnit_RemoveUnit()
        {
            var world = new WorldState();
            world.units["u1"] = new UnitState { id = "u1" };
            Assert.IsTrue(world.units.ContainsKey("u1"));

            world.units.Remove("u1");
            Assert.IsFalse(world.units.ContainsKey("u1"));
        }

        [Test]
        public void AddTension_CapsAt100()
        {
            var world = new WorldState();
            world.AddTension(60);
            world.AddTension(60);
            Assert.AreEqual(100, world.worldTension);
        }

        [Test]
        public void TensionAllows_Threshold()
        {
            var world = new WorldState { worldTension = 50 };
            Assert.IsTrue(world.TensionAllows(50));
            Assert.IsFalse(world.TensionAllows(51));
        }
    }
}

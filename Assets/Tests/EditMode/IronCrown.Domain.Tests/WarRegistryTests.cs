// ============================================================================
// Tests/WarRegistryTests.cs — WarRegistry 停战和平期测试 (C9d)
// ============================================================================

using System.Collections.Generic;
using IronCrown.Domain;
using NUnit.Framework;

namespace IronCrown.Domain.Tests
{
    [TestFixture]
    public class WarRegistryTests
    {
        private WorldState CreateWorld()
        {
            return new WorldState
            {
                countries = new Dictionary<string, CountryState>(),
                provinces = new Dictionary<string, ProvinceState>(),
                units = new Dictionary<string, UnitState>(),
                warRelations = new List<WarRelation>(),
                activeBattles = new List<ActiveBattle>(),
                truceUntilTurn = new Dictionary<string, int>()
            };
        }

        [Test]
        public void SetTruce_StoresOrdinalNormalized()
        {
            var world = CreateWorld();
            WarRegistry.SetTruce(world, "republic_west", "empire_north", 15);

            // key 应为 empire_north_vs_republic_west（Ordinal 升序）
            Assert.IsTrue(world.truceUntilTurn.ContainsKey("empire_north_vs_republic_west"));
            Assert.AreEqual(15, world.truceUntilTurn["empire_north_vs_republic_west"]);

            // 反向查询也应一致
            WarRegistry.SetTruce(world, "empire_north", "republic_west", 20);
            Assert.AreEqual(20, world.truceUntilTurn["empire_north_vs_republic_west"]);
        }

        [Test]
        public void IsInTruce_BeforeUntilTurn_ReturnsTrue()
        {
            var world = CreateWorld();
            WarRegistry.SetTruce(world, "empire_north", "republic_west", 15);

            Assert.IsTrue(WarRegistry.IsInTruce(world, "empire_north", "republic_west", 10));
            Assert.IsTrue(WarRegistry.IsInTruce(world, "republic_west", "empire_north", 14));
        }

        [Test]
        public void IsInTruce_AfterUntilTurn_ReturnsFalse()
        {
            var world = CreateWorld();
            WarRegistry.SetTruce(world, "empire_north", "republic_west", 15);

            Assert.IsFalse(WarRegistry.IsInTruce(world, "empire_north", "republic_west", 15));
            Assert.IsFalse(WarRegistry.IsInTruce(world, "empire_north", "republic_west", 20));
        }

        [Test]
        public void IsInTruce_NeverSet_ReturnsFalse()
        {
            var world = CreateWorld();
            Assert.IsFalse(WarRegistry.IsInTruce(world, "empire_north", "republic_west", 1));
        }
    }
}

// ============================================================================
// Tests/EditMode/IronCrown.Simulation.Tests/MovementResolverTests.cs
// C2b：MovementResolver 移动校验 & 移动力重置
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Simulation.Tests
{
    [TestFixture]
    public class MovementResolverTests
    {
        [Test]
        public void TryMove_Valid_MovesAndDeductsMovesLeft()
        {
            var world = BuildWorldWithUnits();
            var resolver = new MovementResolver();

            var result = resolver.TryMove(world, "empire_north_inf_1", "coal_basin", "empire_north");

            Assert.IsTrue(result.accepted);
            Assert.AreEqual("coal_basin", world.units["empire_north_inf_1"].currentProvinceId);
            Assert.AreEqual(2, world.units["empire_north_inf_1"].movesLeft);
        }

        [Test]
        public void TryMove_NotNeighbor_Rejects()
        {
            var world = BuildWorldWithUnits();
            var resolver = new MovementResolver();

            // iron_city 邻接 coal_basin 和 red_plain，不邻接 coral_bay
            var result = resolver.TryMove(world, "empire_north_inf_1", "coral_bay", "empire_north");

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("非邻接省份", result.reason);
            Assert.AreEqual("iron_city", world.units["empire_north_inf_1"].currentProvinceId);
            Assert.AreEqual(3, world.units["empire_north_inf_1"].movesLeft);
        }

        [Test]
        public void TryMove_EnemyControlled_Rejects()
        {
            var world = BuildWorldWithUnits();
            var resolver = new MovementResolver();

            // red_plain 归 alliance_east（敌方）
            var result = resolver.TryMove(world, "empire_north_inf_1", "red_plain", "empire_north");

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("非己方控制省份", result.reason);
        }

        [Test]
        public void TryMove_NotOwnedByPlayer_Rejects()
        {
            var world = BuildWorldWithUnits();
            var resolver = new MovementResolver();

            // 尝试移动 alliance_east 的部队，但 playerCountryId = empire_north
            var result = resolver.TryMove(world, "alliance_east_inf_1", "red_plain", "empire_north");

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("非己方部队", result.reason);
        }

        [Test]
        public void TryMove_NoMovesLeft_Rejects()
        {
            var world = BuildWorldWithUnits();
            var resolver = new MovementResolver();
            world.units["empire_north_inf_1"].movesLeft = 0;

            var result = resolver.TryMove(world, "empire_north_inf_1", "coal_basin", "empire_north");

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("移动力不足", result.reason);
        }

        [Test]
        public void TryMove_UnitNotFound_Rejects()
        {
            var world = BuildWorldWithUnits();
            var resolver = new MovementResolver();

            var result = resolver.TryMove(world, "nonexistent", "iron_city", "empire_north");

            Assert.IsFalse(result.accepted);
            Assert.AreEqual("部队不存在", result.reason);
        }

        [Test]
        public void TryMove_ConsecutiveSteps_DepleteMoves()
        {
            var world = BuildWorldWithUnits();
            var resolver = new MovementResolver();
            // empire_north_inf_1 在 iron_city，speed=3
            // iron_city -> coal_basin -> west_plain（需要 empire_north 控制 coal_basin 和 west_plain）

            // 确保 coal_basin 和 west_plain 都归 empire_north 控制
            world.provinces["coal_basin"].controllerCountry = "empire_north";
            world.provinces["west_plain"].controllerCountry = "empire_north";

            // 第 1 步
            var r1 = resolver.TryMove(world, "empire_north_inf_1", "coal_basin", "empire_north");
            Assert.IsTrue(r1.accepted);
            Assert.AreEqual(2, world.units["empire_north_inf_1"].movesLeft);

            // 第 2 步
            var r2 = resolver.TryMove(world, "empire_north_inf_1", "west_plain", "empire_north");
            Assert.IsTrue(r2.accepted);
            Assert.AreEqual(1, world.units["empire_north_inf_1"].movesLeft);

            // 第 3 步（回到 coal_basin）
            var r3 = resolver.TryMove(world, "empire_north_inf_1", "coal_basin", "empire_north");
            Assert.IsTrue(r3.accepted);
            Assert.AreEqual(0, world.units["empire_north_inf_1"].movesLeft);

            // 第 4 步 — 无移动力
            var r4 = resolver.TryMove(world, "empire_north_inf_1", "iron_city", "empire_north");
            Assert.IsFalse(r4.accepted);
            Assert.AreEqual("移动力不足", r4.reason);
        }

        [Test]
        public void ResetMovement_RestoresToSpeed()
        {
            var world = BuildWorldWithUnits();
            var resolver = new MovementResolver();

            // 手动消耗移动力
            world.units["empire_north_inf_1"].movesLeft = 0;
            world.units["alliance_east_inf_1"].movesLeft = 1;

            resolver.ResetMovement(world);

            foreach (var u in world.units.Values)
            {
                Assert.AreEqual(u.speed, u.movesLeft, $"unit {u.id} movesLeft should equal speed");
            }
        }

        [Test]
        public void ResetMovement_DeterministicOrder()
        {
            var world = new WorldState();
            // 按乱序加入，验证内部按 id 升序遍历
            world.units["z_unit"] = new UnitState { id = "z_unit", speed = 5, movesLeft = 0 };
            world.units["a_unit"] = new UnitState { id = "a_unit", speed = 3, movesLeft = 0 };
            world.units["m_unit"] = new UnitState { id = "m_unit", speed = 7, movesLeft = 0 };

            var resolver = new MovementResolver();
            resolver.ResetMovement(world);

            Assert.AreEqual(3, world.units["a_unit"].movesLeft);
            Assert.AreEqual(7, world.units["m_unit"].movesLeft);
            Assert.AreEqual(5, world.units["z_unit"].movesLeft);
        }

        // === 辅助方法 ===

        private static WorldState BuildWorldWithUnits()
        {
            var world = new WorldState();

            // 省份：iron_city -> coal_basin（邻接），red_plain（邻接 iron_city，敌方）
            world.provinces["iron_city"] = new ProvinceState
            {
                id = "iron_city", name = "铁都",
                ownerCountry = "empire_north", controllerCountry = "empire_north",
                neighbors = new[] { "coal_basin", "red_plain" },
                gridX = 1, gridY = 0
            };
            world.provinces["coal_basin"] = new ProvinceState
            {
                id = "coal_basin", name = "煤盆",
                ownerCountry = "empire_north", controllerCountry = "empire_north",
                neighbors = new[] { "iron_city", "west_plain" },
                gridX = 2, gridY = 0
            };
            world.provinces["red_plain"] = new ProvinceState
            {
                id = "red_plain", name = "赤原",
                ownerCountry = "alliance_east", controllerCountry = "alliance_east",
                neighbors = new[] { "iron_city" },
                gridX = 0, gridY = 1
            };
            world.provinces["west_plain"] = new ProvinceState
            {
                id = "west_plain", name = "西原",
                ownerCountry = "republic_west", controllerCountry = "republic_west",
                neighbors = new[] { "coal_basin" },
                gridX = 3, gridY = 0
            };
            world.provinces["coral_bay"] = new ProvinceState
            {
                id = "coral_bay", name = "珊瑚湾",
                ownerCountry = "kingdom_south", controllerCountry = "kingdom_south",
                neighbors = new string[0],
                gridX = 0, gridY = 2
            };

            // 部队
            world.units["empire_north_inf_1"] = new UnitState
            {
                id = "empire_north_inf_1", unitType = "infantry",
                ownerCountry = "empire_north", currentProvinceId = "iron_city",
                speed = 3, movesLeft = 3, manpower = 100, maxManpower = 100,
                organization = 60, maxOrganization = 60
            };
            world.units["alliance_east_inf_1"] = new UnitState
            {
                id = "alliance_east_inf_1", unitType = "infantry",
                ownerCountry = "alliance_east", currentProvinceId = "red_plain",
                speed = 3, movesLeft = 3, manpower = 100, maxManpower = 100,
                organization = 60, maxOrganization = 60
            };

            return world;
        }
    }
}

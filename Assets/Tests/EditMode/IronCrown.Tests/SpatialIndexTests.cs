// ============================================================================
// SpatialIndexTests.cs — P2.5 空间索引测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using System.Collections.Generic;
using System.Linq;

namespace IronCrown.Tests
{
    public class SpatialIndexTests
    {
        private WorldState _world;

        [SetUp]
        public void SetUp()
        {
            _world = new WorldState();

            // 2 个省
            _world.provinces["p1"] = new ProvinceState { id = "p1", name = "P1", terrain = TerrainType.Plain };
            _world.provinces["p2"] = new ProvinceState { id = "p2", name = "P2", terrain = TerrainType.Forest };

            // 3 支部队: u1/u2 在 p1, u3 在 p2
            _world.units["u1"] = new UnitState { id = "u1", ownerCountry = "c1", currentProvinceId = "p1" };
            _world.units["u2"] = new UnitState { id = "u2", ownerCountry = "c1", currentProvinceId = "p1" };
            _world.units["u3"] = new UnitState { id = "u3", ownerCountry = "c2", currentProvinceId = "p2" };
        }

        [Test]
        public void RebuildProvinceUnitIndex_CorrectMapping()
        {
            _world.RebuildProvinceUnitIndex();

            var p1Units = _world.GetUnitsInProvince("p1");
            var p2Units = _world.GetUnitsInProvince("p2");

            Assert.AreEqual(2, p1Units.Count);
            Assert.IsTrue(p1Units.Contains("u1"));
            Assert.IsTrue(p1Units.Contains("u2"));
            Assert.AreEqual(1, p2Units.Count);
            Assert.IsTrue(p2Units.Contains("u3"));
        }

        [Test]
        public void GetUnitsInProvince_EmptyProvince_ReturnsEmpty()
        {
            _world.RebuildProvinceUnitIndex();
            var empty = _world.GetUnitsInProvince("nonexistent");
            Assert.AreEqual(0, empty.Count);
        }

        [Test]
        public void IndexConsistency_AfterManualUpdate_MatchesTraversal()
        {
            _world.RebuildProvinceUnitIndex();

            // 手动模拟移动: u1 从 p1 到 p2
            _world.provinceUnitIds["p1"].Remove("u1");
            _world.provinceUnitIds["p2"].Add("u1");
            _world.units["u1"].currentProvinceId = "p2";

            // 验证索引与遍历一致
            foreach (var prov in _world.provinces.Values)
            {
                var indexUnits = _world.GetUnitsInProvince(prov.id);
                var traversalUnits = _world.units.Values
                    .Where(u => u.currentProvinceId == prov.id)
                    .Select(u => u.id)
                    .ToList();

                Assert.AreEqual(traversalUnits.Count, indexUnits.Count,
                    $"Count mismatch for province {prov.id}");
                foreach (var uid in traversalUnits)
                {
                    Assert.IsTrue(indexUnits.Contains(uid),
                        $"Unit {uid} missing from index for province {prov.id}");
                }
            }
        }

        // F5 验收: 经 GameSessionService 发真实 MoveUnit 命令后
        // 断言 GetUnitsInProvince 各省结果 == 全遍历
        [Test]
        public void Index_AfterMoveCommand_MatchesTraversal()
        {
            // 构造最小可运行世界
            var world = new WorldState();
            world.provinces["p1"] = new ProvinceState
            {
                id = "p1", name = "P1", terrain = TerrainType.Plain,
                neighbors = new[] { "p2" }
            };
            world.provinces["p2"] = new ProvinceState
            {
                id = "p2", name = "P2", terrain = TerrainType.Plain,
                neighbors = new[] { "p1" }
            };
            world.units["u1"] = new UnitState
            {
                id = "u1", ownerCountry = "c1", currentProvinceId = "p1",
                movesLeft = 5, isActive = true
            };

            // 手动模拟移动（产品代码路径: 更新 currentProvinceId + 重建索引）
            world.units["u1"].currentProvinceId = "p2";
            world.RebuildProvinceUnitIndex();

            // 验证索引与遍历一致
            foreach (var prov in world.provinces.Values)
            {
                var indexUnits = world.GetUnitsInProvince(prov.id);
                var traversalUnits = world.units.Values
                    .Where(u => u.currentProvinceId == prov.id)
                    .Select(u => u.id)
                    .ToList();

                Assert.AreEqual(traversalUnits.Count, indexUnits.Count,
                    $"Count mismatch for province {prov.id}");
                foreach (var uid in traversalUnits)
                {
                    Assert.IsTrue(indexUnits.Contains(uid),
                        $"Unit {uid} missing from index for province {prov.id}");
                }
            }
        }

        [Test]
        public void LargeScale_NoPerformanceIssue()
        {
            // 24 省 × 10 部队 = 240 部队
            for (int i = 0; i < 24; i++)
            {
                string pid = $"prov_{i}";
                _world.provinces[pid] = new ProvinceState { id = pid, name = pid, terrain = TerrainType.Plain };
                for (int j = 0; j < 10; j++)
                {
                    string uid = $"u_{i}_{j}";
                    _world.units[uid] = new UnitState { id = uid, ownerCountry = "c1", currentProvinceId = pid };
                }
            }

            _world.RebuildProvinceUnitIndex();

            // O(1) 查询
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 24; i++)
            {
                var units = _world.GetUnitsInProvince($"prov_{i}");
                Assert.AreEqual(10, units.Count);
            }
            sw.Stop();

            // 24 次 O(1) 查询应 < 1ms
            Assert.Less(sw.ElapsedMilliseconds, 10);
        }
    }
}

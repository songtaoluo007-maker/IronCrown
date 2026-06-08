// ============================================================================
// TileModelTests.cs — P2.2 格模型测试
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IronCrown.Domain;

namespace IronCrown.Simulation.Tests
{
    public class TileModelTests
    {
        private WorldState _world;

        [SetUp]
        public void SetUp()
        {
            _world = new WorldState();
            // 模拟 2 个省: A(0,0) 和 B(1,0)
            var provA = new ProvinceState { id = "prov_a", name = "A", gridX = 0, gridY = 0, terrain = TerrainType.Plain };
            var provB = new ProvinceState { id = "prov_b", name = "B", gridX = 1, gridY = 0, terrain = TerrainType.Forest };
            _world.provinces["prov_a"] = provA;
            _world.provinces["prov_b"] = provB;

            // 每省 4 格 (2×2)
            foreach (var prov in _world.provinces.Values)
            {
                for (int n = 0; n < 4; n++)
                {
                    string tileId = $"{prov.id}_t{n}";
                    int tx = prov.gridX * 2 + (n % 2);
                    int ty = prov.gridY * 2 + (n / 2);
                    _world.tiles[tileId] = new TileState
                    {
                        id = tileId,
                        gridX = tx,
                        gridY = ty,
                        terrain = prov.terrain,
                        provinceId = prov.id
                    };
                    prov.tileIds.Add(tileId);
                }
            }
        }

        [Test]
        public void EachProvince_Has4Tiles()
        {
            foreach (var prov in _world.provinces.Values)
            {
                Assert.AreEqual(4, prov.tileIds.Count, $"Province {prov.id} should have 4 tileIds");
                Assert.AreEqual(4, _world.tiles.Values.Count(t => t.provinceId == prov.id),
                    $"Province {prov.id} should have 4 tiles in world.tiles");
            }
        }

        [Test]
        public void TileIds_FollowConvention()
        {
            foreach (var prov in _world.provinces.Values)
            {
                for (int n = 0; n < 4; n++)
                {
                    string expected = $"{prov.id}_t{n}";
                    Assert.Contains(expected, prov.tileIds, $"Province {prov.id} should contain {expected}");
                    Assert.IsTrue(_world.tiles.ContainsKey(expected), $"World.tiles should contain {expected}");
                }
            }
        }

        [Test]
        public void TileIds_AreDeterministic()
        {
            // 同样的省配置应产生同样的 tileIds
            var world2 = new WorldState();
            var prov = new ProvinceState { id = "prov_a", name = "A", gridX = 0, gridY = 0, terrain = TerrainType.Plain };
            world2.provinces["prov_a"] = prov;
            for (int n = 0; n < 4; n++)
            {
                string tileId = $"prov_a_t{n}";
                world2.tiles[tileId] = new TileState
                {
                    id = tileId,
                    gridX = n % 2,
                    gridY = n / 2,
                    terrain = TerrainType.Plain,
                    provinceId = "prov_a"
                };
                prov.tileIds.Add(tileId);
            }

            Assert.AreEqual(_world.provinces["prov_a"].tileIds, prov.tileIds);
        }

        [Test]
        public void TileCoordinates_AreCorrect()
        {
            // prov_a: gridX=0, gridY=0 → tiles at (0,0),(1,0),(0,1),(1,1)
            var tilesA = _world.tiles.Values.Where(t => t.provinceId == "prov_a").OrderBy(t => t.id).ToList();
            Assert.AreEqual(0, tilesA[0].gridX); Assert.AreEqual(0, tilesA[0].gridY); // t0
            Assert.AreEqual(1, tilesA[1].gridX); Assert.AreEqual(0, tilesA[1].gridY); // t1
            Assert.AreEqual(0, tilesA[2].gridX); Assert.AreEqual(1, tilesA[2].gridY); // t2
            Assert.AreEqual(1, tilesA[3].gridX); Assert.AreEqual(1, tilesA[3].gridY); // t3
        }

        [Test]
        public void TileTerrain_InheritsProvinceTerrain()
        {
            foreach (var tile in _world.tiles.Values)
            {
                var prov = _world.provinces[tile.provinceId];
                Assert.AreEqual(prov.terrain, tile.terrain,
                    $"Tile {tile.id} terrain should match province {prov.id}");
            }
        }

        [Test]
        public void WorldTiles_Count_Equals_ProvincesTimes4()
        {
            int expected = _world.provinces.Count * 4;
            Assert.AreEqual(expected, _world.tiles.Count);
        }
    }
}

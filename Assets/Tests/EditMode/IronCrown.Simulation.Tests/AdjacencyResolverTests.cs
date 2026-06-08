// ============================================================================
// AdjacencyResolverTests.cs — P2.2 邻接自动推导测试
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;

namespace IronCrown.Simulation.Tests
{
    public class AdjacencyResolverTests
    {
        private WorldState _world;
        private AdjacencyResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _world = new WorldState();
            _resolver = new AdjacencyResolver();
        }

        /// <summary>构建 6 省测试世界（模拟现有手写配置）</summary>
        private void Build6ProvinceWorld()
        {
            // 省配置（与 provinces.json 手写 neighbors 一致的布局）
            // 布局 (2×3 网格):
            //   (0,0)=north_capital  (1,0)=north_west
            //   (0,1)=north_east     (1,1)=south_capital
            //   (0,2)=south_west     (1,2)=south_east
            var configs = new[]
            {
                ("north_capital", 0, 0, TerrainType.Urban),
                ("north_west",    1, 0, TerrainType.Forest),
                ("north_east",    0, 1, TerrainType.Plain),
                ("south_capital", 1, 1, TerrainType.Urban),
                ("south_west",    0, 2, TerrainType.Mountain),
                ("south_east",    1, 2, TerrainType.Hills),
            };

            foreach (var (id, gx, gy, terrain) in configs)
            {
                var prov = new ProvinceState
                {
                    id = id, name = id, gridX = gx, gridY = gy,
                    terrain = terrain, ownerCountry = "empire",
                    neighbors = System.Array.Empty<string>() // 空，由推导填充
                };
                _world.provinces[id] = prov;

                // 每省 4 格
                for (int n = 0; n < 4; n++)
                {
                    string tileId = $"{id}_t{n}";
                    _world.tiles[tileId] = new TileState
                    {
                        id = tileId,
                        gridX = gx * 2 + (n % 2),
                        gridY = gy * 2 + (n / 2),
                        terrain = terrain,
                        provinceId = id
                    };
                    prov.tileIds.Add(tileId);
                }
            }
        }

        [Test]
        public void Compute_FillsNeighbors_ForAllProvinces()
        {
            Build6ProvinceWorld();
            _resolver.ComputeAndApply(_world);

            foreach (var prov in _world.provinces.Values)
            {
                Assert.IsNotNull(prov.neighbors, $"Province {prov.id} neighbors should not be null");
                Assert.IsTrue(prov.neighbors.Length > 0, $"Province {prov.id} should have at least 1 neighbor");
            }
        }

        [Test]
        public void Adjacency_IsSymmetric()
        {
            Build6ProvinceWorld();
            _resolver.ComputeAndApply(_world);

            foreach (var prov in _world.provinces.Values)
            {
                foreach (var neighborId in prov.neighbors)
                {
                    var neighbor = _world.provinces[neighborId];
                    Assert.Contains(prov.id, neighbor.neighbors,
                        $"If {prov.id} lists {neighborId} as neighbor, {neighborId} should list {prov.id}");
                }
            }
        }

        [Test]
        public void Adjacency_NoSelfReference()
        {
            Build6ProvinceWorld();
            _resolver.ComputeAndApply(_world);

            foreach (var prov in _world.provinces.Values)
            {
                Assert.IsFalse(prov.neighbors.Contains(prov.id),
                    $"Province {prov.id} should not list itself as neighbor");
            }
        }

        [Test]
        public void Adjacency_MatchesExpectedLayout()
        {
            Build6ProvinceWorld();
            _resolver.ComputeAndApply(_world);

            // 预期邻接 (2×3 网格):
            // north_capital(0,0) ↔ north_west(1,0), north_east(0,1)
            // north_west(1,0)    ↔ north_capital(0,0), south_capital(1,1)
            // north_east(0,1)    ↔ north_capital(0,0), south_capital(1,1), south_west(0,2)
            // south_capital(1,1) ↔ north_west(1,0), north_east(0,1), south_east(1,2)
            // south_west(0,2)    ↔ north_east(0,1), south_east(1,2)
            // south_east(1,2)    ↔ south_capital(1,1), south_west(0,2)

            var nc = _world.provinces["north_capital"].neighbors.OrderBy(x => x).ToArray();
            Assert.AreEqual(2, nc.Length);
            Assert.Contains("north_east", nc);
            Assert.Contains("north_west", nc);

            var nw = _world.provinces["north_west"].neighbors.OrderBy(x => x).ToArray();
            Assert.AreEqual(2, nw.Length);
            Assert.Contains("north_capital", nw);
            Assert.Contains("south_capital", nw);

            var ne = _world.provinces["north_east"].neighbors.OrderBy(x => x).ToArray();
            Assert.AreEqual(3, ne.Length);
            Assert.Contains("north_capital", ne);
            Assert.Contains("south_capital", ne);
            Assert.Contains("south_west", ne);

            var sc = _world.provinces["south_capital"].neighbors.OrderBy(x => x).ToArray();
            Assert.AreEqual(3, sc.Length);
            Assert.Contains("north_east", sc);
            Assert.Contains("north_west", sc);
            Assert.Contains("south_east", sc);

            var sw = _world.provinces["south_west"].neighbors.OrderBy(x => x).ToArray();
            Assert.AreEqual(2, sw.Length);
            Assert.Contains("north_east", sw);
            Assert.Contains("south_east", sw);

            var se = _world.provinces["south_east"].neighbors.OrderBy(x => x).ToArray();
            Assert.AreEqual(2, se.Length);
            Assert.Contains("south_capital", se);
            Assert.Contains("south_west", se);
        }

        [Test]
        public void Adjacency_ProvinceWithNoTiles_HasNoNeighbors()
        {
            // 孤岛省（无 tiles）→ 无邻接
            var island = new ProvinceState { id = "island", name = "Island", gridX = 100, gridY = 100, terrain = TerrainType.Coastline };
            _world.provinces["island"] = island;

            Build6ProvinceWorld();
            _resolver.ComputeAndApply(_world);

            Assert.AreEqual(0, _world.provinces["island"].neighbors.Length);
        }

        [Test]
        public void GetNeighbors_ReturnsSameAsProvinceNeighbors()
        {
            Build6ProvinceWorld();
            _resolver.ComputeAndApply(_world);

            foreach (var prov in _world.provinces.Values)
            {
                var fromResolver = _resolver.GetNeighbors(prov.id);
                Assert.AreEqual(prov.neighbors, fromResolver,
                    $"GetNeighbors({prov.id}) should match province.neighbors");
            }
        }
    }
}

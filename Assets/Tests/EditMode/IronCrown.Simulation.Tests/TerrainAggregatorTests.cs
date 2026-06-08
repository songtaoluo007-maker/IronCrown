// ============================================================================
// TerrainAggregatorTests.cs — P2.4 地形聚合测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;

namespace IronCrown.Simulation.Tests
{
    public class TerrainAggregatorTests
    {
        private WorldState _world;

        [SetUp]
        public void SetUp()
        {
            _world = new WorldState();
        }

        private ProvinceState MakeProvince(string id, params TerrainType[] tileTerrains)
        {
            var prov = new ProvinceState
            {
                id = id, name = id, gridX = 0, gridY = 0,
                terrain = tileTerrains[0] // 省地形 = 第一格
            };
            _world.provinces[id] = prov;

            for (int n = 0; n < tileTerrains.Length; n++)
            {
                string tileId = $"{id}_t{n}";
                _world.tiles[tileId] = new TileState
                {
                    id = tileId,
                    gridX = n % 2,
                    gridY = n / 2,
                    terrain = tileTerrains[n],
                    provinceId = id
                };
                prov.tileIds.Add(tileId);
            }
            return prov;
        }

        [Test]
        public void AllSameTerrain_ReturnsThatTerrain()
        {
            var prov = MakeProvince("p1", TerrainType.Plain, TerrainType.Plain, TerrainType.Plain, TerrainType.Plain);
            Assert.AreEqual(TerrainType.Plain, TerrainAggregator.GetProvinceCombatTerrain(prov, _world));
        }

        [Test]
        public void MajorityTerrain_Wins()
        {
            // 3 Forest + 1 Plain → Forest
            var prov = MakeProvince("p1", TerrainType.Forest, TerrainType.Forest, TerrainType.Forest, TerrainType.Plain);
            Assert.AreEqual(TerrainType.Forest, TerrainAggregator.GetProvinceCombatTerrain(prov, _world));
        }

        [Test]
        public void TieBreak_DefenseHigherWins()
        {
            // 2 Plain (100) + 2 Mountain (125) → Mountain (higher defense)
            var prov = MakeProvince("p1", TerrainType.Plain, TerrainType.Plain, TerrainType.Mountain, TerrainType.Mountain);
            Assert.AreEqual(TerrainType.Mountain, TerrainAggregator.GetProvinceCombatTerrain(prov, _world));
        }

        [Test]
        public void TieBreak_MultipleEqual_DefenseHigherWins()
        {
            // 2 Forest (110) + 2 Hills (115) → Hills
            var prov = MakeProvince("p1", TerrainType.Forest, TerrainType.Forest, TerrainType.Hills, TerrainType.Hills);
            Assert.AreEqual(TerrainType.Hills, TerrainAggregator.GetProvinceCombatTerrain(prov, _world));
        }

        [Test]
        public void NoTiles_FallsBackToProvinceTerrain()
        {
            var prov = new ProvinceState
            {
                id = "empty", name = "empty", terrain = TerrainType.Urban,
                tileIds = new System.Collections.Generic.List<string>()
            };
            _world.provinces["empty"] = prov;
            Assert.AreEqual(TerrainType.Urban, TerrainAggregator.GetProvinceCombatTerrain(prov, _world));
        }

        [Test]
        public void SingleTile_ReturnsThatTileTerrain()
        {
            var prov = new ProvinceState
            {
                id = "single", name = "single", terrain = TerrainType.Plain
            };
            _world.provinces["single"] = prov;
            string tileId = "single_t0";
            _world.tiles[tileId] = new TileState
            {
                id = tileId, gridX = 0, gridY = 0,
                terrain = TerrainType.Swamp, provinceId = "single"
            };
            prov.tileIds.Add(tileId);

            Assert.AreEqual(TerrainType.Swamp, TerrainAggregator.GetProvinceCombatTerrain(prov, _world));
        }

        [Test]
        public void MixedTerrain_3Plain1Mountain_ReturnsPlain()
        {
            var prov = MakeProvince("mix", TerrainType.Plain, TerrainType.Plain, TerrainType.Plain, TerrainType.Mountain);
            Assert.AreEqual(TerrainType.Plain, TerrainAggregator.GetProvinceCombatTerrain(prov, _world));
        }

        [Test]
        public void MixedTerrain_2Forest2Urban_ReturnsUrban()
        {
            // Forest=110, Urban=130 → Urban wins tie
            var prov = MakeProvince("mix", TerrainType.Forest, TerrainType.Forest, TerrainType.Urban, TerrainType.Urban);
            Assert.AreEqual(TerrainType.Urban, TerrainAggregator.GetProvinceCombatTerrain(prov, _world));
        }
    }
}

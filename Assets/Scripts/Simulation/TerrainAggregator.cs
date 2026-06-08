// ============================================================================
// Simulation/TerrainAggregator.cs — 省级地形聚合（P2.2/P2.4）
// 省战斗地形 = 省内格地形的主导类型
// 聚合方式: 出现最多的格地形;平票取防御倍率高者
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public static class TerrainAggregator
    {
        /// <summary>
        /// 获取省份的战斗地形（由省内格地形聚合得出）。
        /// 聚合规则: 主导地形（出现最多）;平票取防御倍率高者。
        /// </summary>
        public static TerrainType GetProvinceCombatTerrain(ProvinceState province, WorldState world)
        {
            if (province.tileIds == null || province.tileIds.Count == 0)
                return province.terrain; // 无格时回退省地形

            // 统计各地形出现次数
            var counts = new Dictionary<TerrainType, int>();
            foreach (var tileId in province.tileIds)
            {
                if (world.tiles.TryGetValue(tileId, out var tile))
                {
                    if (!counts.ContainsKey(tile.terrain))
                        counts[tile.terrain] = 0;
                    counts[tile.terrain]++;
                }
            }

            if (counts.Count == 0)
                return province.terrain;

            // 找最大出现次数
            int maxCount = counts.Values.Max();

            // 平票: 取防御倍率高者
            var candidates = counts.Where(kv => kv.Value == maxCount).Select(kv => kv.Key).ToList();
            if (candidates.Count == 1)
                return candidates[0];

            return candidates.OrderByDescending(t => GetBaseDefenseWeight(t)).First();
        }

        /// <summary>基础防御权重（用于平票裁决，不含 config 倍率）</summary>
        private static int GetBaseDefenseWeight(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Plain => 100,
                TerrainType.Forest => 110,
                TerrainType.Hills => 115,
                TerrainType.Mountain => 125,
                TerrainType.Urban => 130,
                TerrainType.Swamp => 120,
                TerrainType.River => 120,
                TerrainType.Coastline => 105,
                TerrainType.Desert => 100,
                TerrainType.Jungle => 115,
                _ => 100
            };
        }
    }
}

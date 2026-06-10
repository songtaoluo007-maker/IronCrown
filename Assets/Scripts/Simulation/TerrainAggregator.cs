// ============================================================================
// Simulation/TerrainAggregator.cs — 省级地形聚合（P2.2/P2.4）
// 省战斗地形 = 省内格地形的主导类型
// 聚合方式: 出现最多的格地形;平票取防御倍率高者（从 config 读取,非硬编码）
// F2 修复: 删除硬编码 GetBaseDefenseWeight,改用 EconomyConfig.terrainDefenseMult
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;
using IronCrown.Domain.Config;

namespace IronCrown.Simulation
{
    public static class TerrainAggregator
    {
        /// <summary>
        /// 获取省份的战斗地形（由省内格地形聚合得出）。
        /// 聚合规则: 主导地形（出现最多）;平票取 config 防御倍率高者,再按枚举值升序兜底。
        /// </summary>
        public static TerrainType GetProvinceCombatTerrain(ProvinceState province, WorldState world, EconomyConfig eco)
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

            // 平票: 按 config 防御倍率降序 → 枚举值升序兜底（确定性）
            var candidates = counts.Where(kv => kv.Value == maxCount).Select(kv => kv.Key).ToList();
            if (candidates.Count == 1)
                return candidates[0];

            return candidates
                .OrderByDescending(t => eco != null && eco.terrainDefenseMult.TryGetValue(t.ToString(), out var m) ? m : 100)
                .ThenBy(t => (int)t)
                .First();
        }
    }
}

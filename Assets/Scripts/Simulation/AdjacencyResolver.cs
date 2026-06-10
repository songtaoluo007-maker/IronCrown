// ============================================================================
// Simulation/AdjacencyResolver.cs — 邻接自动推导（P2.2）
// 格邻接: 网格坐标 4 邻
// 省邻接: ∃ A 省格与 B 省格相邻 ⇒ A、B 省邻接
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class AdjacencyResolver
    {
        // 缓存: provinceId → 邻省 id 集合
        private Dictionary<string, HashSet<string>> _provinceAdjacencyCache;

        /// <summary>从 tiles 推导所有省的邻接关系，并写入 ProvinceState.neighbors</summary>
        public void ComputeAndApply(WorldState world)
        {
            _provinceAdjacencyCache = new Dictionary<string, HashSet<string>>();

            // 按 provinceId 分组 tiles
            var tilesByProvince = world.tiles.Values
                .GroupBy(t => t.provinceId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 构建 grid 坐标 → tile 映射
            var gridMap = new Dictionary<(int x, int y), TileState>();
            foreach (var tile in world.tiles.Values)
                gridMap[(tile.gridX, tile.gridY)] = tile;

            // 4 邻方向
            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { -1, 1, 0, 0 };

            // 格邻接 → 省邻接
            foreach (var tile in world.tiles.Values)
            {
                for (int d = 0; d < 4; d++)
                {
                    int nx = tile.gridX + dx[d];
                    int ny = tile.gridY + dy[d];
                    if (gridMap.TryGetValue((nx, ny), out var neighbor))
                    {
                        // 不同省 → 互为邻省
                        if (neighbor.provinceId != tile.provinceId)
                        {
                            AddAdjacency(tile.provinceId, neighbor.provinceId);
                        }
                    }
                }
            }

            // 写入 ProvinceState.neighbors（替代手写配置）
            foreach (var province in world.provinces.Values)
            {
                if (_provinceAdjacencyCache.TryGetValue(province.id, out var neighbors))
                {
                    province.neighbors = neighbors.OrderBy(n => n, System.StringComparer.Ordinal).ToArray();
                }
                else
                {
                    province.neighbors = System.Array.Empty<string>();
                }
            }
        }

        /// <summary>获取指定省的邻接列表（需先调用 ComputeAndApply）</summary>
        public string[] GetNeighbors(string provinceId)
        {
            if (_provinceAdjacencyCache != null && _provinceAdjacencyCache.TryGetValue(provinceId, out var neighbors))
                return neighbors.OrderBy(n => n, System.StringComparer.Ordinal).ToArray();
            return System.Array.Empty<string>();
        }

        private void AddAdjacency(string a, string b)
        {
            if (!_provinceAdjacencyCache.ContainsKey(a))
                _provinceAdjacencyCache[a] = new HashSet<string>();
            if (!_provinceAdjacencyCache.ContainsKey(b))
                _provinceAdjacencyCache[b] = new HashSet<string>();
            _provinceAdjacencyCache[a].Add(b);
            _provinceAdjacencyCache[b].Add(a);
        }
    }
}

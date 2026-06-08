// ============================================================================
// Application/Persistence/Migration_1to2.cs — v1 → v2 迁移器
// P2.2: 旧档(省无 tiles) → 按省 gridX/gridY 程序生成默认格 + 回填 tileIds
// ============================================================================

using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace IronCrown.Application
{
    /// <summary>
    /// v1 → v2 迁移器。
    /// 对无 tiles 的 v1 存档，按每省 gridX/gridY 生成 4 格(2×2 子网格)，
    /// 格坐标 = 省 gridX*2+{0,1}, gridY*2+{0,1}。
    /// tile id 规约: {provinceId}_t{n} (n=0..3)。
    /// 格地形继承省主地形。
    /// </summary>
    public sealed class Migration_1to2 : ISaveMigration
    {
        public int FromVersion => 1;

        public JObject Migrate(JObject raw)
        {
            var provinces = raw["provinces"] as JArray;
            if (provinces == null)
            {
                raw["schemaVersion"] = 2;
                return raw;
            }

            var tiles = new JArray();

            foreach (var province in provinces)
            {
                string provinceId = province["id"]?.ToString() ?? "";
                int gridX = province["gridX"]?.Value<int>() ?? 0;
                int gridY = province["gridY"]?.Value<int>() ?? 0;
                string terrain = province["terrain"]?.ToString() ?? "Plain";

                var tileIds = new JArray();

                for (int n = 0; n < 4; n++)
                {
                    string tileId = $"{provinceId}_t{n}";
                    int tx = gridX * 2 + (n % 2);
                    int ty = gridY * 2 + (n / 2);

                    tiles.Add(new JObject
                    {
                        ["id"] = tileId,
                        ["gridX"] = tx,
                        ["gridY"] = ty,
                        ["terrain"] = terrain,
                        ["provinceId"] = provinceId
                    });

                    tileIds.Add(tileId);
                }

                // 回填 tileIds 到省
                province["tileIds"] = tileIds;
            }

            raw["tiles"] = tiles;
            raw["schemaVersion"] = 2;
            return raw;
        }
    }
}

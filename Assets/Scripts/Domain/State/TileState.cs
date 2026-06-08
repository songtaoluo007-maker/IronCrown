// ============================================================================
// Domain/State/TileState.cs — 格（Tile）数据模型
// P2.2: 省由多格聚合，格是最小地图单元
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>格运行时状态</summary>
    public class TileState
    {
        /// <summary>格 ID（规约: {provinceId}_t{n}, n=0..3）</summary>
        public string id;

        /// <summary>网格 X 坐标（省 gridX*2 + {0,1}）</summary>
        public int gridX;

        /// <summary>网格 Y 坐标（省 gridY*2 + {0,1}）</summary>
        public int gridY;

        /// <summary>地形类型（P2.2 阶段继承省主地形，P2.4 做差异化）</summary>
        public TerrainType terrain;

        /// <summary>归属省份 ID</summary>
        public string provinceId;
    }
}

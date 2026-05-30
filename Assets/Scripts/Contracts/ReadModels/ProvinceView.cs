// ============================================================================
// Contracts/ReadModels/ProvinceView.cs — 省份只读模型
// UI 层通过此 DTO 读取省份状态，不直接引用 Domain
// ============================================================================

namespace IronCrown.Contracts
{
    public sealed class ProvinceView
    {
        public string id;
        public string name;
        public string ownerCountry;
        public string ownerColor;     // 该国 mapColor（hex）
        public string terrain;
        public int gridX;
        public int gridY;
        public int infrastructure;
        public int population;
        public int victoryPoint;
        public bool isCapital;
        public string[] resourceOutput;
        public string[] neighbors;
        public int garrisonCount;
        public string[] garrisonUnitIds;
    }
}

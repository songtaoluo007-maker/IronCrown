// ============================================================================
// Contracts/ReadModels/UnitView.cs — 部队只读模型
// UI 层通过此 DTO 读取部队状态，不直接引用 Domain
// ============================================================================

namespace IronCrown.Contracts
{
    public sealed class UnitView
    {
        public string id;
        public string unitType;
        public string ownerCountry;
        public string currentProvinceId;
        public int manpower;
        public int maxManpower;
        public int organization;
        public int maxOrganization;
        public int movesLeft;
        public int speed;
    }
}

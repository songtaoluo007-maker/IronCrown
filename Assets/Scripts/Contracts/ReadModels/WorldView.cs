// ============================================================================
// Contracts/ReadModels/WorldView.cs — 世界只读模型
// UI 层通过此 DTO 读取世界状态，不直接引用 Domain
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Contracts
{
    public sealed class WorldView
    {
        public int turn;
        public string phase;          // GamePhase.ToString()
        public int worldTension;
        public string playerCountryId;
        public string selectedProvinceId;
        public List<CountryView> countries;
        public List<ProvinceView> provinces;
    }
}

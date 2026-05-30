// ============================================================================
// Contracts/Events/UnitMovedEvent.cs — 部队移动事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct UnitMovedEvent
    {
        public string unitId;
        public string fromProvinceId;
        public string toProvinceId;
        public int movesLeftAfter;
    }
}

// ============================================================================
// Events/UnitRetreatedEvent.cs — 部队溃退事件（C13）
// ============================================================================

namespace IronCrown.Contracts
{
    public struct UnitRetreatedEvent
    {
        public string unitId;
        public string fromProvinceId;
        public string retreatProvinceId;
        public int turnNumber;
    }
}

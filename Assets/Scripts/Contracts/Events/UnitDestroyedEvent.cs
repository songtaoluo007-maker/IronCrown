// ============================================================================
// Contracts/Events/UnitDestroyedEvent.cs — 部队消灭事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct UnitDestroyedEvent
    {
        public string unitId;
        public string ownerCountry;
        public string provinceId;
        public string cause;            // "battle" | "occupation"
    }
}

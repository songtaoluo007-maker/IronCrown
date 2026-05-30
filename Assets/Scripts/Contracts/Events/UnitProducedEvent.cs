// ============================================================================
// Contracts/Events/UnitProducedEvent.cs — 部队生产完成事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct UnitProducedEvent
    {
        public string unitId;
        public string ownerCountry;
        public string provinceId;
        public string unitType;
    }
}

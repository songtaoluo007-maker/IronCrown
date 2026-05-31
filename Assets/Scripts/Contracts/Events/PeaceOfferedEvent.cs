// ============================================================================
// Contracts/Events/PeaceOfferedEvent.cs — 停战提议事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct PeaceOfferedEvent
    {
        public string fromCountry;
        public string toCountry;
        public bool accepted;
        public string reason;
    }
}

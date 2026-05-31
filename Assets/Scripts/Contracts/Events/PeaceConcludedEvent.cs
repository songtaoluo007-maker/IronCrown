// ============================================================================
// Contracts/Events/PeaceConcludedEvent.cs — 停战达成事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct PeaceConcludedEvent
    {
        public string countryA;  // Ordinal 较小
        public string countryB;  // Ordinal 较大
        public int atTurn;
    }
}

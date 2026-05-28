// ============================================================================
// Contracts/Events/PolicyChangedEvent.cs — 政策变更事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct PolicyChangedEvent
    {
        public string CountryId;
        public string PolicyId;
        public bool Activated;
    }
}

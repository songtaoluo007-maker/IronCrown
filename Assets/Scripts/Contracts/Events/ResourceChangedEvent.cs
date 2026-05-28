// ============================================================================
// Contracts/Events/ResourceChangedEvent.cs — 资源变更事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct ResourceChangedEvent
    {
        public string CountryId;
        public string ResourceId;
        public int OldValue;
        public int NewValue;
    }
}

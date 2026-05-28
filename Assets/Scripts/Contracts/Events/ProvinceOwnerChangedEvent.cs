// ============================================================================
// Contracts/Events/ProvinceOwnerChangedEvent.cs — 省份所有权变更事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct ProvinceOwnerChangedEvent
    {
        public string ProvinceId;
        public string OldOwner;
        public string NewOwner;
    }
}

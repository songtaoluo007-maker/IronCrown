// ============================================================================
// Contracts/Events/ProvinceOccupiedEvent.cs — 省份占领事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct ProvinceOccupiedEvent
    {
        public string provinceId;
        public string newControllerCountry;
        public string previousControllerCountry;
        public string attackerUnitId;
    }
}

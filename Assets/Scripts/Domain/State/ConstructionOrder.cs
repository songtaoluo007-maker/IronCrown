// ============================================================================
// Domain/State/ConstructionOrder.cs — 建造订单
// ============================================================================

namespace IronCrown.Domain
{
    public sealed class ConstructionOrder
    {
        public string factoryKind;     // "civilian" | "military"
        public int turnsRemaining;
    }
}

// ============================================================================
// Contracts/Events/BattleInitiatedEvent.cs — 战斗发起事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct BattleInitiatedEvent
    {
        public string battleId;
        public string attackerUnitId;
        public string defenderUnitId;
        public string provinceId;
    }
}

// ============================================================================
// Contracts/Events/BattleConcludedEvent.cs — 战斗结束事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct BattleConcludedEvent
    {
        public string battleId;
        public string provinceId;
        public string winnerKind;      // "Attacker" | "Defender" | "Draw"
        public string attackerUnitId;
        public string defenderUnitId;
        public int turnsElapsed;
    }
}

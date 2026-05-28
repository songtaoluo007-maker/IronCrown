// ============================================================================
// Contracts/Events/BattleResolvedEvent.cs — 战斗结算事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct BattleResolvedEvent
    {
        public string AttackerId;
        public string DefenderId;
        public string ProvinceId;
        public bool AttackerWon;
    }
}

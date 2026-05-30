// ============================================================================
// Contracts/ReadModels/ActiveBattleView.cs — 活动战斗只读模型
// ============================================================================

namespace IronCrown.Contracts
{
    public sealed class ActiveBattleView
    {
        public string id;
        public string attackerUnitId;
        public string defenderUnitId;
        public string provinceId;
        public int turnsElapsed;
        public int attackerOrg;
        public int attackerMaxOrg;
        public int defenderOrg;
        public int defenderMaxOrg;
    }
}

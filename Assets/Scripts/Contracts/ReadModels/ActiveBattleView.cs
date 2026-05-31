// ============================================================================
// Contracts/ReadModels/ActiveBattleView.cs — 活动战斗只读模型（C9c 多兵种）
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Contracts
{
    public sealed class ActiveBattleView
    {
        public string id;
        public List<string> attackerUnitIds = new();
        public List<string> defenderUnitIds = new();
        public string provinceId;
        public string attackerOwnerCountry;
        public int turnsElapsed;
        public int attackerOrg;      // sum
        public int attackerMaxOrg;   // sum
        public int defenderOrg;      // sum
        public int defenderMaxOrg;   // sum
    }
}

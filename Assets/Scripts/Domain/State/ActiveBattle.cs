// ============================================================================
// Domain/State/ActiveBattle.cs — 活动战斗数据
// 玩家发起进攻后创建，双方"卡在战场"，每回合 Settlement 阶段 tick
// ============================================================================

namespace IronCrown.Domain
{
    public sealed class ActiveBattle
    {
        public string id;               // "{attackerUnitId}_vs_{defenderUnitId}"
        public string attackerUnitId;
        public string defenderUnitId;
        public string provinceId;
        public int turnsElapsed;        // 已 tick 次数
    }
}

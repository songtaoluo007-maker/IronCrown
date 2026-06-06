// ============================================================================
// Domain/State/ActiveBattle.cs — 活动战斗数据（C9c 多兵种联合战斗）
// 玩家发起进攻后创建，双方"卡在战场"，每回合 Settlement 阶段 tick
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    public sealed class ActiveBattle
    {
        public string id;                              // = provinceId（一省一战）
        public List<string> attackerUnitIds = new();   // 多攻方
        public List<string> defenderUnitIds = new();   // 多守方
        public string provinceId;
        public string attackerOwnerCountry;            // 攻方阵营（首位 attacker 的 owner）
        public string defenderOwnerCountry;            // 守方阵营（首位 defender 的 owner）
        public int turnsElapsed;                       // 已 tick 次数
    }
}

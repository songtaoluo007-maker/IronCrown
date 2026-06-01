// ============================================================================
// Contracts/ReadModels/CommanderView.cs — 将领只读 DTO（C15a）
// ============================================================================

namespace IronCrown.Contracts
{
    /// <summary>将领视图（只读 DTO）</summary>
    public sealed class CommanderView
    {
        public string id;
        public string name;
        public string ownerCountry;
        public int rank;
        public string rankName;          // "尉官"/"校官"/"将官"/"元帅"/"大元帅"
        public int victories;
        public int encirclements;
        public int baseAttack;
        public int baseDefense;
        public int rankAttackBonusPct;   // 军衔攻击加成 %
        public int rankDefenseBonusPct;  // 军衔防御加成 %
        public int maxDivisions;         // 麾下师上限
        public int commandedDivisions;   // 当前指挥师数
        public bool isActive;
        public bool canPromote;
        public string buffDescription;   // 完整 buff 描述
    }
}

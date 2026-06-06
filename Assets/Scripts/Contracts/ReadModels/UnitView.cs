// ============================================================================
// Contracts/ReadModels/UnitView.cs — 部队只读模型
// UI 层通过此 DTO 读取部队状态，不直接引用 Domain
// ============================================================================

namespace IronCrown.Contracts
{
    public sealed class UnitView
    {
        public string id;
        public string unitType;
        public string divisionTemplateName;  // C11: 师模板名
        public string brigadeSummary;         // C11: 如 "9 步兵 + 3 炮兵"
        public string ownerCountry;
        public string currentProvinceId;
        public int manpower;
        public int maxManpower;
        public int equipment;             // C13
        public int maxEquipment;          // C13
        public int organization;
        public int maxOrganization;
        public int movesLeft;
        public int speed;
        public bool isInBattle;
        public int tacticalExp;           // C13: 战役经验 0-100
        public int tacticalLevel;         // C13: 战役等级 0-4
        public int recoveryTurnsLeft;     // C13: 溃退恢复剩余回合
        public bool isRecovering;         // C13: 是否溃退恢复中

        // === C14: 补给系统 ===
        public bool isCutoff;             // 是否被切断补给
        public int cutoffTurns;           // 被切断累计回合数
        public bool isDisorganized;       // 是否混乱状态
        public int morale;                // 士气 0-100

        // === C15a: 将领 ===
        public string commanderId;
        public string commanderName;
        public string commanderRank;     // 军衔名称
    }
}

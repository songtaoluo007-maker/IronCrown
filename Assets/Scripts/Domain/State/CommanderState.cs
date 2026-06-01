// ============================================================================
// Domain/State/CommanderState.cs — 将领运行时状态（C15a）
// EU4 风格将军卡 + 5 阶军衔晋升
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>将领运行时状态</summary>
    public sealed class CommanderState
    {
        // === 基础 ===
        public string id;
        public string name;
        public string ownerCountry;

        // === 军衔（0=尉官 → 1=校官 → 2=将官 → 3=元帅 → 4=大元帅） ===
        public int rank;
        public int victories;           // 累计胜场（含包围加权）
        public int encirclements;       // 累计包围歼敌次数

        // === 属性（随军衔提升） ===
        public int baseAttack;          // 基础攻击加成
        public int baseDefense;         // 基础防御加成

        // === 指挥容量 ===
        public int maxDivisions;        // 麾下师上限（rank 5 → +5）

        // === 状态 ===
        public bool isActive;           // 是否在役

        // === 军衔晋升阈值 ===
        public static readonly int[] RankThresholds = { 0, 5, 15, 35, 75 };

        // === 军衔名称 ===
        public static readonly string[] RankNames = { "尉官", "校官", "将官", "元帅", "大元帅" };

        /// <summary>当前军衔名称</summary>
        public string RankName => RankNames[System.Math.Clamp(rank, 0, 4)];

        /// <summary>军衔攻击加成百分比（每级 +5%）</summary>
        public int RankAttackBonusPct => rank * 5;

        /// <summary>军衔防御加成百分比（每级 +5%）</summary>
        public int RankDefenseBonusPct => rank * 5;

        /// <summary>是否可晋升</summary>
        public bool CanPromote => rank < 4 && victories >= RankThresholds[rank + 1];

        /// <summary>尝试晋升（返回是否成功）</summary>
        public bool TryPromote()
        {
            if (!CanPromote) return false;
            rank++;
            maxDivisions = 5 + rank; // 基础 5 + 每级 +1
            return true;
        }

        /// <summary>记录胜场（含包围加权）</summary>
        public void RecordVictory(bool isEncirclement = false)
        {
            victories++;
            if (isEncirclement)
            {
                encirclements++;
                victories += 2; // 包围 = 额外 +2 胜场（总计 +3）
            }
        }

        /// <summary>获取完整 buff 描述</summary>
        public string GetBuffDescription()
        {
            return $"{RankName} 攻+{RankAttackBonusPct}% 防+{RankDefenseBonusPct}% 指挥{maxDivisions}师";
        }
    }
}

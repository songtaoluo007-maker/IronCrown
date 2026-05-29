// ============================================================================
// Contracts/Commands/GameCommand.cs — 玩家命令 DTO（只读）
// ============================================================================

namespace IronCrown.Contracts
{
    public sealed class GameCommand
    {
        public CommandType commandType;
        public string countryId;
        public int level;  // 档位 (0-2)，仅 SetTaxLevel/SetCivilLevel 使用
    }
}

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
        public string unitType;  // 兵种类型，仅 BuildUnit 使用
        public string unitId;    // 部队ID，仅 MoveUnit 使用
        public string targetProvinceId;  // 目标省份ID，仅 MoveUnit 使用
        public string targetCountryId;   // 目标国家ID，仅 OfferPeace 使用
    }
}

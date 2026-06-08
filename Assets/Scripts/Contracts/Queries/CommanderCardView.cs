// ============================================================================
// Contracts/Queries/CommanderCardView.cs — P2.1 将领卡片展示 DTO
// Presentation 层专用，不依赖 Domain
// ============================================================================

namespace IronCrown.Contracts
{
    /// <summary>将领卡片展示数据（Presentation 层只用这个，不用 Domain 类型）</summary>
    public struct CommanderCardView
    {
        public string cardId;
        public string name;
        public string rarity;
        public string skillDescription;
        public int unlockCost;
        public int starUpCost;
        public bool owned;
        public int starLevel;
        public bool isMaxStar;
        public bool canAfford;
    }
}

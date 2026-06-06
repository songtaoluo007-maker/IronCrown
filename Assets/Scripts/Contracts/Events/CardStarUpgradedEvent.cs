// ============================================================================
// Contracts/Events/CardStarUpgradedEvent.cs — C16 卡牌升星事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct CardStarUpgradedEvent
    {
        public string commanderId;
        public string cardId;
        public int newStar;          // 1-5
    }
}

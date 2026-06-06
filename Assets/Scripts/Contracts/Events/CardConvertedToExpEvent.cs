// ============================================================================
// Contracts/Events/CardConvertedToExpEvent.cs — C16 满星转经验事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct CardConvertedToExpEvent
    {
        public string commanderId;
        public string cardId;
        public int expGained;        // 获得的胜场经验
    }
}

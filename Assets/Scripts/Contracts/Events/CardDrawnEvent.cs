// ============================================================================
// Contracts/Events/CardDrawnEvent.cs — C16 抽到新卡事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct CardDrawnEvent
    {
        public string rarity;        // N/R/SR/SSR
        public string cardId;        // 将军卡模板 ID
        public string commanderId;   // 新创建的将领 ID
    }
}

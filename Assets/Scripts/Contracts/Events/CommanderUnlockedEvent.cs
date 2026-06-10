// ============================================================================
// Contracts/Events/CommanderUnlockedEvent.cs — P2.1 新卡解锁事件
// 替代 CardDrawnEvent（"抽到"语义已退役）
// ============================================================================

namespace IronCrown.Contracts
{
    public struct CommanderUnlockedEvent
    {
        public string commanderId;   // 新创建的将领 ID
        public string cardId;        // 将军卡模板 ID
        public string rarity;        // N/R/SR/SSR
    }
}

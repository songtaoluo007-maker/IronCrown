// ============================================================================
// Contracts/Events/AiPeaceOfferedEvent.cs — AI 主动求和事件 (C7)
// ============================================================================

namespace IronCrown.Contracts
{
    /// <summary>AI 向玩家主动提议停战</summary>
    public struct AiPeaceOfferedEvent
    {
        public string fromCountry;      // 发起方（AI）
        public string toCountry;        // 目标（玩家）
        public int expiryTurnNumber;    // 过期回合号
    }
}

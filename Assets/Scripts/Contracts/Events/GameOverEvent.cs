// ============================================================================
// Contracts/Events/GameOverEvent.cs — 游戏结束事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct GameOverEvent
    {
        public string result;          // "Victory" | "Defeat"
        public string winnerCountryId; // 玩家胜=playerCountryId；失败=null
    }
}

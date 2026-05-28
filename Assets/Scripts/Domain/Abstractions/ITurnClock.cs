// ============================================================================
// Domain/Abstractions/ITurnClock.cs — 回合时钟接口
// ============================================================================

namespace IronCrown.Domain
{
    public interface ITurnClock
    {
        int CurrentTurn { get; }
        int MaxTurns { get; set; }
        GamePhase CurrentPhase { get; }
        bool IsPaused { get; set; }
        void AdvancePhase();
        void Reset(int maxTurns = 60);
    }
}

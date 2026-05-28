// ============================================================================
// Domain/Time/GameClock.cs — 游戏时钟（实现 ITurnClock）
// 从 Core/GameClock.cs 迁移，删除 EventBus.Instance 引用
// ============================================================================

using IronCrown.Contracts;

namespace IronCrown.Domain
{
    /// <summary>
    /// 游戏时钟。1 回合 = 1 个月。
    /// 管理当前回合数、游戏阶段、暂停状态。
    /// </summary>
    public sealed class GameClock : ITurnClock
    {
        private readonly IEventPublisher _events;

        public int CurrentTurn { get; private set; } = 1;
        public int MaxTurns { get; set; } = 60;
        public GamePhase CurrentPhase { get; private set; } = GamePhase.TurnStart;
        public bool IsPaused { get; set; } = false;

        public GameClock(IEventPublisher events)
        {
            _events = events;
        }

        public void AdvancePhase()
        {
            if (IsPaused) return;

            CurrentPhase = CurrentPhase switch
            {
                GamePhase.TurnStart       => GamePhase.InternalAffairs,
                GamePhase.InternalAffairs => GamePhase.Military,
                GamePhase.Military        => GamePhase.Diplomacy,
                GamePhase.Diplomacy       => GamePhase.Settlement,
                GamePhase.Settlement      => NextTurn(),
                _ => GamePhase.TurnStart
            };
        }

        private GamePhase NextTurn()
        {
            CurrentTurn++;
            if (CurrentTurn > MaxTurns)
            {
                IsPaused = true;
                return GamePhase.GameOver;
            }
            _events.Publish(new TurnStartEvent { TurnNumber = CurrentTurn });
            return GamePhase.TurnStart;
        }

        public void Reset(int maxTurns = 60)
        {
            CurrentTurn = 1;
            MaxTurns = maxTurns;
            CurrentPhase = GamePhase.TurnStart;
            IsPaused = false;
        }
    }

    /// <summary>游戏阶段枚举（随 GameClock 迁入 Domain）</summary>
    public enum GamePhase
    {
        TurnStart,
        InternalAffairs,
        Military,
        Diplomacy,
        Settlement,
        GameOver
    }
}

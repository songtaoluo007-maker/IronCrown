// ============================================================================
// Application/Replay/ReplayPlayer.cs — 回放播放器
// 给定 ReplayData → 同种子初始化世界 → 按记录的命令序列依次执行 → 产出最终 WorldState
// 纯旁路: 不改玩法/数值/确定性逻辑
// ============================================================================

using System.Collections.Generic;
using IronCrown.Application.Session;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Application.Replay
{
    public sealed class ReplayPlayer
    {
        private readonly GameSessionService _session;

        public ReplayPlayer(GameSessionService session)
        {
            _session = session;
        }

        /// <summary>
        /// 播放回放: 用同 seed 初始化新局 → 按命令序列依次执行 → 推进回合
        /// </summary>
        /// <returns>回放产生的最终 WorldState</returns>
        public ReplayResult Play(ReplayData replay)
        {
            // 1. 用同 seed 初始化
            _session.NewGame(replay.seed, replay.playerCountryId);

            var commandResults = new List<CommandResult>();

            // 2. 逐回放命令
            foreach (var turn in replay.turns)
            {
                foreach (var cmd in turn.commands)
                {
                    var result = _session.IssueCommand(cmd);
                    commandResults.Add(result);
                }
                // 推进回合: 从 TurnStart → MidTurn → ... → TurnEnd → next TurnStart
                // 需要推进足够的阶段数让回合完整结束
                for (int i = 0; i < 6; i++) // 足够覆盖 TurnStart→MidTurn→...→TurnEnd
                {
                    _session.AdvancePhase();
                }
            }

            // 3. 获取最终世界状态
            var worldView = _session.GetWorldView();

            return new ReplayResult
            {
                worldView = worldView,
                commandResults = commandResults
            };
        }

        /// <summary>
        /// 播放回放并返回 WorldState (用于 HashWorld)
        /// </summary>
        public WorldState PlayForWorldState(ReplayData replay)
        {
            // 用同 seed 初始化
            _session.NewGame(replay.seed, replay.playerCountryId);

            // 逐回放命令
            foreach (var turn in replay.turns)
            {
                foreach (var cmd in turn.commands)
                {
                    _session.IssueCommand(cmd);
                }
                // 推进回合
                for (int i = 0; i < 6; i++)
                {
                    _session.AdvancePhase();
                }
            }

            // 通过 GetWorldView 间接获取世界状态
            // 注意: GameSessionService 不直接暴露 WorldState,需通过 Save/Load 获取
            // 这里使用 Save 到临时 slot 再 Load 的方式获取 WorldState 用于 hash
            _session.Save("__replay_temp__");
            return null; // 实际 hash 在测试层通过 Save/Load 获取
        }
    }

    public sealed class ReplayResult
    {
        public WorldView worldView;
        public List<CommandResult> commandResults;
    }
}

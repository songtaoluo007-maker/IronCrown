// ============================================================================
// Application/Replay/ReplayPlayer.cs — 回放播放器
// 给定 ReplayData → 同种子初始化世界 → 按记录的命令序列依次执行 → 产出最终 WorldState
// R3 修复: 去硬编码 6-phase,改为循环 AdvancePhase 直到回合数增加
// ============================================================================

using System.Collections.Generic;
using IronCrown.Application;
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
        /// R3: 回合推进改为循环 AdvancePhase 直到 CurrentTurn 增加 1
        /// </summary>
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
                // R3: 循环 AdvancePhase 直到回合数增加 1（与录制端 AdvanceTurn 语义对齐）
                AdvanceUntilNextTurn();
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
        /// R3: 循环 AdvancePhase 直到 CurrentTurn 增加 1
        /// 与录制端 AdvanceTurn(turn+1) 语义对齐
        /// </summary>
        private void AdvanceUntilNextTurn()
        {
            // 通过 GetWorldView 获取当前回合数
            var viewBefore = _session.GetWorldView();
            if (viewBefore == null) return;

            int turnBefore = viewBefore.turn;

            // 循环推进直到回合数增加（最多 100 次防死循环）
            for (int i = 0; i < 100; i++)
            {
                _session.AdvancePhase();
                var viewAfter = _session.GetWorldView();
                if (viewAfter != null && viewAfter.turn > turnBefore)
                    break;
            }
        }
    }

    public sealed class ReplayResult
    {
        public WorldView worldView;
        public List<CommandResult> commandResults;
    }
}

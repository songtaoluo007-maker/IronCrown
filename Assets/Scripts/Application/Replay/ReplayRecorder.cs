// ============================================================================
// Application/Replay/ReplayRecorder.cs — 回放录制器
// 旁路记录: GameSessionService 每次 IssueCommand 时追加命令
// 回合推进时切下一回合。可开关,不影响玩法/确定性
// ============================================================================

using System.Collections.Generic;
using IronCrown.Contracts;

namespace IronCrown.Application.Replay
{
    public sealed class ReplayRecorder
    {
        private ReplayData _data;
        private int _currentTurn;
        private bool _enabled;

        public bool IsRecording => _enabled && _data != null;

        /// <summary>开始录制新会话</summary>
        public void StartRecording(int seed, string playerCountryId, string configId = null, string configVersion = null)
        {
            _data = new ReplayData
            {
                seed = seed,
                playerCountryId = playerCountryId,
                initialConfigId = configId ?? "default",
                initialConfigVersion = configVersion ?? "1.0"
            };
            _currentTurn = 1;
            _enabled = true;

            // 确保第一回合存在
            EnsureTurn(_currentTurn);
        }

        /// <summary>记录一条命令</summary>
        public void RecordCommand(GameCommand cmd)
        {
            if (!IsRecording) return;
            EnsureTurn(_currentTurn);
            // 深拷贝: 避免外部修改同一引用
            var copy = new GameCommand
            {
                commandType = cmd.commandType,
                countryId = cmd.countryId,
                level = cmd.level,
                unitType = cmd.unitType,
                unitId = cmd.unitId,
                targetProvinceId = cmd.targetProvinceId,
                targetCountryId = cmd.targetCountryId,
                configId = cmd.configId,
                commanderId = cmd.commanderId,
                targetCardId = cmd.targetCardId
            };
            _data.turns[_data.turns.Count - 1].commands.Add(copy);
        }

        /// <summary>回合推进,切到下一回合</summary>
        public void AdvanceTurn(int newTurnNumber)
        {
            if (!IsRecording) return;
            _currentTurn = newTurnNumber;
            EnsureTurn(_currentTurn);
        }

        /// <summary>停止录制并返回数据</summary>
        public ReplayData StopRecording()
        {
            _enabled = false;
            var result = _data;
            _data = null;
            return result;
        }

        /// <summary>获取当前录制数据的快照(不停止录制)</summary>
        public ReplayData GetSnapshot()
        {
            return _data;
        }

        private void EnsureTurn(int turnNumber)
        {
            if (_data == null) return;
            // 按需创建回合槽
            while (_data.turns.Count < turnNumber)
            {
                _data.turns.Add(new TurnCommands { turnNumber = _data.turns.Count + 1 });
            }
        }
    }
}

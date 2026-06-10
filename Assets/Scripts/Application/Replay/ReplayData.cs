// ============================================================================
// Application/Replay/ReplayData.cs — 回放数据 DTO
// 记录种子 + 命令流,用于确定性回放验证
// ============================================================================

using System.Collections.Generic;
using IronCrown.Contracts;

namespace IronCrown.Application.Replay
{
    /// <summary>完整回放数据: 种子 + 每回合命令序列</summary>
    public sealed class ReplayData
    {
        public int seed;
        public string initialConfigId;     // 起始配置标识
        public string initialConfigVersion; // 配置版本
        public string playerCountryId;     // 玩家国家 ID
        public List<TurnCommands> turns = new();
    }

    /// <summary>单回合命令序列</summary>
    public sealed class TurnCommands
    {
        public int turnNumber;
        public List<GameCommand> commands = new();
    }
}

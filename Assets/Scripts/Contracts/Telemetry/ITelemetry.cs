// ============================================================================
// Contracts/Telemetry/ITelemetry.cs — P2.6 埋点接口
// 轻量本地埋点，纯旁路记录，不影响确定性/数值
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Contracts
{
    /// <summary>埋点事件端口（Application 层注入，Simulation 纯函数不依赖）</summary>
    public interface ITelemetry
    {
        /// <summary>回合推进事件</summary>
        void TrackTurnAdvanced(int turnNumber, Dictionary<string, CountrySnapshot> countrySnapshots);

        /// <summary>战斗结算事件</summary>
        void TrackBattleResolved(string attackerCountry, string defenderCountry,
            string provinceId, BattleOutcome outcome, int attackerRemaining, int defenderRemaining);

        /// <summary>省份占领事件</summary>
        void TrackProvinceOccupied(string provinceId, string fromCountry, string toCountry, int turnNumber);

        /// <summary>将领解锁事件</summary>
        void TrackCommanderUnlocked(string countryId, string commanderId, string rarity);

        /// <summary>命令发出事件（操作热点统计）</summary>
        void TrackCommandIssued(string commandType, string countryId);

        /// <summary>游戏结束事件</summary>
        void TrackGameOver(string winnerCountryId, int totalTurns, float sessionDurationSeconds,
            string victoryType);

        /// <summary>输出单局汇总 JSON</summary>
        void FlushSessionSummary();
    }

    /// <summary>国家快照（每回合记录）</summary>
    public struct CountrySnapshot
    {
        public string countryId;
        public int capital;
        public int manpower;
        public int provinces;
        public int units;
    }

    /// <summary>战斗结果</summary>
    public enum BattleOutcome
    {
        AttackerWin,
        DefenderWin,
        Stalemate
    }
}

// ============================================================================
// Infrastructure/Telemetry/LocalJsonTelemetry.cs — P2.6 本地 JSON 埋点
// 写本地 JSON 文件，不接外部服务/网络
// 序列化使用 Newtonsoft.Json（规则 8: 不重复造轮子）
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using IronCrown.Contracts;

namespace IronCrown.Infrastructure.Telemetry
{
    public class LocalJsonTelemetry : ITelemetry
    {
        private readonly string _outputDir;
        private readonly List<TelemetryEvent> _events = new();
        private readonly DateTime _sessionStart;
        private int _turnCount;
        private int _battleCount;
        private int _occupationCount;
        private int _commanderUnlockCount;
        private int _commandCount;
        private string _winnerCountryId;
        private string _victoryType;

        public LocalJsonTelemetry(string outputDir = null)
        {
            _outputDir = outputDir ?? Path.Combine("Design", "telemetry");
            _sessionStart = DateTime.UtcNow;
            Directory.CreateDirectory(_outputDir);
        }

        public void TrackTurnAdvanced(int turnNumber, Dictionary<string, CountrySnapshot> countrySnapshots)
        {
            _turnCount = Math.Max(_turnCount, turnNumber);
            _events.Add(new TelemetryEvent
            {
                type = "turn_advanced",
                timestamp = DateTime.UtcNow.ToString("o"),
                turn = turnNumber,
                data = new Dictionary<string, object>
                {
                    // F1 fix: Newtonsoft 序列化匿名类型（property getter → JSON）,
                    // 替换原 JsonUtility 反射 field 导致空对象的问题
                    ["countries"] = countrySnapshots.ToDictionary(
                        kv => kv.Key,
                        kv => (object)new { capital = kv.Value.capital, manpower = kv.Value.manpower, provinces = kv.Value.provinces, units = kv.Value.units })
                }
            });
        }

        public void TrackBattleResolved(string attackerCountry, string defenderCountry,
            string provinceId, BattleOutcome outcome, int attackerRemaining, int defenderRemaining)
        {
            _battleCount++;
            _events.Add(new TelemetryEvent
            {
                type = "battle_resolved",
                timestamp = DateTime.UtcNow.ToString("o"),
                data = new Dictionary<string, object>
                {
                    ["attacker"] = attackerCountry,
                    ["defender"] = defenderCountry,
                    ["province"] = provinceId,
                    ["outcome"] = outcome.ToString(),
                    ["attackerRemaining"] = attackerRemaining,
                    ["defenderRemaining"] = defenderRemaining
                }
            });
        }

        public void TrackProvinceOccupied(string provinceId, string fromCountry, string toCountry, int turnNumber)
        {
            _occupationCount++;
            _events.Add(new TelemetryEvent
            {
                type = "province_occupied",
                timestamp = DateTime.UtcNow.ToString("o"),
                turn = turnNumber,
                data = new Dictionary<string, object>
                {
                    ["province"] = provinceId,
                    ["from"] = fromCountry,
                    ["to"] = toCountry
                }
            });
        }

        public void TrackCommanderUnlocked(string countryId, string commanderId, string rarity)
        {
            _commanderUnlockCount++;
            _events.Add(new TelemetryEvent
            {
                type = "commander_unlocked",
                timestamp = DateTime.UtcNow.ToString("o"),
                data = new Dictionary<string, object>
                {
                    ["country"] = countryId,
                    ["commander"] = commanderId,
                    ["rarity"] = rarity
                }
            });
        }

        public void TrackCommandIssued(string commandType, string countryId)
        {
            _commandCount++;
            _events.Add(new TelemetryEvent
            {
                type = "command_issued",
                timestamp = DateTime.UtcNow.ToString("o"),
                data = new Dictionary<string, object>
                {
                    ["commandType"] = commandType,
                    ["country"] = countryId
                }
            });
        }

        public void TrackGameOver(string winnerCountryId, int totalTurns, float sessionDurationSeconds, string victoryType)
        {
            _winnerCountryId = winnerCountryId;
            _turnCount = totalTurns;
            _victoryType = victoryType;
            _events.Add(new TelemetryEvent
            {
                type = "game_over",
                timestamp = DateTime.UtcNow.ToString("o"),
                turn = totalTurns,
                data = new Dictionary<string, object>
                {
                    ["winner"] = winnerCountryId,
                    ["totalTurns"] = totalTurns,
                    ["durationSeconds"] = sessionDurationSeconds,
                    ["victoryType"] = victoryType
                }
            });
        }

        public void FlushSessionSummary()
        {
            var duration = DateTime.UtcNow - _sessionStart;

            var summary = new SessionSummary
            {
                sessionId = Guid.NewGuid().ToString("N")[..8],
                startTime = _sessionStart.ToString("o"),
                endTime = DateTime.UtcNow.ToString("o"),
                durationSeconds = (float)duration.TotalSeconds,
                totalTurns = _turnCount,
                totalBattles = _battleCount,
                totalOccupations = _occupationCount,
                totalCommanderUnlocks = _commanderUnlockCount,
                totalCommands = _commandCount,
                winnerCountryId = _winnerCountryId,
                victoryType = _victoryType,
                funnel = new SessionFunnel
                {
                    firstBattleTurn = _events.FirstOrDefault(e => e.type == "battle_resolved")?.turn ?? 0,
                    firstOccupationTurn = _events.FirstOrDefault(e => e.type == "province_occupied")?.turn ?? 0,
                    gameOverTurn = _turnCount
                },
                events = _events
            };

            string json = JsonConvert.SerializeObject(summary, Formatting.Indented);
            string filename = $"session-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            string path = Path.Combine(_outputDir, filename);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        // === 内部数据结构 ===

        private class TelemetryEvent
        {
            public string type;
            public string timestamp;
            public int turn;
            public Dictionary<string, object> data;
        }

        private class SessionSummary
        {
            public string sessionId;
            public string startTime;
            public string endTime;
            public float durationSeconds;
            public int totalTurns;
            public int totalBattles;
            public int totalOccupations;
            public int totalCommanderUnlocks;
            public int totalCommands;
            public string winnerCountryId;
            public string victoryType;
            public SessionFunnel funnel;
            public List<TelemetryEvent> events;
        }

        private class SessionFunnel
        {
            public int firstBattleTurn;
            public int firstOccupationTurn;
            public int gameOverTurn;
        }
    }
}

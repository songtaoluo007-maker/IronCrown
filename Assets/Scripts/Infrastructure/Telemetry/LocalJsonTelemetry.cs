// ============================================================================
// Infrastructure/Telemetry/LocalJsonTelemetry.cs — P2.6 本地 JSON 埋点
// 写本地 JSON 文件，不接外部服务/网络
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
                // 漏斗: 发展→首战→首占→终局
                funnel = new SessionFunnel
                {
                    firstBattleTurn = _events.FirstOrDefault(e => e.type == "battle_resolved")?.turn ?? 0,
                    firstOccupationTurn = _events.FirstOrDefault(e => e.type == "province_occupied")?.turn ?? 0,
                    gameOverTurn = _turnCount
                },
                events = _events
            };

            string json = JsonUtility.ToJson(summary, true);
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

        // === 简单 JSON 序列化（不依赖 Newtonsoft） ===
        private static class JsonUtility
        {
            public static string ToJson(object obj, bool pretty = false)
            {
                var sb = new StringBuilder();
                SerializeObject(sb, obj, pretty, 0);
                return sb.ToString();
            }

            private static void SerializeObject(StringBuilder sb, object obj, bool pretty, int indent)
            {
                if (obj == null) { sb.Append("null"); return; }

                var type = obj.GetType();

                if (obj is string s) { sb.Append('"').Append(Escape(s)).Append('"'); return; }
                if (obj is bool b) { sb.Append(b ? "true" : "false"); return; }
                if (obj is int or long or short or byte) { sb.Append(obj); return; }
                if (obj is float f) { sb.Append(f.ToString("F2")); return; }
                if (obj is double d) { sb.Append(d.ToString("F2")); return; }

                if (obj is System.Collections.IDictionary dict)
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        if (pretty) { sb.AppendLine(); Pad(sb, indent + 1); }
                        sb.Append('"').Append(Escape(entry.Key.ToString()!)).Append("\":");
                        if (pretty) sb.Append(' ');
                        SerializeObject(sb, entry.Value, pretty, indent + 1);
                    }
                    if (pretty && !first) { sb.AppendLine(); Pad(sb, indent); }
                    sb.Append('}');
                    return;
                }

                if (obj is System.Collections.IList list)
                {
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        if (pretty) { sb.AppendLine(); Pad(sb, indent + 1); }
                        SerializeObject(sb, list[i], pretty, indent + 1);
                    }
                    if (pretty && list.Count > 0) { sb.AppendLine(); Pad(sb, indent); }
                    sb.Append(']');
                    return;
                }

                // 反射序列化对象字段
                sb.Append('{');
                var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                bool firstField = true;
                foreach (var field in fields)
                {
                    var value = field.GetValue(obj);
                    if (!firstField) sb.Append(',');
                    firstField = false;
                    if (pretty) { sb.AppendLine(); Pad(sb, indent + 1); }
                    sb.Append('"').Append(Escape(field.Name)).Append("\":");
                    if (pretty) sb.Append(' ');
                    SerializeObject(sb, value, pretty, indent + 1);
                }
                if (pretty && fields.Length > 0) { sb.AppendLine(); Pad(sb, indent); }
                sb.Append('}');
            }

            private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
            private static void Pad(StringBuilder sb, int indent) { for (int i = 0; i < indent; i++) sb.Append("  "); }
        }
    }
}

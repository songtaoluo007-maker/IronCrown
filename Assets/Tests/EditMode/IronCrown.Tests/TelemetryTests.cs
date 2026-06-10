// ============================================================================
// TelemetryTests.cs — P2.6 埋点测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Contracts;
using System.Collections.Generic;

namespace IronCrown.Tests
{
    public class TelemetryTests
    {
        private StubTelemetry _stub;

        [SetUp]
        public void SetUp()
        {
            _stub = new StubTelemetry();
        }

        [Test]
        public void TrackTurnAdvanced_RecordsCall()
        {
            var snapshots = new Dictionary<string, CountrySnapshot>
            {
                ["c1"] = new CountrySnapshot { countryId = "c1", capital = 100, manpower = 500, provinces = 4, units = 3 }
            };
            _stub.TrackTurnAdvanced(5, snapshots);

            Assert.AreEqual(1, _stub.TurnAdvancedCount);
            Assert.AreEqual(5, _stub.LastTurnNumber);
        }

        [Test]
        public void TrackBattleResolved_RecordsOutcome()
        {
            _stub.TrackBattleResolved("c1", "c2", "p1", BattleOutcome.AttackerWin, 3, 0);

            Assert.AreEqual(1, _stub.BattleResolvedCount);
            Assert.AreEqual(BattleOutcome.AttackerWin, _stub.LastBattleOutcome);
        }

        [Test]
        public void TrackCommandIssued_RecordsType()
        {
            _stub.TrackCommandIssued("MoveUnit", "c1");
            _stub.TrackCommandIssued("BuildUnit", "c1");

            Assert.AreEqual(2, _stub.CommandIssuedCount);
            Assert.AreEqual("BuildUnit", _stub.LastCommandType);
        }

        [Test]
        public void TrackGameOver_RecordsWinner()
        {
            _stub.TrackGameOver("c1", 30, 600f, "conquest");

            Assert.AreEqual(1, _stub.GameOverCount);
            Assert.AreEqual("c1", _stub.LastWinner);
        }

        [Test]
        public void TrackProvinceOccupied_RecordsTransfer()
        {
            _stub.TrackProvinceOccupied("p1", "c1", "c2", 10);

            Assert.AreEqual(1, _stub.ProvinceOccupiedCount);
            Assert.AreEqual("c2", _stub.LastOccupier);
        }

        [Test]
        public void TrackCommanderUnlocked_RecordsRarity()
        {
            _stub.TrackCommanderUnlocked("c1", "cmd_1", "SSR");

            Assert.AreEqual(1, _stub.CommanderUnlockedCount);
            Assert.AreEqual("SSR", _stub.LastRarity);
        }

        [Test]
        public void FlushSessionSummary_Called()
        {
            _stub.FlushSessionSummary();
            Assert.IsTrue(_stub.FlushCalled);
        }

        [Test]
        public void TurnAdvanced_SerializesCountryData_NotEmpty()
        {
            // F1 验收: 构造 1 国 snapshot → TrackTurnAdvanced → FlushSessionSummary
            // → 读回 JSON, 断言含 "capital" 且 countries 为非空对象
            var telemetry = new IronCrown.Infrastructure.Telemetry.LocalJsonTelemetry(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ic_test_telemetry"));

            var snapshots = new Dictionary<string, CountrySnapshot>
            {
                ["c1"] = new CountrySnapshot { countryId = "c1", capital = 500, manpower = 200, provinces = 3, units = 2 }
            };
            telemetry.TrackTurnAdvanced(1, snapshots);
            telemetry.FlushSessionSummary();

            // 读回写出的 JSON
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ic_test_telemetry");
            var files = System.IO.Directory.GetFiles(dir, "session-*.json");
            Assert.IsTrue(files.Length > 0, "No telemetry file written");

            string json = System.IO.File.ReadAllText(files[^1]);
            Assert.IsTrue(json.Contains("capital"), "JSON missing 'capital' field");
            Assert.IsTrue(json.Contains("500"), "JSON missing capital value 500");
            Assert.IsFalse(json.Contains("\"countries\": {}"), "countries serialized as empty object");

            // cleanup
            System.IO.Directory.Delete(dir, true);
        }

        /// <summary>Stub ITelemetry for testing</summary>
        private class StubTelemetry : ITelemetry
        {
            public int TurnAdvancedCount { get; private set; }
            public int BattleResolvedCount { get; private set; }
            public int ProvinceOccupiedCount { get; private set; }
            public int CommanderUnlockedCount { get; private set; }
            public int CommandIssuedCount { get; private set; }
            public int GameOverCount { get; private set; }
            public bool FlushCalled { get; private set; }

            public int LastTurnNumber { get; private set; }
            public BattleOutcome LastBattleOutcome { get; private set; }
            public string LastCommandType { get; private set; }
            public string LastWinner { get; private set; }
            public string LastOccupier { get; private set; }
            public string LastRarity { get; private set; }

            public void TrackTurnAdvanced(int turnNumber, Dictionary<string, CountrySnapshot> countrySnapshots)
            {
                TurnAdvancedCount++;
                LastTurnNumber = turnNumber;
            }

            public void TrackBattleResolved(string attackerCountry, string defenderCountry,
                string provinceId, BattleOutcome outcome, int attackerRemaining, int defenderRemaining)
            {
                BattleResolvedCount++;
                LastBattleOutcome = outcome;
            }

            public void TrackProvinceOccupied(string provinceId, string fromCountry, string toCountry, int turnNumber)
            {
                ProvinceOccupiedCount++;
                LastOccupier = toCountry;
            }

            public void TrackCommanderUnlocked(string countryId, string commanderId, string rarity)
            {
                CommanderUnlockedCount++;
                LastRarity = rarity;
            }

            public void TrackCommandIssued(string commandType, string countryId)
            {
                CommandIssuedCount++;
                LastCommandType = commandType;
            }

            public void TrackGameOver(string winnerCountryId, int totalTurns, float sessionDurationSeconds, string victoryType)
            {
                GameOverCount++;
                LastWinner = winnerCountryId;
            }

            public void FlushSessionSummary()
            {
                FlushCalled = true;
            }
        }
    }
}

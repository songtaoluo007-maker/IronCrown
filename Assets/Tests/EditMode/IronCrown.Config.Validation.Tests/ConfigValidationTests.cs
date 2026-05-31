// ============================================================================
// ConfigValidationTests.cs — 配置完整性校验（每轮 TDD 首先跑）
// C1: 邻接对称性校验
// ============================================================================

using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace IronCrown.Config.Validation.Tests
{
    public class ConfigValidationTests
    {
        private T LoadConfig<T>(string fileName) where T : class
        {
            var path = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Configs", "Json", fileName);
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<T>(json);
        }

        [System.Serializable]
        class ProvinceList { public List<ProvinceData> items = new(); }
        [System.Serializable]
        class ProvinceData
        {
            public string id;
            public string terrain;
            public string ownerCountry;
            public int gridX;
            public int gridY;
            public string[] neighbors;
        }

        [System.Serializable]
        class CountryList { public List<CountryData> items = new(); }
        [System.Serializable]
        class CountryData
        {
            public string id;
            public string ideology;
            public string mapColor;
            public string capitalProvinceId;
        }

        [Test]
        public void Countries_HasMapColor()
        {
            var config = LoadConfig<CountryList>("countries.json");
            Assert.IsNotNull(config);
            foreach (var c in config.items)
            {
                Assert.IsFalse(string.IsNullOrEmpty(c.mapColor), $"Country {c.id} missing mapColor");
                Assert.IsTrue(c.mapColor.StartsWith("#"), $"Country {c.id} mapColor must start with #");
            }
        }

        [Test]
        public void Provinces_HasGridCoordinates()
        {
            var config = LoadConfig<ProvinceList>("provinces.json");
            Assert.IsNotNull(config);
            foreach (var p in config.items)
            {
                Assert.GreaterOrEqual(p.gridX, 0, $"Province {p.id} gridX < 0");
                Assert.LessOrEqual(p.gridX, 2, $"Province {p.id} gridX > 2");
                Assert.GreaterOrEqual(p.gridY, 0, $"Province {p.id} gridY < 0");
                Assert.LessOrEqual(p.gridY, 2, $"Province {p.id} gridY > 2");
            }
        }

        [Test]
        public void Provinces_HasNeighbors()
        {
            var config = LoadConfig<ProvinceList>("provinces.json");
            Assert.IsNotNull(config);
            foreach (var p in config.items)
            {
                Assert.IsNotNull(p.neighbors, $"Province {p.id} missing neighbors");
                Assert.Greater(p.neighbors.Length, 0, $"Province {p.id} neighbors is empty");
            }
        }

        [Test]
        public void Provinces_NeighborsSymmetric()
        {
            var config = LoadConfig<ProvinceList>("provinces.json");
            Assert.IsNotNull(config);
            var allIds = new HashSet<string>(config.items.Select(p => p.id));

            foreach (var p in config.items)
            {
                if (p.neighbors == null) continue;
                foreach (var nid in p.neighbors)
                {
                    Assert.IsTrue(allIds.Contains(nid), $"Province {p.id} references unknown neighbor {nid}");
                    var neighbor = config.items.Find(x => x.id == nid);
                    Assert.IsNotNull(neighbor, $"Neighbor {nid} not found");
                    Assert.IsTrue(neighbor.neighbors != null && neighbor.neighbors.Contains(p.id),
                        $"Asymmetry: {p.id} → {nid} but {nid} ↛ {p.id}");
                }
            }
        }

        [Test]
        public void Provinces_NoSelfReference()
        {
            var config = LoadConfig<ProvinceList>("provinces.json");
            Assert.IsNotNull(config);
            foreach (var p in config.items)
            {
                if (p.neighbors == null) continue;
                Assert.IsFalse(p.neighbors.Contains(p.id), $"Province {p.id} references itself");
            }
        }

        // === 原有测试 ===

        [Test]
        public void Countries_IdeologyValid()
        {
            var config = LoadConfig<CountryList>("countries.json");
            Assert.IsNotNull(config);
            var validIdeologies = new HashSet<string> { "ImperialOrder", "FreeRepublic", "TradeFederation", "Theocracy", "MilitaryJunta", "ConstitutionalMonarchy", "Collectivism", "Technocrat", "MilitaryGov" };
            foreach (var c in config.items)
                Assert.IsTrue(validIdeologies.Contains(c.ideology), $"Country {c.id} invalid ideology: {c.ideology}");
        }

        [Test]
        public void Countries_UniqueCapital()
        {
            var config = LoadConfig<CountryList>("countries.json");
            Assert.IsNotNull(config);
            var capitals = new HashSet<string>();
            foreach (var c in config.items)
            {
                Assert.IsFalse(capitals.Contains(c.capitalProvinceId), $"Duplicate capital {c.capitalProvinceId}");
                capitals.Add(c.capitalProvinceId);
            }
        }

        [Test]
        public void Countries_CapitalProvinceExists()
        {
            var countries = LoadConfig<CountryList>("countries.json");
            var provinces = LoadConfig<ProvinceList>("provinces.json");
            Assert.IsNotNull(countries);
            Assert.IsNotNull(provinces);
            var provinceIds = new HashSet<string>(provinces.items.Select(p => p.id));
            foreach (var c in countries.items)
                Assert.IsTrue(provinceIds.Contains(c.capitalProvinceId), $"Country {c.id} capital {c.capitalProvinceId} not in provinces");
        }

        [Test]
        public void Provinces_TerrainValid()
        {
            var config = LoadConfig<ProvinceList>("provinces.json");
            Assert.IsNotNull(config);
            var validTerrains = new HashSet<string> { "Plain", "Forest", "Mountain", "Hills", "Desert", "Swamp", "Urban", "Jungle", "Coastline", "River" };
            foreach (var p in config.items)
                Assert.IsTrue(validTerrains.Contains(p.terrain), $"Province {p.id} invalid terrain: {p.terrain}");
        }

        [Test]
        public void Provinces_UniqueOwner()
        {
            var config = LoadConfig<ProvinceList>("provinces.json");
            Assert.IsNotNull(config);
            var owners = new HashSet<string>();
            foreach (var p in config.items)
            {
                Assert.IsFalse(owners.Contains(p.ownerCountry), $"Duplicate owner {p.ownerCountry}");
                owners.Add(p.ownerCountry);
            }
        }

        [System.Serializable]
        class EconomyList { public List<EconomyData> items = new(); }
        [System.Serializable]
        class EconomyData
        {
            public string id;
            public int unitProductionTurns;
            // C5 战争代价
            public int warStabilityPenaltyPerTurn;
            public int warExhaustionPerTurn;
            public int warSupportPenaltyPerLoss;
            public int warSupportBonusPerVictory;
            public int warSupportPenaltyPerCapitalLoss;
            public int aiPeaceAcceptExhaustionThreshold;
            public int aiPeaceAcceptPowerRatioPct;
            // C6 占领抵抗
            public int resistanceOnCapture;
            public int resistanceDecayWithGarrison;
            public int resistanceGrowWithoutGarrison;
            public int resistanceUprisingThreshold;
            public int resistanceUprisingChancePct;
            public int resistanceGarrisonDamageManpower;
            public int resistanceGarrisonDamageEquipment;
            // C7 AI主动求和
            public int aiPeaceOfferExhaustionThreshold;
            public int aiPeaceOfferPowerRatioPct;
            public int aiPeaceOfferCooldownTurns;
            public int aiPeaceOfferExpiryTurns;
        }

        [System.Serializable]
        class UnitConfigList { public List<UnitConfigData> items = new(); }
        [System.Serializable]
        class UnitConfigData
        {
            public string id;
            public int speed;
        }

        [Test]
        public void Economy_HasUnitProductionTurns()
        {
            var config = LoadConfig<EconomyList>("economy.json");
            Assert.IsNotNull(config);
            var eco = config.items.Find(e => e.id == "global");
            Assert.IsNotNull(eco, "economy.json 应有 id='global'");
            Assert.Greater(eco.unitProductionTurns, 0, "unitProductionTurns 应 > 0");
        }

        [Test]
        public void Economy_HasWarFields()
        {
            var config = LoadConfig<EconomyList>("economy.json");
            Assert.IsNotNull(config);
            var eco = config.items.Find(e => e.id == "global");
            Assert.IsNotNull(eco, "economy.json 应有 id='global'");

            // C5 战争代价：7 字段都应 ≥ 0
            Assert.GreaterOrEqual(eco.warStabilityPenaltyPerTurn, 0, "warStabilityPenaltyPerTurn >= 0");
            Assert.GreaterOrEqual(eco.warExhaustionPerTurn, 0, "warExhaustionPerTurn >= 0");
            Assert.GreaterOrEqual(eco.warSupportPenaltyPerLoss, 0, "warSupportPenaltyPerLoss >= 0");
            Assert.GreaterOrEqual(eco.warSupportBonusPerVictory, 0, "warSupportBonusPerVictory >= 0");
            Assert.GreaterOrEqual(eco.warSupportPenaltyPerCapitalLoss, 0, "warSupportPenaltyPerCapitalLoss >= 0");
            Assert.GreaterOrEqual(eco.aiPeaceAcceptExhaustionThreshold, 0, "aiPeaceAcceptExhaustionThreshold >= 0");
            Assert.LessOrEqual(eco.aiPeaceAcceptPowerRatioPct, 100, "aiPeaceAcceptPowerRatioPct <= 100");
            Assert.GreaterOrEqual(eco.aiPeaceAcceptPowerRatioPct, 0, "aiPeaceAcceptPowerRatioPct >= 0");
        }

        [Test]
        public void Economy_HasOccupationFields()
        {
            var config = LoadConfig<EconomyList>("economy.json");
            Assert.IsNotNull(config);
            var eco = config.items.Find(e => e.id == "global");
            Assert.IsNotNull(eco, "economy.json 应有 id='global'");

            Assert.GreaterOrEqual(eco.resistanceOnCapture, 0, "resistanceOnCapture >= 0");
            Assert.LessOrEqual(eco.resistanceOnCapture, 100, "resistanceOnCapture <= 100");
            Assert.Less(eco.resistanceDecayWithGarrison, 0, "resistanceDecayWithGarrison < 0");
            Assert.Greater(eco.resistanceGrowWithoutGarrison, 0, "resistanceGrowWithoutGarrison > 0");
            Assert.GreaterOrEqual(eco.resistanceUprisingThreshold, 0, "resistanceUprisingThreshold >= 0");
            Assert.Greater(eco.resistanceUprisingChancePct, 0, "resistanceUprisingChancePct > 0");
            Assert.LessOrEqual(eco.resistanceUprisingChancePct, 100, "resistanceUprisingChancePct <= 100");
            Assert.Greater(eco.resistanceGarrisonDamageManpower, 0, "resistanceGarrisonDamageManpower > 0");
            Assert.Greater(eco.resistanceGarrisonDamageEquipment, 0, "resistanceGarrisonDamageEquipment > 0");
        }

        [Test]
        public void UnitConfig_HasSpeed()
        {
            var config = LoadConfig<UnitConfigList>("units.json");
            Assert.IsNotNull(config);
            Assert.IsTrue(config.items.Count > 0, "units.json 至少有 1 种单位");
            foreach (var u in config.items)
            {
                Assert.Greater(u.speed, 0, $"unitType={u.id} 的 speed 应 > 0");
            }
        }
    }
}

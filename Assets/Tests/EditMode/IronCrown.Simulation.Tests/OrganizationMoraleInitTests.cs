// ============================================================================
// OrganizationMoraleInitTests.cs — C13-fix 回归测试
// 防止新师 organization/morale 初始化回归（C11 类 bug）
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using IronCrown.Domain;

namespace IronCrown.Simulation.Tests
{
    [TestFixture]
    public class OrganizationMoraleInitTests
    {
        private TestConfigRegistry _config;

        [SetUp]
        public void SetUp()
        {
            _config = new TestConfigRegistry();
            _config.Register("infantry", new UnitConfig
            {
                id = "infantry", name = "步兵",
                attack = 10, defense = 15, breakthrough = 5,
                speed = 3, hp = 100, organization = 60,
                armor = 0, piercing = 5, supplyConsumption = 10,
                cost = new Dictionary<string, int> { { "steel", 5 }, { "food", 10 }, { "capital", 2 } }
            });
            _config.Register("artillery", new UnitConfig
            {
                id = "artillery", name = "炮兵",
                attack = 20, defense = 5, breakthrough = 3,
                speed = 2, hp = 60, organization = 40,
                armor = 0, piercing = 10, supplyConsumption = 15,
                cost = new Dictionary<string, int> { { "steel", 15 }, { "food", 5 }, { "capital", 5 } }
            });
        }

        // =================================================================
        // 回归 1: 新师 organization 必须满编
        // =================================================================

        [Test]
        public void NewDivision_OrganizationEqualsMax()
        {
            var divTemplate = new DivisionTemplate
            {
                id = "test_div", name = "Test Division",
                brigades = new[]
                {
                    new BrigadeEntry { brigadeType = "infantry", count = 9 },
                    new BrigadeEntry { brigadeType = "artillery", count = 3 }
                }
            };

            var unit = UnitFactory.CreateFromDivisionTemplate("U1", divTemplate, "A", "P1", _config);

            Assert.Greater(unit.maxOrganization, 0, "maxOrganization should be set");
            Assert.AreEqual(unit.maxOrganization, unit.organization,
                "New division: current organization must equal max (C13-fix regression)");
        }

        // =================================================================
        // 回归 2: 新师 morale 必须初始化
        // =================================================================

        [Test]
        public void NewDivision_MoraleInitialized()
        {
            var divTemplate = new DivisionTemplate
            {
                id = "test_div", name = "Test Division",
                brigades = new[] { new BrigadeEntry { brigadeType = "infantry", count = 9 } }
            };

            var unit = UnitFactory.CreateFromDivisionTemplate("U1", divTemplate, "A", "P1", _config);

            Assert.AreEqual(50, unit.morale, "New division: morale must be initialized to 50");
        }

        // =================================================================
        // 回归 3: 新师 manpower/equipment 满编
        // =================================================================

        [Test]
        public void NewDivision_ManpowerEquipmentFull()
        {
            var divTemplate = new DivisionTemplate
            {
                id = "test_div", name = "Test Division",
                brigades = new[]
                {
                    new BrigadeEntry { brigadeType = "infantry", count = 9 },
                    new BrigadeEntry { brigadeType = "artillery", count = 3 }
                }
            };

            var unit = UnitFactory.CreateFromDivisionTemplate("U1", divTemplate, "A", "P1", _config);

            Assert.AreEqual(unit.maxManpower, unit.manpower,
                "New division: current manpower must equal max");
            Assert.AreEqual(unit.maxEquipment, unit.equipment,
                "New division: current equipment must equal max");
            Assert.Greater(unit.manpower, 0, "manpower must be > 0");
            Assert.Greater(unit.equipment, 0, "equipment must be > 0");
        }

        // =================================================================
        // 回归 4: RecalculateFromBrigades 只改 max，不改 current
        // =================================================================

        [Test]
        public void Recalculate_DoesNotTouchCurrentValues()
        {
            var divTemplate = new DivisionTemplate
            {
                id = "test_div", name = "Test Division",
                brigades = new[]
                {
                    new BrigadeEntry { brigadeType = "infantry", count = 9 },
                    new BrigadeEntry { brigadeType = "artillery", count = 3 }
                }
            };

            var unit = UnitFactory.CreateFromDivisionTemplate("U1", divTemplate, "A", "P1", _config);

            // 人为降低 current 值（模拟战斗损伤）
            unit.organization = 30;
            unit.manpower = 500;
            unit.equipment = 200;

            int orgBefore = unit.organization;
            int mpBefore = unit.manpower;
            int eqBefore = unit.equipment;

            // 重算（模拟旅级战损后）
            unit.RecalculateFromBrigades(_config);

            Assert.AreEqual(orgBefore, unit.organization,
                "RecalculateFromBrigades must NOT touch current organization");
            Assert.AreEqual(mpBefore, unit.manpower,
                "RecalculateFromBrigades must NOT touch current manpower");
            Assert.AreEqual(eqBefore, unit.equipment,
                "RecalculateFromBrigades must NOT touch current equipment");
        }

        // =================================================================
        // 回归 5: maxOrganization 按旅数量加权
        // =================================================================

        [Test]
        public void MaxOrganization_WeightedByBrigadeCount()
        {
            // 9 步兵(org=60) + 3 炮兵(org=40)
            // 加权平均 = (60×9 + 40×3) / 12 = 54
            var divTemplate = new DivisionTemplate
            {
                id = "test_div", name = "Test Division",
                brigades = new[]
                {
                    new BrigadeEntry { brigadeType = "infantry", count = 9 },
                    new BrigadeEntry { brigadeType = "artillery", count = 3 }
                }
            };

            var unit = UnitFactory.CreateFromDivisionTemplate("U1", divTemplate, "A", "P1", _config);

            int expected = (60 * 9 + 40 * 3) / 12; // 54
            Assert.AreEqual(expected, unit.maxOrganization,
                "maxOrganization must be weighted by brigade count (9 inf + 3 art → 54)");
        }

        // =================================================================
        // 回归 6: 纯步兵师 organization = 步兵 org
        // =================================================================

        [Test]
        public void PureInfantry_OrganizationEqualsInfantryOrg()
        {
            var divTemplate = new DivisionTemplate
            {
                id = "inf_div", name = "Infantry Division",
                brigades = new[] { new BrigadeEntry { brigadeType = "infantry", count = 12 } }
            };

            var unit = UnitFactory.CreateFromDivisionTemplate("U1", divTemplate, "A", "P1", _config);

            Assert.AreEqual(60, unit.maxOrganization,
                "Pure infantry: maxOrganization should be 60");
            Assert.AreEqual(60, unit.organization,
                "Pure infantry: current organization should be 60 (full)");
        }
    }
}

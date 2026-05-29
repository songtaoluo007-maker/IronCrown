// ============================================================================
// Presentation.Tests/MainHudControllerTests.cs — HUD 纯逻辑单元测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Contracts;
using IronCrown.Presentation;
using System.Collections.Generic;

namespace IronCrown.Presentation.Tests
{
    public class MainHudControllerTests
    {
        [Test]
        public void FormatHeader_ReturnsTurnAndPhase()
        {
            var world = new WorldView
            {
                turn = 5,
                phase = "InternalAffairs",
                countries = new List<CountryView>()
            };

            var result = MainHudController.FormatHeader(world);

            Assert.AreEqual("回合 5 · InternalAffairs", result);
        }

        [Test]
        public void FormatCountryRow_ContainsNameAndTreasury()
        {
            var country = new CountryView
            {
                id = "test",
                name = "测试国",
                treasury = 500,
                stability = 75,
                equipmentStockpile = 10,
                resources = new Dictionary<string, int>
                {
                    { "steel", 42 },
                    { "oil", 15 }
                }
            };

            var result = MainHudController.FormatCountryRow(country);

            Assert.That(result, Does.Contain("测试国"));
            Assert.That(result, Does.Contain("500"));
            Assert.That(result, Does.Contain("75"));
            Assert.That(result, Does.Contain("10"));
            Assert.That(result, Does.Contain("steel: 42"));
            Assert.That(result, Does.Contain("oil: 15"));
        }

        [Test]
        public void FormatCountryRow_EmptyResources_NoCrash()
        {
            var country = new CountryView
            {
                id = "test",
                name = "空国",
                treasury = 0,
                stability = 50,
                equipmentStockpile = 0,
                resources = new Dictionary<string, int>()
            };

            var result = MainHudController.FormatCountryRow(country);

            Assert.That(result, Does.Contain("空国"));
            Assert.That(result, Does.Contain("国库: 0"));
        }
    }
}

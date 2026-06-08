// ============================================================================
// ShopResolverTests.cs — [已废弃 P2.1] 商城退役测试
// 验证所有商城方法已正确返回空/false
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Simulation.Tests
{
    /// <summary>
    /// P2.1: 商城已退役。所有方法返回空/false。
    /// </summary>
    public class ShopResolverTests
    {
        private ShopResolver _shop;

        [SetUp]
        public void SetUp()
        {
#pragma warning disable CS0618
            _shop = new ShopResolver();
#pragma warning restore CS0618
        }

        [Test]
        public void BuyBundle_Deprecated_ReturnsFalse()
        {
            var country = new CountryState { id = "empire", gachaTickets = 100 };
#pragma warning disable CS0618
            bool ok = _shop.BuyBundle(country, new EconomyConfig(), 5);
#pragma warning restore CS0618
            Assert.IsFalse(ok);
            Assert.AreEqual(100, country.gachaTickets); // 不变
        }

        [Test]
        public void BuySsrTicket_Deprecated_ReturnsNull()
        {
            var country = new CountryState { id = "empire", gachaTickets = 300 };
            var world = new WorldState();
            world.countries["empire"] = country;
#pragma warning disable CS0618
            var cmdr = _shop.BuySsrTicket(country, world, new RandomService(42), new TestConfigRegistry(), new EconomyConfig(), 5);
#pragma warning restore CS0618
            Assert.IsNull(cmdr);
            Assert.AreEqual(300, country.gachaTickets);
        }

        [Test]
        public void BuySpecificCardTicket_Deprecated_ReturnsNull()
        {
            var country = new CountryState { id = "empire", gachaTickets = 200 };
            var world = new WorldState();
            world.countries["empire"] = country;
#pragma warning disable CS0618
            var cmdr = _shop.BuySpecificCardTicket(country, world, new TestConfigRegistry(), new EconomyConfig(), "general_engineer", 5);
#pragma warning restore CS0618
            Assert.IsNull(cmdr);
            Assert.AreEqual(200, country.gachaTickets);
        }
    }
}

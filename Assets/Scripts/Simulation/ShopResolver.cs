// ============================================================================
// Simulation/ShopResolver.cs — [已废弃 P2.1] 商城系统退役
// 随机抽卡/买券/商城入口全部移除
// ============================================================================

using IronCrown.Domain;
using IronCrown.Contracts;

namespace IronCrown.Simulation
{
    /// <summary>
    /// P2.1 废弃: 随机抽卡+商城已退役。
    /// 保留此类避免破坏 DI 注入;所有方法返回空/false。
    /// </summary>
    [System.Obsolete("P2.1: 商城已退役,使用 CommanderUnlockResolver.UnlockCommander")]
    public sealed class ShopResolver
    {
        public ShopResolver(IEventPublisher events = null, GachaResolver gacha = null) { }

        [System.Obsolete("P2.1: 10连券包已移除")]
        public bool BuyBundle(CountryState country, EconomyConfig eco, int currentTurn) => false;

        [System.Obsolete("P2.1: SSR保底券已移除")]
        public CommanderState BuySsrTicket(CountryState country, WorldState world,
            IRandom rng, IConfigRegistry config, EconomyConfig eco, int currentTurn) => null;

        [System.Obsolete("P2.1: 特定卡券已移除,使用 CommanderUnlockResolver.UnlockCommander")]
        public CommanderState BuySpecificCardTicket(CountryState country, WorldState world,
            IConfigRegistry config, EconomyConfig eco, string cardId, int currentTurn) => null;
    }
}

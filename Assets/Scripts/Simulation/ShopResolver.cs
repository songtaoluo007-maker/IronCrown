// ============================================================================
// Simulation/ShopResolver.cs — 商城系统（C17）
// 用 gachaTickets 购买确定性获取强卡的途径
// ============================================================================

using System;
using System.Linq;
using IronCrown.Domain;
using IronCrown.Contracts;

namespace IronCrown.Simulation
{
    public sealed class ShopResolver
    {
        private readonly IEventPublisher _events;
        private readonly GachaResolver _gacha;

        public ShopResolver(IEventPublisher events, GachaResolver gacha)
        {
            _events = events;
            _gacha = gacha;
        }

        // =====================================================================
        // 1. 10 连券包：8 券 → 给 10 抽机会（折扣体验奖励）
        // =====================================================================

        /// <summary>购买 10 连券包</summary>
        public bool BuyBundle(CountryState country, EconomyConfig eco)
        {
            if (country.gachaTickets < eco.shopBundle10DrawsCost)
                return false;

            country.gachaTickets -= eco.shopBundle10DrawsCost;
            country.gachaTickets += eco.shopBundle10DrawsGrants;

            _events.Publish(new ShopPurchasedEvent
            {
                buyerCountry = country.id,
                itemKind = "bundle_10",
                cost = eco.shopBundle10DrawsCost,
                atTurn = 0
            });
            return true;
        }

        // =====================================================================
        // 2. SSR 保底兑换券：200 券 → 强制 SSR
        // =====================================================================

        /// <summary>购买 SSR 保底兑换券</summary>
        public CommanderState BuySsrTicket(CountryState country, WorldState world,
            IRandom rng, IConfigRegistry config, EconomyConfig eco)
        {
            if (country.gachaTickets < eco.shopSsrTicketCost)
                return null;

            // 找所有 SSR 卡
            var ssrCards = config.All<CommanderConfig>()
                .Where(c => c.rarity == "SSR" && c.id != "general_test_basic")
                .ToList();
            if (ssrCards.Count == 0) return null;

            country.gachaTickets -= eco.shopSsrTicketCost;

            // 随机选一张 SSR
            var picked = ssrCards[rng.Range(0, ssrCards.Count)];
            var cmdr = _gacha.GrantCard(country, world, config, picked.id);

            _events.Publish(new ShopPurchasedEvent
            {
                buyerCountry = country.id,
                itemKind = "ssr_ticket",
                cost = eco.shopSsrTicketCost,
                atTurn = 0
            });
            return cmdr;
        }

        // =====================================================================
        // 3. 特定卡兑换券：100 券 → 直接获得指定卡
        // =====================================================================

        /// <summary>购买特定卡兑换券</summary>
        public CommanderState BuySpecificCardTicket(CountryState country, WorldState world,
            IConfigRegistry config, EconomyConfig eco, string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return null;

            var card = config.Get<CommanderConfig>(cardId);
            if (card == null || card.id == "general_test_basic")
                return null;

            if (country.gachaTickets < eco.shopSpecificCardTicketCost)
                return null;

            country.gachaTickets -= eco.shopSpecificCardTicketCost;
            var cmdr = _gacha.GrantCard(country, world, config, cardId);

            _events.Publish(new ShopPurchasedEvent
            {
                buyerCountry = country.id,
                itemKind = "specific_card",
                cost = eco.shopSpecificCardTicketCost,
                atTurn = 0
            });
            return cmdr;
        }
    }
}

// ============================================================================
// Simulation/CommanderUnlockResolver.cs — 战功点定向解锁（P2.1）
// 替代 GachaResolver 的随机抽卡;战功点定向解锁 + 升星
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;
using IronCrown.Contracts;

namespace IronCrown.Simulation
{
    public sealed class CommanderUnlockResolver
    {
        private readonly IEventPublisher _events;
        private readonly CommanderResolver _commander;

        public CommanderUnlockResolver(IEventPublisher events, CommanderResolver commander = null)
        {
            _events = events;
            _commander = commander;
        }

        // =====================================================================
        // 核心: 定向解锁（替代 DrawCard）
        // =====================================================================

        /// <summary>用战功点定向解锁指定将军卡（新卡→创建;已有→升星）</summary>
        public CommanderState UnlockCommander(CountryState country, WorldState world,
            IConfigRegistry config, EconomyConfig eco, string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;

            var card = config.Get<CommanderConfig>(cardId);
            if (card == null || card.id == "general_test_basic") return null;

            // 计算成本
            int cost = GetMeritCost(card, country, world, config, eco);
            if (country.gachaTickets < cost) return null; // 战功点不足

            // 扣战功点
            country.gachaTickets -= cost;

            // 查找已有将领
            var existing = world.commanders.Values
                .FirstOrDefault(c => c.ownerCountry == country.id && c.generalCardId == cardId);

            if (existing != null)
            {
                // 升星
                if (existing.starLevel < eco.maxStarLevel)
                {
                    existing.starLevel++;
                    _events.Publish(new CardStarUpgradedEvent
                    {
                        commanderId = existing.id,
                        cardId = cardId,
                        newStar = existing.starLevel
                    });
                    return existing;
                }
                else
                {
                    // 满星 → 转化为胜场经验
                    existing.victories += 5;
                    _events.Publish(new CardConvertedToExpEvent
                    {
                        commanderId = existing.id,
                        cardId = cardId,
                        expGained = 5
                    });
                    return existing;
                }
            }

            // 新卡 → 创建将领（不消耗 capital/manpower）
            var newCmdr = CreateCommanderFromCard(card, country, world);
            _events.Publish(new CardDrawnEvent
            {
                rarity = card.rarity ?? "N",
                cardId = cardId,
                commanderId = newCmdr.id
            });
            return newCmdr;
        }

        // =====================================================================
        // 战功点发放（原 AwardTicketsForVictory 改名）
        // =====================================================================

        /// <summary>战斗胜利后发放战功点</summary>
        public void AwardMerit(CountryState country, EconomyConfig eco,
            bool wasEncirclement = false, bool capturedCapital = false)
        {
            country.gachaTickets += eco.gachaTicketsPerVictory;
            if (wasEncirclement)
                country.gachaTickets += eco.gachaTicketsPerEncirclement;
            if (capturedCapital)
                country.gachaTickets += eco.gachaTicketsPerCapitalCapture;
        }

        // =====================================================================
        // 成本计算
        // =====================================================================

        /// <summary>获取解锁/升星成本（按稀有度+星级递增）</summary>
        public int GetMeritCost(CommanderConfig card, CountryState country, WorldState world,
            IConfigRegistry config, EconomyConfig eco)
        {
            int baseCost = card.rarity switch
            {
                "SSR" => eco.meritUnlockCostSSR,
                "SR" => eco.meritUnlockCostSR,
                "R" => eco.meritUnlockCostR,
                _ => eco.meritUnlockCostN
            };

            // 已有卡 → 升星成本 = baseCost × (当前星级+1) × multiplier / 100
            var existing = world.commanders.Values
                .FirstOrDefault(c => c.ownerCountry == country.id && c.generalCardId == card.id);
            if (existing != null)
            {
                int starMultiplier = (existing.starLevel + 1) * eco.meritStarUpMultiplier / 100;
                return baseCost * starMultiplier;
            }

            return baseCost; // 新卡 = 基础成本
        }

        // =====================================================================
        // 内部辅助（复用原 GachaResolver 的 CreateCommanderFromCard）
        // =====================================================================

        private CommanderState CreateCommanderFromCard(CommanderConfig card, CountryState country, WorldState world)
        {
            string id = _commander.GenerateCommanderId(world, country.id);
            var commander = new CommanderState
            {
                id = id,
                name = card.name,
                ownerCountry = country.id,
                generalCardId = card.id,
                rank = 0,
                victories = 0,
                encirclements = 0,
                baseAttack = card.baseAttack,
                baseDefense = card.baseDefense,
                maxDivisions = card.baseMaxDivisions > 0 ? card.baseMaxDivisions : 1,
                starLevel = 0,
                isActive = true
            };

            world.commanders[id] = commander;
            country.commanderIds.Add(id);
            return commander;
        }
    }
}

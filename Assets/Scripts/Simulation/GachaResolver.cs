// ============================================================================
// Simulation/GachaResolver.cs — 单机抽卡系统（C16）
// gachaTickets + DrawCard + 稀有度概率 + 保底 + 升星
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;
using IronCrown.Contracts;

namespace IronCrown.Simulation
{
    public sealed class GachaResolver
    {
        private readonly IEventPublisher _events;
        private readonly CommanderResolver _commander;

        public GachaResolver(IEventPublisher events, CommanderResolver commander = null)
        {
            _events = events;
            _commander = commander;
        }

        // =====================================================================
        // 抽卡核心
        // =====================================================================

        /// <summary>抽卡（消耗 1 券 → 按稀有度概率 → 新卡 or 升星）</summary>
        public CommanderState DrawCard(CountryState country, WorldState world, IRandom rng, IConfigRegistry config, EconomyConfig eco)
        {
            if (country.gachaTickets < eco.gachaTicketCostPerDraw) return null; // 券不足
            country.gachaTickets -= eco.gachaTicketCostPerDraw;
            country.gachaPityCounter++;

            // === 保底判定 ===
            string rarity;
            int totalWeight = eco.gachaRarityWeightN + eco.gachaRarityWeightR
                            + eco.gachaRarityWeightSR + eco.gachaRarityWeightSSR;

            if (country.gachaPityCounter >= eco.gachaSsrPityThreshold)
            {
                // 保底 SSR
                rarity = "SSR";
                country.gachaPityCounter = 0;
            }
            else
            {
                int roll = rng.Range(0, totalWeight);
                if (roll < eco.gachaRarityWeightN)
                    rarity = "N";
                else if (roll < eco.gachaRarityWeightN + eco.gachaRarityWeightR)
                    rarity = "R";
                else if (roll < eco.gachaRarityWeightN + eco.gachaRarityWeightR + eco.gachaRarityWeightSR)
                    rarity = "SR";
                else
                {
                    rarity = "SSR";
                    country.gachaPityCounter = 0; // SSR 重置保底
                }
            }

            // === 从卡池按稀有度取（按 id 升序 + rng 选） ===
            var pool = config.All<CommanderConfig>()
                .Where(c => c.rarity == rarity)
                .OrderBy(c => c.id)
                .ToList();
            if (pool.Count == 0) return null;

            var picked = pool[rng.Range(0, pool.Count)];

            // === 判定升星 or 新卡 ===
            var existing = world.commanders.Values
                .FirstOrDefault(c => c.ownerCountry == country.id && c.generalCardId == picked.id);

            if (existing != null)
            {
                if (existing.starLevel < eco.maxStarLevel)
                {
                    // 升星
                    existing.starLevel++;
                    _events.Publish(new CardStarUpgradedEvent
                    {
                        commanderId = existing.id,
                        cardId = picked.id,
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
                        cardId = picked.id,
                        expGained = 5
                    });
                    return existing;
                }
            }

            // === 新卡 → 创建将领（不消耗 capital/manpower） ===
            var newCmdr = CreateCommanderFromCard(picked, country, world);
            _events.Publish(new CardDrawnEvent
            {
                rarity = rarity,
                cardId = picked.id,
                commanderId = newCmdr.id
            });
            return newCmdr;
        }

        // =====================================================================
        // 直接授予卡牌（商城特定卡券 / SSR 保底券调用）
        // =====================================================================

        /// <summary>直接授予指定卡牌（不消耗券，不走概率）</summary>
        public CommanderState GrantCard(CountryState country, WorldState world, IConfigRegistry config, string cardId)
        {
            var card = config.Get<CommanderConfig>(cardId);
            if (card == null) return null;

            // 检查是否已有此卡
            var existing = world.commanders.Values
                .FirstOrDefault(c => c.ownerCountry == country.id && c.generalCardId == cardId);

            if (existing != null)
            {
                var eco = config.Get<EconomyConfig>("global");
                int maxStar = eco?.maxStarLevel ?? 5;
                if (existing.starLevel < maxStar)
                {
                    existing.starLevel++;
                    _events.Publish(new CardStarUpgradedEvent
                    {
                        commanderId = existing.id,
                        cardId = cardId,
                        newStar = existing.starLevel
                    });
                }
                else
                {
                    existing.victories += 5;
                    _events.Publish(new CardConvertedToExpEvent
                    {
                        commanderId = existing.id,
                        cardId = cardId,
                        expGained = 5
                    });
                }
                return existing;
            }

            // 新卡
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
        // gachaTickets 累积（BattleResolver 调用）
        // =====================================================================

        /// <summary>战斗胜利后发放 gachaTickets</summary>
        public void AwardTicketsForVictory(CountryState country, EconomyConfig eco,
            bool wasEncirclement = false, bool capturedCapital = false)
        {
            country.gachaTickets += eco.gachaTicketsPerVictory;
            if (wasEncirclement)
                country.gachaTickets += eco.gachaTicketsPerEncirclement;
            if (capturedCapital)
                country.gachaTickets += eco.gachaTicketsPerCapitalCapture;
        }

        // =====================================================================
        // 内部辅助
        // =====================================================================

        /// <summary>从将军卡模板创建将领（不消耗资源）</summary>
        private CommanderState CreateCommanderFromCard(CommanderConfig card, CountryState country, WorldState world)
        {
            string id = $"cmdr_{country.id}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
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

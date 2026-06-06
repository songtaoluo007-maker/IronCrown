// ============================================================================
// Simulation/AiPeaceOfferResolver.cs — AI 主动求和 (C7)
// 每回合 Settlement 检查 AI 国家，满足条件则向玩家发求和提议
// ============================================================================

using System;
using System.Linq;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class AiPeaceOfferResolver
    {
        private readonly IEventPublisher _events;

        public AiPeaceOfferResolver(IEventPublisher events)
        {
            _events = events;
        }

        /// <summary>
        /// 每回合 Settlement 阶段调用（WarToll 之后）。
        /// 检查所有 AI 国家与玩家的战争，满足条件则发求和提议。
        /// </summary>
        public void CheckAiPeaceOffers(WorldState world, EconomyConfig eco, string playerCountryId, int currentTurn)
        {
            if (string.IsNullOrEmpty(playerCountryId)) return;

            // 找到玩家参与的所有战争关系
            var playerWars = world.warRelations
                .Where(w => w.countryA == playerCountryId || w.countryB == playerCountryId)
                .ToList();

            foreach (var war in playerWars)
            {
                // 确定 AI 对手
                string aiCountryId = war.countryA == playerCountryId ? war.countryB : war.countryA;

                if (!world.countries.TryGetValue(aiCountryId, out var aiCountry)) continue;
                if (!world.countries.TryGetValue(playerCountryId, out var playerCountry)) continue;

                // 冷却中
                if (aiCountry.peaceOfferCooldown > 0)
                {
                    aiCountry.peaceOfferCooldown--;
                    continue;
                }

                // 已有待处理提议（任何方向）— 检查过期
                if (!string.IsNullOrEmpty(aiCountry.pendingPeaceOfferFrom))
                {
                    if (aiCountry.pendingPeaceOfferExpiry > 0 && currentTurn >= aiCountry.pendingPeaceOfferExpiry)
                    {
                        aiCountry.pendingPeaceOfferFrom = null;
                        aiCountry.pendingPeaceOfferExpiry = 0;
                    }
                    else
                        continue;
                }
                if (!string.IsNullOrEmpty(playerCountry.pendingPeaceOfferFrom))
                {
                    if (playerCountry.pendingPeaceOfferExpiry > 0 && currentTurn >= playerCountry.pendingPeaceOfferExpiry)
                    {
                        playerCountry.pendingPeaceOfferFrom = null;
                        playerCountry.pendingPeaceOfferExpiry = 0;
                    }
                    else
                        continue;
                }

                // 条件 1: warExhaustion ≥ 阈值
                if (aiCountry.warExhaustion < eco.aiPeaceOfferExhaustionThreshold)
                    continue;

                // 条件 2: AI 国力 ≤ 玩家的 N%
                int aiPower = CalculatePower(aiCountry, world);
                int playerPower = CalculatePower(playerCountry, world);
                if (playerPower <= 0) continue; // 防除零

                int powerRatioPct = (aiPower * 100) / playerPower;
                if (powerRatioPct > eco.aiPeaceOfferPowerRatioPct)
                    continue;

                // 条件满足 → 发提议
                playerCountry.pendingPeaceOfferFrom = aiCountryId;
                playerCountry.pendingPeaceOfferExpiry = currentTurn + eco.aiPeaceOfferExpiryTurns;

                _events.Publish(new AiPeaceOfferedEvent
                {
                    fromCountry = aiCountryId,
                    toCountry = playerCountryId,
                    expiryTurnNumber = currentTurn + eco.aiPeaceOfferExpiryTurns
                });
            }
        }

        /// <summary>国力公式（复用 C5）</summary>
        private static int CalculatePower(CountryState country, WorldState world)
        {
            int factoryPower = (country.civilianFactories + country.militaryFactories) * 10;
            int unitPower = country.unitIds
                .Where(id => world.units.ContainsKey(id))
                .Count() * 20;
            return factoryPower + unitPower;
        }
    }
}

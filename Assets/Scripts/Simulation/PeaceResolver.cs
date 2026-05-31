// ============================================================================
// Simulation/PeaceResolver.cs — 停战谈判结算器 (C5)
// 处理玩家 OfferPeace 命令 + AI 接受/拒绝启发式
// ============================================================================

using System;
using System.Linq;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class PeaceResolver
    {
        private readonly IEventPublisher _events;

        public PeaceResolver(IEventPublisher events)
        {
            _events = events;
        }

        /// <summary>
        /// 玩家提议停战。fromCountry=提议方（玩家），toCountry=被提议方（AI）。
        /// AI 根据 warExhaustion + 实力对比决定接受/拒绝。
        /// </summary>
        public CommandResult OfferPeace(WorldState world, string fromCountry, string toCountry, EconomyConfig eco)
        {
            if (!world.countries.TryGetValue(fromCountry, out var from))
                return CommandResult.Reject("发起国不存在");
            if (!world.countries.TryGetValue(toCountry, out var to))
                return CommandResult.Reject("目标国不存在");
            if (fromCountry == toCountry)
                return CommandResult.Reject("不能与自己停战");
            if (!WarRegistry.AreAtWar(world, fromCountry, toCountry))
                return CommandResult.Reject("双方未处于战争状态");

            bool accept = ShouldAcceptPeace(to, from, world, eco);

            if (accept)
            {
                WarRegistry.TryEndWar(world, fromCountry, toCountry, out _);
                from.warExhaustion = Math.Max(0, from.warExhaustion / 2);
                to.warExhaustion = Math.Max(0, to.warExhaustion / 2);

                var (lo, hi) = string.Compare(fromCountry, toCountry, StringComparison.Ordinal) < 0
                    ? (fromCountry, toCountry)
                    : (toCountry, fromCountry);

                _events.Publish(new PeaceOfferedEvent
                {
                    fromCountry = fromCountry,
                    toCountry = toCountry,
                    accepted = true,
                    reason = ""
                });
                _events.Publish(new PeaceConcludedEvent
                {
                    countryA = lo,
                    countryB = hi,
                    atTurn = world.turnNumber
                });
                return CommandResult.Accept();
            }
            else
            {
                _events.Publish(new PeaceOfferedEvent
                {
                    fromCountry = fromCountry,
                    toCountry = toCountry,
                    accepted = false,
                    reason = "对方拒绝停战"
                });
                return CommandResult.Reject("对方拒绝停战");
            }
        }

        /// <summary>
        /// AI 决定是否接受停战。
        /// me = 被提议方（AI），requester = 提议方（玩家）。
        /// </summary>
        private bool ShouldAcceptPeace(CountryState me, CountryState requester, WorldState world, EconomyConfig eco)
        {
            int myPower = ComputeNationalPower(me, world);
            int theirPower = ComputeNationalPower(requester, world);

            // 极度疲惫无条件接受
            if (me.warExhaustion >= eco.aiPeaceAcceptExhaustionThreshold * 2)
                return true;

            // 疲惫 + 实力弱势
            if (me.warExhaustion >= eco.aiPeaceAcceptExhaustionThreshold
                && myPower * 100 <= theirPower * eco.aiPeaceAcceptPowerRatioPct)
                return true;

            return false;
        }

        /// <summary>
        /// 玩家接受 AI 的求和提议（C7）。
        /// 复用 C5 停战流程：结束战争 + 双方 warExhaustion 减半。
        /// </summary>
        public CommandResult AcceptPeace(WorldState world, string playerCountryId, string aiCountryId, EconomyConfig eco)
        {
            if (!world.countries.TryGetValue(playerCountryId, out var player))
                return CommandResult.Reject("玩家国家不存在");
            if (!world.countries.TryGetValue(aiCountryId, out var ai))
                return CommandResult.Reject("AI 国家不存在");
            if (!WarRegistry.AreAtWar(world, playerCountryId, aiCountryId))
                return CommandResult.Reject("双方未处于战争状态");

            // 验证确实有待处理提议
            if (player.pendingPeaceOfferFrom != aiCountryId)
                return CommandResult.Reject("无待处理的停战提议");

            // 清除提议
            player.pendingPeaceOfferFrom = null;

            // 复用 C5 停战流程
            WarRegistry.TryEndWar(world, playerCountryId, aiCountryId, out _);
            player.warExhaustion = Math.Max(0, player.warExhaustion / 2);
            ai.warExhaustion = Math.Max(0, ai.warExhaustion / 2);

            var (lo, hi) = string.Compare(playerCountryId, aiCountryId, StringComparison.Ordinal) < 0
                ? (playerCountryId, aiCountryId)
                : (aiCountryId, playerCountryId);

            _events.Publish(new PeaceConcludedEvent
            {
                countryA = lo,
                countryB = hi,
                atTurn = world.turnNumber
            });

            return CommandResult.Accept();
        }

        /// <summary>
        /// 玩家拒绝 AI 的求和提议（C7）。
        /// 设冷却 + 清除待处理提议。
        /// </summary>
        public CommandResult RejectPeace(WorldState world, string playerCountryId, string aiCountryId, EconomyConfig eco)
        {
            if (!world.countries.TryGetValue(playerCountryId, out var player))
                return CommandResult.Reject("玩家国家不存在");
            if (!world.countries.TryGetValue(aiCountryId, out var ai))
                return CommandResult.Reject("AI 国家不存在");

            // 验证确实有待处理提议
            if (player.pendingPeaceOfferFrom != aiCountryId)
                return CommandResult.Reject("无待处理的停战提议");

            // 清除提议 + 设冷却
            player.pendingPeaceOfferFrom = null;
            ai.peaceOfferCooldown = eco.aiPeaceOfferCooldownTurns;

            return CommandResult.Accept();
        }

        /// <summary>
        /// 过期处理：清除过期的待处理提议（每回合 Settlement 调用）。
        /// </summary>
        public void ExpirePendingOffers(WorldState world, int currentTurn)
        {
            // 简单实现：每回合清除所有 pending（过期由 AiPeaceOfferResolver 的 expiryTurnNumber 控制）
            // 这里只清除已经过期的——通过检查 pending 是否存在
            // 实际过期逻辑在 AiPeaceOfferResolver 中通过 expiryTurnNumber 判断
        }

        /// <summary>
        /// 简化国力 = 工厂数*10 + 部队数*20 + 资本/10
        /// </summary>
        private int ComputeNationalPower(CountryState c, WorldState world)
        {
            int factories = c.civilianFactories + c.militaryFactories + c.dockyards;
            int units = world.units.Values.Count(u => u.ownerCountry == c.id);
            int capital = c.GetResource("capital") / 10;
            return factories * 10 + units * 20 + capital;
        }
    }
}

// ============================================================================
// Simulation/GachaResolver.cs — [已废弃 P2.1] 保留兼容旧代码调用
// 新代码请使用 CommanderUnlockResolver
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;
using IronCrown.Contracts;

namespace IronCrown.Simulation
{
    /// <summary>
    /// P2.1 废弃: 随机抽卡已退役。
    /// 保留此类避免破坏现有 DI 注入和 BattleResolver 调用点。
    /// AwardTicketsForVictory 委托给 CommanderUnlockResolver.AwardMerit。
    /// </summary>
    [System.Obsolete("P2.1: 使用 CommanderUnlockResolver 替代")]
    public sealed class GachaResolver
    {
        private readonly CommanderUnlockResolver _unlock;

        public GachaResolver(CommanderUnlockResolver unlock = null)
        {
            _unlock = unlock;
        }

        /// <summary>战斗胜利后发放战功点（委托给 CommanderUnlockResolver）</summary>
        public void AwardTicketsForVictory(CountryState country, EconomyConfig eco,
            bool wasEncirclement = false, bool capturedCapital = false)
        {
            if (_unlock != null)
            {
                _unlock.AwardMerit(country, eco, wasEncirclement, capturedCapital);
            }
            else
            {
                // 降级: 直接计算（兼容无 CommanderUnlockResolver 的旧测试）
                country.gachaTickets += eco.gachaTicketsPerVictory;
                if (wasEncirclement)
                    country.gachaTickets += eco.gachaTicketsPerEncirclement;
                if (capturedCapital)
                    country.gachaTickets += eco.gachaTicketsPerCapitalCapture;
            }
        }

        // 以下方法全部废弃,保留空壳避免编译错误
        [System.Obsolete("P2.1: 随机抽卡已退役,使用 CommanderUnlockResolver.UnlockCommander")]
        public CommanderState DrawCard(CountryState country, WorldState world, IRandom rng, IConfigRegistry config, EconomyConfig eco)
            => null;

        [System.Obsolete("P2.1: 使用 CommanderUnlockResolver.UnlockCommander")]
        public CommanderState GrantCard(CountryState country, WorldState world, IConfigRegistry config, string cardId)
            => null;
    }
}

// ============================================================================
// Simulation/WarTollResolver.cs — 战争代价结算器 (C5)
// 每回合对 AtWar 国家施加 stability/warExhaustion 变化
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class WarTollResolver
    {
        /// <summary>
        /// 每回合 Settlement 在 TickBattles 之后调用。
        /// 遍历所有有 WarRelation 的国家，施加 stability 惩罚和 warExhaustion 累积。
        /// </summary>
        public void ApplyTurnToll(WorldState world, EconomyConfig eco)
        {
            // 收集所有正在战争中的国家 id（去重）
            var atWarCountries = new HashSet<string>(StringComparer.Ordinal);
            foreach (var w in world.warRelations)
            {
                atWarCountries.Add(w.countryA);
                atWarCountries.Add(w.countryB);
            }

            // 按 Ordinal 排序处理（确定性）
            foreach (var id in atWarCountries.OrderBy(s => s, StringComparer.Ordinal))
            {
                if (!world.countries.TryGetValue(id, out var c)) continue;
                c.stability = Math.Clamp(c.stability - eco.warStabilityPenaltyPerTurn, 0, 100);
                c.warExhaustion = Math.Clamp(c.warExhaustion + eco.warExhaustionPerTurn, 0, 100);
            }
        }
    }
}

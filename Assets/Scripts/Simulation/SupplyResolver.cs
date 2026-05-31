// ============================================================================
// Simulation/SupplyResolver.cs — 补给解析器
// C13: 双条补员（人力 + 装备）
// C14: BFS 补给链 / isCutoff / disorganized
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class SupplyResolver
    {
        /// <summary>C13 stub: 补给检查（C14 完整 BFS 实现）</summary>
        public void CheckSupply(CountryState country, WorldState world)
        {
            // C14: BFS 补给链 / isCutoff / disorganized
        }

        /// <summary>C13: 双条补员 — 每回合 Settlement 末尾调用</summary>
        public void ReplenishUnits(WorldState world, EconomyConfig eco, IConfigRegistry config)
        {
            foreach (var country in world.countries.Values.OrderBy(c => c.id, StringComparer.Ordinal))
            {
                foreach (var unitId in country.unitIds.ToList())
                {
                    if (!world.units.TryGetValue(unitId, out var unit)) continue;

                    // 溃退恢复倒计时
                    if (unit.recoveryTurnsLeft > 0)
                    {
                        unit.recoveryTurnsLeft--;
                        continue;  // 溃退中不补员
                    }

                    // C14: 被切断不补员（C13 默认 false）
                    if (unit.isCutoff) continue;

                    // --- 人力补员 ---
                    int manpowerNeeded = unit.maxManpower - unit.manpower;
                    int manpowerFromPool = manpowerNeeded * eco.reinforceRatePct / 100;
                    int actualManpower = Math.Min(manpowerFromPool, country.manpower);
                    unit.manpower += actualManpower;
                    country.manpower -= actualManpower;

                    // --- 装备补员 ---
                    int equipmentNeeded = unit.maxEquipment - unit.equipment;
                    int equipmentFromPool = equipmentNeeded * eco.reinforceRatePct / 100;
                    int actualEquipment = Math.Min(equipmentFromPool, country.equipmentStockpile);
                    unit.equipment += actualEquipment;
                    country.equipmentStockpile -= actualEquipment;

                    // --- 旅级同步补员 ---
                    if ((actualManpower > 0 || actualEquipment > 0) && unit.brigades != null && unit.brigades.Count > 0)
                    {
                        DistributeReinforcementToBrigades(unit, actualManpower, actualEquipment);
                        if (config != null)
                            unit.RecalculateFromBrigades(config);
                    }
                }
            }
        }

        /// <summary>旅级补员分摊（按 manpower 权重）</summary>
        private void DistributeReinforcementToBrigades(UnitState unit, int manpowerGained, int equipmentGained)
        {
            if (unit.brigades == null || unit.brigades.Count == 0) return;

            // 人力分摊
            if (manpowerGained > 0)
            {
                int totalMp = unit.brigades.Sum(b => b.manpower);
                if (totalMp <= 0)
                {
                    // 全灭旅平均分配
                    int perBrigade = manpowerGained / unit.brigades.Count;
                    foreach (var b in unit.brigades) b.manpower += perBrigade;
                    unit.brigades[0].manpower += manpowerGained - perBrigade * unit.brigades.Count;
                }
                else
                {
                    int distributed = 0;
                    for (int i = 0; i < unit.brigades.Count; i++)
                    {
                        var b = unit.brigades[i];
                        int share = (i == unit.brigades.Count - 1)
                            ? manpowerGained - distributed
                            : manpowerGained * b.manpower / totalMp;
                        b.manpower += share;
                        distributed += share;
                    }
                }
            }

            // 装备分摊
            if (equipmentGained > 0)
            {
                int totalEq = unit.brigades.Sum(b => b.equipment);
                if (totalEq <= 0)
                {
                    int perBrigade = equipmentGained / unit.brigades.Count;
                    foreach (var b in unit.brigades) b.equipment += perBrigade;
                    unit.brigades[0].equipment += equipmentGained - perBrigade * unit.brigades.Count;
                }
                else
                {
                    int distributed = 0;
                    for (int i = 0; i < unit.brigades.Count; i++)
                    {
                        var b = unit.brigades[i];
                        int share = (i == unit.brigades.Count - 1)
                            ? equipmentGained - distributed
                            : equipmentGained * b.equipment / totalEq;
                        b.equipment += share;
                        distributed += share;
                    }
                }
            }
        }
    }
}

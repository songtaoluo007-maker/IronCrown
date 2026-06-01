// ============================================================================
// Simulation/SupplyResolver.cs — 补给解析器（C14 完整实现）
// BFS 补给链 / 4 回合死亡窗口 / 解围 A 方案 / 夹击 morale / disorganized
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class SupplyResolver
    {
        // =====================================================================
        // C14: BFS 补给链核心
        // =====================================================================

        /// <summary>
        /// BFS 补给检查：首都出发，沿己方/友方控制省 BFS，
        /// 标记所有可达省的补给值，不可达省上的部队标记 isCutoff。
        /// </summary>
        public void CheckSupply(CountryState country, WorldState world)
        {
            // 1) BFS 计算补给网络
            var supplyMap = BuildSupplyNetwork(country, world);

            // 2) 遍历该国所有部队，判定补给状态
            foreach (var unitId in country.unitIds.ToList())
            {
                if (!world.units.TryGetValue(unitId, out var unit)) continue;

                bool reachable = supplyMap.TryGetValue(unit.currentProvinceId, out int supplyValue);
                bool hasSupply = reachable && supplyValue > 0;

                if (!hasSupply)
                {
                    // === 被切断 ===
                    unit.isCutoff = true;
                    ApplyCutoffEffects(unit, world);
                }
                else
                {
                    // === 补给可达 ===
                    unit.isCutoff = false;
                    unit.cutoffTurns = 0;

                    // 补给不足 → disorganized 状态
                    int required = unit.supplyConsumption;
                    if (required > 0 && supplyValue < required)
                    {
                        int deficitPct = (required - supplyValue) * 100 / required;
                        ApplySupplyDeficit(unit, deficitPct, world);
                    }
                    else
                    {
                        unit.isDisorganized = false;
                    }
                }
            }

            // 3) 夹击/包围 morale 检查
            ApplyFlankingMorale(country, world);
        }

        /// <summary>
        /// BFS 从首都出发，计算每个可达省份的补给值。
        /// 补给值 = 省份补给容量，沿链路衰减。
        /// </summary>
        private Dictionary<string, int> BuildSupplyNetwork(CountryState country, WorldState world)
        {
            var result = new Dictionary<string, int>();

            // 找首都
            string capitalId = country.capitalProvinceId;
            if (string.IsNullOrEmpty(capitalId)) return result;
            if (!world.provinces.TryGetValue(capitalId, out var capital)) return result;

            // 首都必须在己方控制下
            if (capital.controllerCountry != country.id) return result;

            // BFS
            var queue = new Queue<string>();
            var visited = new HashSet<string>();

            int capitalSupply = capital.CalculateSupplyCapacity();
            result[capitalId] = capitalSupply;
            visited.Add(capitalId);
            queue.Enqueue(capitalId);

            while (queue.Count > 0)
            {
                string currentId = queue.Dequeue();
                if (!world.provinces.TryGetValue(currentId, out var current)) continue;

                int currentSupply = result[currentId];
                if (current.neighbors == null) continue;

                foreach (var neighborId in current.neighbors)
                {
                    if (visited.Contains(neighborId)) continue;
                    if (!world.provinces.TryGetValue(neighborId, out var neighbor)) continue;

                    // 必须是己方或友方控制
                    if (!IsFriendlyControlled(neighbor, country, world)) continue;

                    // 补给流 = min(当前省补给, 邻省容量) × 传输效率
                    int neighborCap = neighbor.CalculateSupplyCapacity();
                    int flowEfficiency = GetFlowEfficiency(current, neighbor);
                    int supplyFlow = Math.Min(currentSupply, neighborCap) * flowEfficiency / 100;

                    // 基础衰减：每跳 -10%
                    supplyFlow = supplyFlow * 90 / 100;

                    if (supplyFlow > 0)
                    {
                        result[neighborId] = supplyFlow;
                        visited.Add(neighborId);
                        queue.Enqueue(neighborId);
                    }
                }
            }

            return result;
        }

        /// <summary>是否为友方控制（己方或盟友）</summary>
        private bool IsFriendlyControlled(ProvinceState province, CountryState country, WorldState world)
        {
            // 己方控制
            if (province.controllerCountry == country.id) return true;

            // 盟友控制（同一战争阵营）
            if (!string.IsNullOrEmpty(province.controllerCountry))
            {
                // 检查是否有共同敌人（即盟友关系）
                // 简化：不在战争中 = 友方
                if (!WarRegistry.AreAtWar(world, country.id, province.controllerCountry))
                    return true;
            }

            return false;
        }

        /// <summary>传输效率（基于基础设施差异）</summary>
        private int GetFlowEfficiency(ProvinceState from, ProvinceState to)
        {
            // 铁路连接 +20%，港口 +10%
            int efficiency = 80; // 基础 80%
            if (from.railwayLevel > 0 && to.railwayLevel > 0) efficiency += 20;
            if (from.portLevel > 0 && to.portLevel > 0) efficiency += 10;
            return Math.Min(100, efficiency);
        }

        // =====================================================================
        // C14: 被切断效果（4 回合死亡窗口）
        // =====================================================================

        /// <summary>应用被切断补给效果</summary>
        private void ApplyCutoffEffects(UnitState unit, WorldState world)
        {
            unit.cutoffTurns++;

            // 组织度每回合 -15
            unit.organization = Math.Max(0, unit.organization - 15);

            // 士气每回合 -20
            unit.morale = Math.Max(0, unit.morale - 20);

            // 标记 disorganized
            unit.isDisorganized = true;

            // 4 回合死亡窗口：补给耗尽 → 部队消亡
            if (unit.cutoffTurns >= 4)
            {
                // 标记为待销毁（由 TurnResolver 处理）
                unit.organization = 0;
                unit.manpower = 0;
                unit.equipment = 0;
            }
        }

        /// <summary>补给不足但未切断 → 部分惩罚</summary>
        private void ApplySupplyDeficit(UnitState unit, int deficitPct, WorldState world)
        {
            // 缺额 > 50% → disorganized
            if (deficitPct >= 50)
            {
                unit.isDisorganized = true;
                // 组织度恢复减半
                unit.organization = Math.Max(0, unit.organization - 5);
                // 士气 -10
                unit.morale = Math.Max(0, unit.morale - 10);
            }
            else
            {
                unit.isDisorganized = false;
            }
        }

        // =====================================================================
        // C14: 夹击/包围 morale 惩罚
        // =====================================================================

        /// <summary>检查夹击/包围 morale 效果</summary>
        private void ApplyFlankingMorale(CountryState country, WorldState world)
        {
            foreach (var unitId in country.unitIds.ToList())
            {
                if (!world.units.TryGetValue(unitId, out var unit)) continue;
                if (!world.provinces.TryGetValue(unit.currentProvinceId, out var province)) continue;
                if (province.neighbors == null) continue;

                // 统计敌方控制的邻省数量
                int enemyNeighbors = 0;
                foreach (var neighborId in province.neighbors)
                {
                    if (world.provinces.TryGetValue(neighborId, out var neighbor))
                    {
                        if (!string.IsNullOrEmpty(neighbor.controllerCountry)
                            && neighbor.controllerCountry != country.id
                            && WarRegistry.AreAtWar(world, country.id, neighbor.controllerCountry))
                        {
                            enemyNeighbors++;
                        }
                    }
                }

                // 被 2+ 敌省包围 → morale 惩罚
                if (enemyNeighbors >= 2)
                {
                    int moralePenalty = (enemyNeighbors - 1) * 10; // 每多一个敌省 -10
                    unit.morale = Math.Max(0, unit.morale - moralePenalty);
                }
            }
        }

        // =====================================================================
        // C14: 解围 A 方案
        // =====================================================================

        /// <summary>
        /// 解围检查：当友军攻入被包围省份的邻省时，
        /// 解除该省上己方部队的 isCutoff 状态。
        /// 由 BattleResolver 在占领后调用。
        /// </summary>
        public static void CheckRelief(WorldState world, string relievedProvinceId, string relieverCountryId)
        {
            if (!world.provinces.TryGetValue(relievedProvinceId, out var province)) return;

            // 遍历该省上的所有友方部队
            var unitsToRelieve = world.units.Values
                .Where(u => u.currentProvinceId == relievedProvinceId && u.ownerCountry == relieverCountryId)
                .ToList();

            foreach (var unit in unitsToRelieve)
            {
                if (unit.isCutoff)
                {
                    unit.isCutoff = false;
                    unit.cutoffTurns = 0;
                    unit.isDisorganized = false;
                }
            }
        }

        /// <summary>
        /// 全局解围检查：每回合 Settlement 阶段调用。
        /// 检查所有被切断部队，如果其所在省份有友方邻省，则解除切断。
        /// </summary>
        public void CheckGlobalRelief(WorldState world)
        {
            foreach (var unit in world.units.Values.ToList())
            {
                if (!unit.isCutoff) continue;
                if (!world.provinces.TryGetValue(unit.currentProvinceId, out var province)) continue;
                if (province.neighbors == null) continue;

                // 检查是否有友方邻省
                bool hasFriendlyNeighbor = false;
                foreach (var neighborId in province.neighbors)
                {
                    if (world.provinces.TryGetValue(neighborId, out var neighbor))
                    {
                        if (neighbor.controllerCountry == unit.ownerCountry)
                        {
                            hasFriendlyNeighbor = true;
                            break;
                        }
                    }
                }

                // 有友方邻省 → 解除切断
                if (hasFriendlyNeighbor)
                {
                    unit.isCutoff = false;
                    unit.cutoffTurns = 0;
                    unit.isDisorganized = false;
                }
            }
        }

        // =====================================================================
        // C13: 双条补员（保持不变，增加 isCutoff 检查）
        // =====================================================================

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

                    // C14: 被切断不补员
                    if (unit.isCutoff) continue;

                    // C14: disorganized 时补员率减半
                    int reinforceRate = unit.isDisorganized
                        ? eco.reinforceRatePct / 2
                        : eco.reinforceRatePct;

                    // --- 人力补员 ---
                    int manpowerNeeded = unit.maxManpower - unit.manpower;
                    int manpowerFromPool = manpowerNeeded * reinforceRate / 100;
                    int actualManpower = Math.Min(manpowerFromPool, country.manpower);
                    unit.manpower += actualManpower;
                    country.manpower -= actualManpower;

                    // --- 装备补员 ---
                    int equipmentNeeded = unit.maxEquipment - unit.equipment;
                    int equipmentFromPool = equipmentNeeded * reinforceRate / 100;
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

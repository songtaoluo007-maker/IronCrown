// ============================================================================
// Simulation/CommanderResolver.cs — 将领系统（C15a）
// 军衔晋升 / 指挥容量 / 包围加权 / 战斗 buff
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class CommanderResolver
    {
        private readonly IConfigRegistry _config;

        public CommanderResolver(IConfigRegistry config)
        {
            _config = config;
        }

        /// <summary>确定性生成 commander id：扫描该国现有最大序号 +1（读档后仍唯一）</summary>
        public string GenerateCommanderId(WorldState world, string countryId)
        {
            int max = 0;
            foreach (var c in world.commanders.Values)
            {
                if (c.ownerCountry != countryId) continue;
                var parts = c.id.Split('_');
                if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int n))
                    max = System.Math.Max(max, n);
            }
            return $"cmdr_{countryId}_{max + 1}";
        }

        // =====================================================================
        // 招募将领
        // =====================================================================

        /// <summary>招募将领（消耗 capital + manpower）</summary>
        public CommanderState RecruitCommander(CountryState country, string configId, WorldState world)
        {
            var cfg = _config.Get<CommanderConfig>(configId);
            if (cfg == null) return null;

            int capitalCost = cfg.recruitCapitalCost > 0 ? cfg.recruitCapitalCost : 100;
            int manpowerCost = cfg.recruitManpowerCost > 0 ? cfg.recruitManpowerCost : 500;

            // 检查资源
            if (country.GetResource("capital") < capitalCost) return null;
            if (country.manpower < manpowerCost) return null;

            // 扣除
            country.ModifyResource("capital", -capitalCost);
            country.manpower -= manpowerCost;

            // 创建将领（确定性 id）
            string id = GenerateCommanderId(world, country.id);
            var commander = new CommanderState
            {
                id = id,
                name = cfg.name,
                ownerCountry = country.id,
                generalCardId = configId,
                rank = 0,
                victories = 0,
                encirclements = 0,
                baseAttack = cfg.baseAttack,
                baseDefense = cfg.baseDefense,
                maxDivisions = cfg.baseMaxDivisions > 0 ? cfg.baseMaxDivisions : 1,
                isActive = true
            };

            world.commanders[id] = commander;
            country.commanderIds.Add(id);
            return commander;
        }

        // =====================================================================
        // 军衔晋升
        // =====================================================================

        /// <summary>检查并执行晋升（每回合 Settlement 阶段调用）</summary>
        public void CheckPromotions(WorldState world)
        {
            foreach (var cmdr in world.commanders.Values)
            {
                if (!cmdr.isActive) continue;
                if (cmdr.CanPromote)
                {
                    cmdr.TryPromote();
                }
            }
        }

        /// <summary>记录战斗胜场（BattleResolver 调用）</summary>
        public void RecordBattleVictory(WorldState world, string commanderId, bool isEncirclement = false)
        {
            if (string.IsNullOrEmpty(commanderId)) return;
            if (!world.commanders.TryGetValue(commanderId, out var cmdr)) return;
            cmdr.RecordVictory(isEncirclement);
        }

        // =====================================================================
        // 指挥容量检查
        // =====================================================================

        /// <summary>获取将领当前指挥的师数</summary>
        public int GetCommandedDivisionCount(WorldState world, string commanderId)
        {
            return world.units.Values.Count(u => u.commanderId == commanderId);
        }

        /// <summary>将领是否还能指挥更多师</summary>
        public bool CanCommandMore(WorldState world, string commanderId)
        {
            if (!world.commanders.TryGetValue(commanderId, out var cmdr)) return false;
            return GetCommandedDivisionCount(world, commanderId) < cmdr.maxDivisions;
        }

        /// <summary>分配师到将领</summary>
        public bool AssignDivision(WorldState world, string unitId, string commanderId)
        {
            if (!world.units.TryGetValue(unitId, out var unit)) return false;
            if (!world.commanders.TryGetValue(commanderId, out var cmdr)) return false;
            if (!cmdr.isActive) return false;
            if (unit.ownerCountry != cmdr.ownerCountry) return false;

            // 检查指挥容量
            if (!CanCommandMore(world, commanderId)) return false;

            unit.commanderId = commanderId;
            return true;
        }

        /// <summary>解除将领指挥</summary>
        public void UnassignDivision(WorldState world, string unitId)
        {
            if (world.units.TryGetValue(unitId, out var unit))
                unit.commanderId = null;
        }

        // =====================================================================
        // 战斗 buff 计算
        // =====================================================================

        /// <summary>将领攻击 buff 百分比（100 = ×1.0）</summary>
        public int GetCommanderAttackBuffPct(WorldState world, string commanderId)
        {
            if (string.IsNullOrEmpty(commanderId)) return 100;
            if (!world.commanders.TryGetValue(commanderId, out var cmdr)) return 100;
            return 100 + cmdr.baseAttack + cmdr.RankAttackBonusPct;
        }

        /// <summary>将领防御 buff 百分比（100 = ×1.0）</summary>
        public int GetCommanderDefenseBuffPct(WorldState world, string commanderId)
        {
            if (string.IsNullOrEmpty(commanderId)) return 100;
            if (!world.commanders.TryGetValue(commanderId, out var cmdr)) return 100;
            return 100 + cmdr.baseDefense + cmdr.RankDefenseBonusPct;
        }

        // =====================================================================
        // 同省 5 师容量限制
        // =====================================================================

        /// <summary>获取某省某方的师数（攻/守独立计数）</summary>
        public int GetDivisionCountInProvince(WorldState world, string provinceId, string countryId)
        {
            return world.units.Values.Count(u =>
                u.currentProvinceId == provinceId && u.ownerCountry == countryId);
        }

        /// <summary>检查省份是否还能容纳更多师（5 师上限）</summary>
        public bool CanFitMoreDivisions(WorldState world, string provinceId, string countryId, int maxPerSide = 5)
        {
            return GetDivisionCountInProvince(world, provinceId, countryId) < maxPerSide;
        }
    }
}

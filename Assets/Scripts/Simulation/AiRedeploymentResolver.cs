// ============================================================================
// Simulation/AiRedeploymentResolver.cs — AI 调防 (C8)
// 内陆有富裕部队、前线弱守时自动调一支到前线
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class AiRedeploymentResolver
    {
        private readonly MovementResolver _movement;

        public AiRedeploymentResolver(MovementResolver movement)
        {
            _movement = movement;
        }

        /// <summary>
        /// 每回合 Military 阶段调用（TryAttack 之后）。
        /// 检查 AI 国家前线弱守省份，从内陆调防。
        /// </summary>
        public void TryRedeploy(CountryState country, WorldState world, EconomyConfig eco)
        {
            // 跳过玩家
            if (country.id == world.playerCountryId) return;

            int redeploysLeft = eco.aiMaxRedeploysPerTurn;
            if (redeploysLeft <= 0) return;

            // 找前线弱守省份（按 id 升序确定性遍历）
            var targets = FindVulnerableFrontier(country, world, eco);

            foreach (var targetId in targets)
            {
                if (redeploysLeft <= 0) break;

                // 找内陆源省（有 ≥2 驻军，所有邻省己方控制）
                var sourceId = FindSourceProvince(country, world, targetId);
                if (sourceId == null) continue;

                // 选调防部队：源省按 unit.id 升序 [0]，movesLeft ≥ 1，不在战斗中
                var unit = SelectRedeployUnit(world, sourceId, country.id);
                if (unit == null) continue;

                // 复用 MovementResolver.TryMove（规则 8）
                var result = _movement.TryMove(world, unit.id, targetId, country.id);
                if (result.accepted)
                {
                    redeploysLeft--;
                }
            }
        }

        /// <summary>
        /// 找前线弱守省份：邻接敌控省含敌方部队 AND 自己守军战力 ≤ 邻敌 × ratio%
        /// </summary>
        private List<string> FindVulnerableFrontier(CountryState country, WorldState world, EconomyConfig eco)
        {
            var vulnerable = new List<string>();

            foreach (var province in world.provinces.Values
                .Where(p => p.controllerCountry == country.id)
                .OrderBy(p => p.id, System.StringComparer.Ordinal))
            {
                if (province.neighbors == null) continue;

                // 该省现有守军战力
                int garrisonPower = GetGarrisonPower(world, province.id, country.id);

                // 检查每个邻省
                foreach (var nId in province.neighbors.OrderBy(s => s, System.StringComparer.Ordinal))
                {
                    if (!world.provinces.TryGetValue(nId, out var nProv)) continue;
                    if (nProv.controllerCountry == country.id) continue; // 己方省，跳过

                    // 邻省是否有敌方部队？
                    var enemyUnits = world.units.Values
                        .Where(u => u.currentProvinceId == nId && u.ownerCountry != country.id)
                        .ToList();
                    if (enemyUnits.Count == 0) continue;

                    int enemyPower = enemyUnits.Sum(u => UnitCombatPower(u));

                    // 守军战力 ≤ 邻敌 × ratio% → 弱守
                    if (garrisonPower * 100 <= enemyPower * eco.aiRedeployVulnerableRatioPct)
                    {
                        vulnerable.Add(province.id);
                        break; // 一个省只需一个弱邻即触发
                    }
                }
            }

            return vulnerable;
        }

        /// <summary>
        /// 找内陆源省：直接邻接目标 + 所有邻省己方控制 + 驻军 ≥ 2
        /// </summary>
        private string FindSourceProvince(CountryState country, WorldState world, string targetId)
        {
            if (!world.provinces.TryGetValue(targetId, out var target)) return null;
            if (target.neighbors == null) return null;

            foreach (var nId in target.neighbors.OrderBy(s => s, System.StringComparer.Ordinal))
            {
                if (!world.provinces.TryGetValue(nId, out var nProv)) continue;
                if (nProv.controllerCountry != country.id) continue; // 非己方

                // 检查所有邻省是否己方控制（内陆判定）
                if (nProv.neighbors == null) continue;
                bool allOwned = nProv.neighbors.All(nn =>
                    world.provinces.TryGetValue(nn, out var nnP) && nnP.controllerCountry == country.id);
                if (!allOwned) continue;

                // 驻军 ≥ 2
                int garrisonCount = world.units.Values
                    .Count(u => u.currentProvinceId == nId && u.ownerCountry == country.id);
                if (garrisonCount < 2) continue;

                return nId;
            }

            return null;
        }

        /// <summary>
        /// 选调防部队：源省按 unit.id 升序 [0]，movesLeft ≥ 1，不在战斗中
        /// </summary>
        private UnitState SelectRedeployUnit(WorldState world, string sourceId, string countryId)
        {
            return world.units.Values
                .Where(u => u.currentProvinceId == sourceId
                         && u.ownerCountry == countryId
                         && u.movesLeft >= 1
                         && !world.activeBattles.Any(b => b.attackerUnitIds.Contains(u.id) || b.defenderUnitIds.Contains(u.id)))
                .OrderBy(u => u.id, System.StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>单位战力 = organization + morale + experience * 10</summary>
        private static int UnitCombatPower(UnitState u)
        {
            return u.organization + u.morale + u.experience * 10;
        }

        /// <summary>省份驻军总战力</summary>
        private static int GetGarrisonPower(WorldState world, string provinceId, string countryId)
        {
            return world.units.Values
                .Where(u => u.currentProvinceId == provinceId && u.ownerCountry == countryId)
                .Sum(u => UnitCombatPower(u));
        }
    }
}

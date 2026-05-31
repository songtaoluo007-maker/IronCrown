// ============================================================================
// Simulation/AIResolver.cs — AI 决策器
// B3: 注入 IConfigRegistry + ConstructionResolver，实现经济 AI
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class AIResolver
    {
        private readonly IConfigRegistry _config;
        private readonly ConstructionResolver _construction;
        private readonly BattleResolver _battle;
        private readonly AiRedeploymentResolver _redeploy;

        public AIResolver(IConfigRegistry config, ConstructionResolver construction, BattleResolver battle,
            AiRedeploymentResolver redeploy = null)
        {
            _config = config;
            _construction = construction;
            _battle = battle;
            _redeploy = redeploy;
        }

        public void MakeDecisions(CountryState country, WorldState world)
        {
            // B3: 跳过玩家国
            if (country.id == world.playerCountryId) return;

            var eco = _config.Get<EconomyConfig>("global");
            if (eco == null) return;

            // 经济 AI：capital 充足且未达上限 → 建厂
            if (country.GetResource("capital") >= eco.aiBuildCapitalThreshold)
            {
                if (country.civilianFactories < eco.aiMaxCivilianFactories)
                    _construction.TryBuild(country, "civilian", eco);
                else if (country.militaryFactories < eco.aiMaxMilitaryFactories)
                    _construction.TryBuild(country, "military", eco);
            }

            // 保留战略/战役/战术桩（未来军事 AI 用）
            var strategy = EvaluateStrategicSituation(country, world);
            var campaign = EvaluateCampaignGoals(country, world, strategy);
            ExecuteTacticalOrders(country, world, campaign);

            // C4: 军事 AI — 主动进攻邻接弱者
            TryAttack(country, world);

            // C8: AI 调防 — 内陆富裕部队调往前线弱守省
            if (_redeploy != null)
                _redeploy.TryRedeploy(country, world, eco);
        }

        private AIStrategy EvaluateStrategicSituation(CountryState country, WorldState world)
        {
            var strategy = new AIStrategy();
            int threatLevel = AssessThreat(country, world);
            strategy.threatLevel = threatLevel;

            bool strongEconomy = country.treasury > 500 && country.civilianFactories >= 5;
            bool weakEconomy = country.treasury < 100;

            if (threatLevel > 70)
            {
                strategy.priority = AIPriority.Defense;
                strategy.shouldMobilize = true;
            }
            else if (threatLevel < 30 && strongEconomy && country.warSupport > 60)
            {
                strategy.priority = AIPriority.Expansion;
                strategy.shouldExpandArmy = true;
            }
            else if (weakEconomy)
            {
                strategy.priority = AIPriority.Industrial;
                strategy.shouldBuildFactories = true;
            }
            else
            {
                strategy.priority = AIPriority.Balanced;
            }

            return strategy;
        }

        private AICampaign EvaluateCampaignGoals(CountryState country, WorldState world, AIStrategy strategy)
        {
            var campaign = new AICampaign();
            switch (strategy.priority)
            {
                case AIPriority.Defense:
                    campaign.defendTargets = FindVulnerableProvinces(country, world);
                    break;
                case AIPriority.Expansion:
                    campaign.attackTargets = FindWeakNeighborProvinces(country, world);
                    break;
                case AIPriority.Industrial:
                    campaign.buildTargets = FindIndustrialTargets(country, world);
                    break;
            }
            return campaign;
        }

        private void ExecuteTacticalOrders(CountryState country, WorldState world, AICampaign campaign)
        {
            // TODO: 遍历单位，分配移动/攻击/防守指令
        }

        // === C4: 军事 AI ===

        private void TryAttack(CountryState country, WorldState world)
        {
            var eco = _config.Get<EconomyConfig>("global");
            if (eco == null) return;

            int attacksLeft = eco.aiMaxAttacksPerTurn;
            if (attacksLeft <= 0) return;

            foreach (var unit in world.units.Values
                .Where(u => u.ownerCountry == country.id && u.movesLeft >= 1)
                .OrderBy(u => u.id, System.StringComparer.Ordinal))
            {
                if (attacksLeft <= 0) break;

                // 跳过战斗中部队
                if (world.activeBattles.Any(b => b.attackerUnitId == unit.id || b.defenderUnitId == unit.id))
                    continue;

                if (!world.provinces.TryGetValue(unit.currentProvinceId, out var cur))
                    continue;
                if (cur.neighbors == null) continue;

                foreach (var nId in cur.neighbors.OrderBy(s => s, System.StringComparer.Ordinal))
                {
                    if (!world.provinces.TryGetValue(nId, out var nProv)) continue;
                    if (nProv.controllerCountry == country.id) continue; // 己方

                    // 目标省已有战斗 → 跳过
                    if (world.activeBattles.Any(b => b.provinceId == nId)) continue;

                    if (!IsAttackerStrongEnough(unit, nProv, world, eco)) continue;

                    var result = _battle.InitiateAttack(world, unit.id, nId, country.id);
                    if (result.accepted)
                    {
                        attacksLeft--;
                        break; // 该 unit 已发动，换下一支
                    }
                }
            }
        }

        private bool IsAttackerStrongEnough(UnitState unit, ProvinceState target, WorldState world, EconomyConfig eco)
        {
            // 取该省非己方控制者部队
            var defenders = world.units.Values
                .Where(u => u.currentProvinceId == target.id && u.ownerCountry != unit.ownerCountry)
                .OrderBy(u => u.id, System.StringComparer.Ordinal)
                .ToList();

            // 空城 → 允许
            if (defenders.Count == 0) return true;

            var def = defenders[0];
            int atkPower = unit.baseAttack * unit.organization * 100 / System.Math.Max(1, unit.maxOrganization);
            int defPower = def.baseDefense * def.organization * 100 / System.Math.Max(1, def.maxOrganization);

            return atkPower * 100 >= defPower * eco.aiAttackPowerRatio;
        }

        private int AssessThreat(CountryState country, WorldState world) => 0;
        private List<string> FindVulnerableProvinces(CountryState country, WorldState world) => new();
        private List<string> FindWeakNeighborProvinces(CountryState country, WorldState world) => new();
        private List<string> FindIndustrialTargets(CountryState country, WorldState world) => new();
    }

    public class AIStrategy
    {
        public AIPriority priority;
        public int threatLevel;
        public bool shouldMobilize;
        public bool shouldExpandArmy;
        public bool shouldBuildFactories;
    }

    public class AICampaign
    {
        public List<string> attackTargets = new();
        public List<string> defendTargets = new();
        public List<string> buildTargets = new();
    }

    public enum AIPriority { Defense, Expansion, Industrial, Diplomatic, Balanced }
}

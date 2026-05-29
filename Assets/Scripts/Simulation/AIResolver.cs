// ============================================================================
// Simulation/AIResolver.cs — AI 决策器
// B3: 注入 IConfigRegistry + ConstructionResolver，实现经济 AI
// ============================================================================

using System.Collections.Generic;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class AIResolver
    {
        private readonly IConfigRegistry _config;
        private readonly ConstructionResolver _construction;

        public AIResolver(IConfigRegistry config, ConstructionResolver construction)
        {
            _config = config;
            _construction = construction;
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

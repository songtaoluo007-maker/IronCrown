// ============================================================================
// Simulation/DiplomacyResolver.cs — 外交结算器
// Phase 5: GameState → WorldState
// ============================================================================

using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class DiplomacyResolver
    {
        public void ResolveDiplomacy(WorldState world)
        {
            // TODO: 遍历所有国家对，更新 opinion/threat
            // TODO: AI 外交决策
            // TODO: 国际紧张度自然衰减
        }

        public bool DeclareWar(CountryState aggressor, CountryState defender, WorldState world)
        {
            if (aggressor.warSupport < 50) return false;
            world.AddTension(20, $"{aggressor.name} 向 {defender.name} 宣战");
            // TODO: 更新 DiplomacyRelation 为 AtWar
            return true;
        }

        public bool FormAlliance(CountryState a, CountryState b, WorldState world) => true;
        public bool NonAggressionPact(CountryState a, CountryState b) => true;
        public bool TradeAgreement(CountryState a, CountryState b) => true;
        public bool RequestPeace(CountryState requester, CountryState enemy, WorldState world) => true;
    }
}

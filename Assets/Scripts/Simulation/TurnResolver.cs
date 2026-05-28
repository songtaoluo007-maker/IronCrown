// ============================================================================
// Simulation/TurnResolver.cs — 回合总调度器
// Phase 5: GameState → WorldState，注入 ITurnClock + IEventPublisher
// ============================================================================

using System.Linq;
using IronCrown.Domain;
using IronCrown.Contracts;

namespace IronCrown.Simulation
{
    public sealed class TurnResolver
    {
        private readonly ITurnClock _clock;
        private readonly IEventPublisher _events;
        private readonly EconomyResolver _economy;
        private readonly PoliticsResolver _politics;
        private readonly BattleResolver _battle;
        private readonly SupplyResolver _supply;
        private readonly AIResolver _ai;
        private readonly DiplomacyResolver _diplomacy;

        public TurnResolver(
            ITurnClock clock,
            IEventPublisher events,
            EconomyResolver economy,
            PoliticsResolver politics,
            BattleResolver battle,
            SupplyResolver supply,
            AIResolver ai,
            DiplomacyResolver diplomacy)
        {
            _clock = clock;
            _events = events;
            _economy = economy;
            _politics = politics;
            _battle = battle;
            _supply = supply;
            _ai = ai;
            _diplomacy = diplomacy;
        }

        public void ExecuteTurn(WorldState world)
        {
            _events.Publish(new TurnStartEvent { TurnNumber = _clock.CurrentTurn });
            ExecuteInternalAffairs(world);
            ExecuteMilitary(world);
            ExecuteDiplomacy(world);
            ExecuteSettlement(world);
            _events.Publish(new TurnEndEvent { TurnNumber = _clock.CurrentTurn });
        }

        private void ExecuteInternalAffairs(WorldState world)
        {
            foreach (var country in world.countries.Values.OrderBy(c => c.id, System.StringComparer.Ordinal))
            {
                _politics.ResolvePolitics(country, world);
                _economy.ResolveProduction(country, world);
            }
        }

        private void ExecuteMilitary(WorldState world)
        {
            foreach (var country in world.countries.Values.OrderBy(c => c.id, System.StringComparer.Ordinal))
            {
                _ai.MakeDecisions(country, world);
            }
            foreach (var country in world.countries.Values.OrderBy(c => c.id, System.StringComparer.Ordinal))
            {
                _supply.CheckSupply(country, world);
            }
        }

        private void ExecuteDiplomacy(WorldState world)
        {
            _diplomacy.ResolveDiplomacy(world);
        }

        private void ExecuteSettlement(WorldState world)
        {
            foreach (var country in world.countries.Values.OrderBy(c => c.id, System.StringComparer.Ordinal))
            {
                _economy.ResolveEconomy(country, world);
            }
        }
    }
}

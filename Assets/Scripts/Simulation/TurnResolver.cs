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
        private readonly ConstructionResolver _construction;
        private readonly UnitProductionResolver _unitProduction;
        private readonly MovementResolver _movement;
        private readonly VictoryConditionResolver _victory;
        private readonly IConfigRegistry _config;

        public TurnResolver(
            ITurnClock clock,
            IEventPublisher events,
            EconomyResolver economy,
            PoliticsResolver politics,
            BattleResolver battle,
            SupplyResolver supply,
            AIResolver ai,
            DiplomacyResolver diplomacy,
            ConstructionResolver construction,
            UnitProductionResolver unitProduction = null,
            MovementResolver movement = null,
            VictoryConditionResolver victory = null,
            IConfigRegistry config = null)
        {
            _clock = clock;
            _events = events;
            _economy = economy;
            _politics = politics;
            _battle = battle;
            _supply = supply;
            _ai = ai;
            _diplomacy = diplomacy;
            _construction = construction;
            _unitProduction = unitProduction;
            _movement = movement;
            _victory = victory;
            _config = config;
        }

        public void ExecuteTurn(WorldState world)
        {
            // 回合开始：重置所有部队移动力（早于经济结算）
            if (_movement != null)
                _movement.ResetMovement(world);

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

            // 造兵结算（EconomyResolver 之后、ConstructionResolver 之前）
            if (_unitProduction != null && _config != null)
            {
                var produced = _unitProduction.ResolveProduction(world, _config);
                foreach (var unit in produced)
                {
                    _events.Publish(new UnitProducedEvent
                    {
                        unitId = unit.id,
                        ownerCountry = unit.ownerCountry,
                        provinceId = unit.currentProvinceId,
                        unitType = unit.unitType
                    });
                }
            }

            foreach (var country in world.countries.Values.OrderBy(c => c.id, System.StringComparer.Ordinal))
            {
                _construction.ResolveConstruction(country);
            }

            // 战斗 tick（Settlement 尾段）
            _battle.TickBattles(world);

            // 胜负判定（TickBattles 之后）
            if (_victory != null)
                _victory.CheckVictory(world, _clock);
        }

    }
}

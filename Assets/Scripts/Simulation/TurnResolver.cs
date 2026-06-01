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
        private readonly WarTollResolver _warToll;
        private readonly OccupationResolver _occupation;
        private readonly AiPeaceOfferResolver _aiPeaceOffer;

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
            IConfigRegistry config = null,
            VictoryConditionResolver victory = null,
            WarTollResolver warToll = null,
            OccupationResolver occupation = null,
            AiPeaceOfferResolver aiPeaceOffer = null)
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
            _warToll = warToll;
            _occupation = occupation;
            _aiPeaceOffer = aiPeaceOffer;
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

            // C5: 战争代价（TickBattles 之后、胜负判定之前）
            if (_warToll != null && _config != null)
            {
                var eco = _config.Get<EconomyConfig>("global");
                if (eco != null)
                    _warToll.ApplyTurnToll(world, eco);
            }

            // C6: 占领抵抗（WarToll 之后、胜负判定之前）
            if (_occupation != null && _config != null)
            {
                var ecoOcc = _config.Get<EconomyConfig>("global");
                if (ecoOcc != null)
                    _occupation.ResolveOccupation(world, ecoOcc);
            }

            // C7: AI 主动求和（占领抵抗之后、胜负判定之前）
            if (_aiPeaceOffer != null && _config != null)
            {
                var ecoC7 = _config.Get<EconomyConfig>("global");
                if (ecoC7 != null)
                {
                    string playerId = world.countries.Values
                        .FirstOrDefault(c => c.id == "player" || c.id == world.countries.Keys.First())?.id;
                    _aiPeaceOffer.CheckAiPeaceOffers(world, ecoC7, playerId, world.turnNumber);
                }
            }

            // C14: 全局解围检查（补员之前）
            if (_supply != null)
                _supply.CheckGlobalRelief(world);

            // C13: 双条补员（Settlement 末尾，胜负判定之前）
            if (_supply != null && _config != null)
            {
                var ecoC13 = _config.Get<EconomyConfig>("global");
                if (ecoC13 != null)
                    _supply.ReplenishUnits(world, ecoC13, _config);
            }

            // C14: 清理补给耗尽的部队（cutoffTurns >= 4）
            if (_supply != null)
                CleanupStarvedUnits(world);

            // 胜负判定（TickBattles 之后）
            if (_victory != null)
                _victory.CheckVictory(world, _clock);
        }

        // =====================================================================
        // C14: 清理补给耗尽的部队
        // =====================================================================

        private void CleanupStarvedUnits(WorldState world)
        {
            var deadUnitIds = world.units.Values
                .Where(u => u.isCutoff && u.cutoffTurns >= 4)
                .Select(u => u.id)
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .ToList();

            foreach (var unitId in deadUnitIds)
            {
                if (!world.units.TryGetValue(unitId, out var unit)) continue;

                string provinceId = unit.currentProvinceId;
                string owner = unit.ownerCountry;

                world.units.Remove(unitId);
                if (world.countries.TryGetValue(owner, out var country))
                    country.unitIds.Remove(unitId);

                // 移除活动战斗中的引用
                foreach (var battle in world.activeBattles)
                {
                    battle.attackerUnitIds.Remove(unitId);
                    battle.defenderUnitIds.Remove(unitId);
                }

                _events.Publish(new Contracts.UnitDestroyedEvent
                {
                    unitId = unitId,
                    ownerCountry = owner,
                    provinceId = provinceId,
                    cause = "supply_starved"
                });
            }
        }

    }
}

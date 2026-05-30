// ============================================================================
// Simulation/VictoryConditionResolver.cs — 胜负终局判定
// Settlement 尾段 TickBattles 之后调用
// ============================================================================

using System;
using System.Linq;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class VictoryConditionResolver
    {
        private readonly IEventPublisher _events;

        public VictoryConditionResolver(IEventPublisher events)
        {
            _events = events;
        }

        public VictoryOutcome CheckVictory(WorldState world, ITurnClock clock)
        {
            if (clock.CurrentPhase == GamePhase.GameOver)
                return VictoryOutcome.None;

            if (string.IsNullOrEmpty(world.playerCountryId))
                return VictoryOutcome.None;

            // 失败：玩家首都被占
            if (world.countries.TryGetValue(world.playerCountryId, out var player) &&
                !string.IsNullOrEmpty(player.capitalProvinceId) &&
                world.provinces.TryGetValue(player.capitalProvinceId, out var playerCapital) &&
                playerCapital.controllerCountry != world.playerCountryId)
                return TriggerGameOver(world, "Defeat", null);

            // 胜利：所有非玩家国首都被玩家控制
            bool allCaptured = true;
            foreach (var c in world.countries.Values.OrderBy(c => c.id, StringComparer.Ordinal))
            {
                if (c.id == world.playerCountryId) continue;
                if (string.IsNullOrEmpty(c.capitalProvinceId)) { allCaptured = false; break; }
                if (!world.provinces.TryGetValue(c.capitalProvinceId, out var cap)) { allCaptured = false; break; }
                if (cap.controllerCountry != world.playerCountryId) { allCaptured = false; break; }
            }
            if (allCaptured && world.countries.Count > 1)
                return TriggerGameOver(world, "Victory", world.playerCountryId);

            return VictoryOutcome.None;
        }

        private VictoryOutcome TriggerGameOver(WorldState world, string result, string winnerCountryId)
        {
            world.gameOverResult = result;
            world.gameOverWinnerCountryId = winnerCountryId;
            _events.Publish(new GameOverEvent { result = result, winnerCountryId = winnerCountryId });
            return new VictoryOutcome { result = result, winnerCountryId = winnerCountryId };
        }
    }

    public struct VictoryOutcome
    {
        public string result;
        public string winnerCountryId;
        public static VictoryOutcome None => default;
    }
}

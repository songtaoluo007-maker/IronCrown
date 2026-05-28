// ============================================================================
// Application/Queries/ReadModelBuilder.cs — 只读模型构建器
// 纯映射，无副作用。将 Domain State 映射为 Contracts 只读 DTO。
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Application
{
    public sealed class ReadModelBuilder
    {
        public WorldView BuildWorldView(WorldState world, ITurnClock clock)
        {
            var countries = world.countries.Values
                .OrderBy(c => c.id, System.StringComparer.Ordinal)
                .Select(BuildCountryView)
                .ToList();

            return new WorldView
            {
                turn = clock.CurrentTurn,
                phase = clock.CurrentPhase.ToString(),
                worldTension = world.worldTension,
                countries = countries
            };
        }

        public CountryView BuildCountryView(CountryState c)
        {
            return new CountryView
            {
                id = c.id,
                name = c.name,
                ideology = c.ideology.ToString(),
                treasury = c.treasury,
                stability = c.stability,
                warSupport = c.warSupport,
                legitimacy = c.legitimacy,
                civilianFactories = c.civilianFactories,
                militaryFactories = c.militaryFactories,
                dockyards = c.dockyards,
                manpower = c.manpower,
                resources = new Dictionary<string, int>(c.resources)
            };
        }
    }
}

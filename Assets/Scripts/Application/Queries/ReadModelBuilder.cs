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
        public WorldView BuildWorldView(WorldState world, ITurnClock clock, string playerCountryId = null, string selectedProvinceId = null, IConfigRegistry config = null)
        {
            var countries = world.countries.Values
                .OrderBy(c => c.id, System.StringComparer.Ordinal)
                .Select(BuildCountryView)
                .ToList();

            // 构建 countryId→color 映射
            var colorMap = new Dictionary<string, string>();
            if (config != null)
            {
                foreach (var cc in config.All<CountryConfig>())
                {
                    if (!string.IsNullOrEmpty(cc.mapColor))
                        colorMap[cc.id] = cc.mapColor;
                }
            }

            // units 按 id 排序一次，避免 O(P*U) 无序遍历
            var sortedUnits = world.units.Values.OrderBy(u => u.id, System.StringComparer.Ordinal).ToList();

            var provinces = world.provinces.Values
                .OrderBy(p => p.id, System.StringComparer.Ordinal)
                .Select(p => BuildProvinceView(p, colorMap, sortedUnits))
                .ToList();

            return new WorldView
            {
                turn = clock.CurrentTurn,
                phase = clock.CurrentPhase.ToString(),
                worldTension = world.worldTension,
                playerCountryId = playerCountryId,
                selectedProvinceId = selectedProvinceId,
                countries = countries,
                provinces = provinces
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
                equipmentStockpile = c.equipmentStockpile,
                resources = new Dictionary<string, int>(c.resources),
                constructionQueueCount = c.constructionQueue.Count,
                unitProductionQueueCount = c.unitProductionQueue.Count,
                taxLevel = c.taxLevel,
                civilLevel = c.civilLevel
            };
        }

        public ProvinceView BuildProvinceView(ProvinceState p, Dictionary<string, string> colorMap, List<UnitState> sortedUnits = null)
        {
            string ownerColor = "#808080";
            if (p.ownerCountry != null && colorMap.TryGetValue(p.ownerCountry, out var color))
                ownerColor = color;

            int garrisonCount = 0;
            if (sortedUnits != null)
            {
                foreach (var u in sortedUnits)
                {
                    if (u.currentProvinceId == p.id)
                        garrisonCount++;
                }
            }

            return new ProvinceView
            {
                id = p.id,
                name = p.name,
                ownerCountry = p.ownerCountry,
                ownerColor = ownerColor,
                terrain = p.terrain.ToString(),
                gridX = p.gridX,
                gridY = p.gridY,
                infrastructure = p.infrastructure,
                population = p.population,
                victoryPoint = p.victoryPoint,
                isCapital = p.isCapital,
                resourceOutput = p.resourceOutput,
                neighbors = p.neighbors ?? System.Array.Empty<string>(),
                garrisonCount = garrisonCount
            };
        }
    }
}

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

            // 构建战斗中 unitId/provinceId 集合（在 province 映射之前）
            var battleUnitIds = new HashSet<string>();
            var battleProvinceIds = new HashSet<string>();
            foreach (var b in world.activeBattles)
            {
                battleUnitIds.Add(b.attackerUnitId);
                battleUnitIds.Add(b.defenderUnitId);
                battleProvinceIds.Add(b.provinceId);
            }

            var provinces = world.provinces.Values
                .OrderBy(p => p.id, System.StringComparer.Ordinal)
                .Select(p => BuildProvinceView(p, colorMap, sortedUnits, battleProvinceIds))
                .ToList();

            var units = world.units.Values
                .OrderBy(u => u.id, System.StringComparer.Ordinal)
                .Select(u => BuildUnitView(u, battleUnitIds))
                .ToList();

            var activeBattles = world.activeBattles
                .OrderBy(b => b.id, System.StringComparer.Ordinal)
                .Select(b => BuildActiveBattleView(b, world.units))
                .ToList();

            return new WorldView
            {
                turn = clock.CurrentTurn,
                phase = clock.CurrentPhase.ToString(),
                worldTension = world.worldTension,
                playerCountryId = playerCountryId,
                selectedProvinceId = selectedProvinceId,
                selectedUnitId = world.selectedUnitId,
                countries = countries,
                provinces = provinces,
                units = units,
                activeBattles = activeBattles,
                warRelations = world.warRelations
                    .OrderBy(w => w.countryA, System.StringComparer.Ordinal)
                    .ThenBy(w => w.countryB, System.StringComparer.Ordinal)
                    .Select(BuildWarRelationView)
                    .ToList(),
                gameOverResult = world.gameOverResult,
                gameOverWinnerCountryId = world.gameOverWinnerCountryId
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
                civilLevel = c.civilLevel,
                warExhaustion = c.warExhaustion,
                peaceOfferCooldown = c.peaceOfferCooldown,
                pendingPeaceOfferFrom = c.pendingPeaceOfferFrom,
                    pendingPeaceOfferExpiry = c.pendingPeaceOfferExpiry
            };
        }

        public ProvinceView BuildProvinceView(ProvinceState p, Dictionary<string, string> colorMap, List<UnitState> sortedUnits = null, HashSet<string> battleProvinceIds = null)
        {
            // 按 controllerCountry 取色（占领后立即变色）
            string displayCountry = p.controllerCountry ?? p.ownerCountry;
            string ownerColor = "#808080";
            if (displayCountry != null && colorMap.TryGetValue(displayCountry, out var color))
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

            // 收集驻军 unitId 列表
            string[] garrisonUnitIds = System.Array.Empty<string>();
            if (sortedUnits != null)
            {
                garrisonUnitIds = sortedUnits
                    .Where(u => u.currentProvinceId == p.id)
                    .Select(u => u.id)
                    .ToArray();
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
                garrisonCount = garrisonCount,
                garrisonUnitIds = garrisonUnitIds,
                controllerCountry = p.controllerCountry,
                isOccupied = p.controllerCountry != null && p.controllerCountry != p.ownerCountry,
                hasActiveBattle = battleProvinceIds != null && battleProvinceIds.Contains(p.id),
                  resistance = p.resistance
            };
        }

        public UnitView BuildUnitView(UnitState u, HashSet<string> battleUnitIds = null)
        {
            return new UnitView
            {
                id = u.id,
                unitType = u.unitType,
                ownerCountry = u.ownerCountry,
                currentProvinceId = u.currentProvinceId,
                manpower = u.manpower,
                maxManpower = u.maxManpower,
                organization = u.organization,
                maxOrganization = u.maxOrganization,
                movesLeft = u.movesLeft,
                speed = u.speed,
                isInBattle = battleUnitIds != null && battleUnitIds.Contains(u.id)
            };
        }

        public ActiveBattleView BuildActiveBattleView(ActiveBattle b, Dictionary<string, UnitState> units)
        {
            int atkOrg = 0, atkMaxOrg = 0, defOrg = 0, defMaxOrg = 0;
            if (units.TryGetValue(b.attackerUnitId, out var atk))
            {
                atkOrg = atk.organization;
                atkMaxOrg = atk.maxOrganization;
            }
            if (units.TryGetValue(b.defenderUnitId, out var def))
            {
                defOrg = def.organization;
                defMaxOrg = def.maxOrganization;
            }

            return new ActiveBattleView
            {
                id = b.id,
                attackerUnitId = b.attackerUnitId,
                defenderUnitId = b.defenderUnitId,
                provinceId = b.provinceId,
                turnsElapsed = b.turnsElapsed,
                attackerOrg = atkOrg,
                attackerMaxOrg = atkMaxOrg,
                defenderOrg = defOrg,
                defenderMaxOrg = defMaxOrg
            };
        }

        public WarRelationView BuildWarRelationView(WarRelation w)
        {
            return new WarRelationView
            {
                countryA = w.countryA,
                countryB = w.countryB,
                startTurn = w.startTurn
            };
        }
    }
}

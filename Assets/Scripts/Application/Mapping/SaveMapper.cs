// ============================================================================
// Application/Mapping/SaveMapper.cs — 存档 DTO ↔ 运行时 State 映射
// ============================================================================

using System;
using System.Linq;
using IronCrown.Domain;

namespace IronCrown.Application
{
    public static class SaveMapper
    {
        public static GameState ToSave(WorldState world, int seed, ulong rngState, GamePhase phase)
        {
            var state = new GameState
            {
                turnNumber = world.turnNumber,
                seed = seed,
                rngState = rngState,
                phase = phase.ToString(),
                saveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                countries = world.countries.Values.Select(c => new CountrySaveData
                {
                    id = c.id,
                    name = c.name,
                    treasury = c.treasury,
                    stability = c.stability,
                    warSupport = c.warSupport,
                    equipmentStockpile = c.equipmentStockpile,
                    taxLevel = c.taxLevel,
                    civilLevel = c.civilLevel,
                    civilianFactories = c.civilianFactories,
                    militaryFactories = c.militaryFactories,
                    dockyards = c.dockyards,
                    manpower = c.manpower,
                    totalManpower = c.totalManpower,
                    resources = c.resources.Select(r => new ResourceEntry { key = r.Key, value = r.Value }).ToArray(),
                    constructionQueue = c.constructionQueue.Select(q => new ConstructionOrderSaveData
                    {
                        factoryKind = q.factoryKind,
                        turnsRemaining = q.turnsRemaining
                    }).ToArray(),
                    activePolicies = c.activePolicies.ToArray(),
                    completedTechs = c.completedTechs.ToArray()
                }).ToArray(),
                provinces = world.provinces.Values.Select(p => new ProvinceSaveData
                {
                    id = p.id,
                    name = p.name,
                    ownerCountry = p.ownerCountry,
                    controllerCountry = p.controllerCountry,
                    population = p.population,
                    manpower = p.manpower,
                    infrastructure = p.infrastructure,
                    railwayLevel = p.railwayLevel,
                    portLevel = p.portLevel,
                    airBaseLevel = p.airBaseLevel,
                    industrySlots = p.industrySlots,
                    builtCivilianFactories = p.builtCivilianFactories,
                    builtMilitaryFactories = p.builtMilitaryFactories,
                    resourceOutput = p.resourceOutput,
                    resistance = p.resistance,
                    compliance = p.compliance,
                    victoryPoint = p.victoryPoint,
                    isCapital = p.isCapital,
                    gridX = p.gridX,
                    gridY = p.gridY,
                    terrain = p.terrain.ToString(),
                    neighbors = p.neighbors
                }).ToArray(),
                units = world.units.Values.Select(u => new UnitSaveData
                {
                    id = u.id,
                    unitType = u.unitType,
                    ownerCountry = u.ownerCountry,
                    currentProvince = u.currentProvinceId,
                    manpower = u.manpower,
                    equipment = u.equipment,
                    organization = u.organization
                }).ToArray()
            };
            return state;
        }

        public static WorldState ToRuntime(GameState save)
        {
            var world = new WorldState
            {
                turnNumber = save.turnNumber,
                worldTension = 0,
                playerCountryId = save.playerCountryId
            };

            if (save.countries != null)
            {
                foreach (var cd in save.countries)
                {
                    var c = new CountryState
                    {
                        id = cd.id,
                        name = cd.name,
                        treasury = cd.treasury,
                        stability = cd.stability,
                        warSupport = cd.warSupport,
                        equipmentStockpile = cd.equipmentStockpile,
                        civilianFactories = cd.civilianFactories,
                        militaryFactories = cd.militaryFactories,
                        dockyards = cd.dockyards,
                        manpower = cd.manpower,
                        totalManpower = cd.totalManpower,
                        taxLevel = cd.taxLevel,
                        civilLevel = cd.civilLevel
                    };
                    if (cd.resources != null)
                        foreach (var r in cd.resources)
                            c.resources[r.key] = r.value;
                    if (cd.constructionQueue != null)
                        foreach (var q in cd.constructionQueue)
                            c.constructionQueue.Add(new ConstructionOrder { factoryKind = q.factoryKind, turnsRemaining = q.turnsRemaining });
                    if (cd.activePolicies != null)
                        foreach (var p in cd.activePolicies) c.activePolicies.Add(p);
                    if (cd.completedTechs != null)
                        foreach (var t in cd.completedTechs) c.completedTechs.Add(t);
                    world.countries[c.id] = c;
                }
            }

            if (save.provinces != null)
            {
                foreach (var pd in save.provinces)
                {
                    var p = new ProvinceState
                    {
                        id = pd.id,
                        name = pd.name,
                        ownerCountry = pd.ownerCountry,
                        controllerCountry = pd.controllerCountry,
                        population = pd.population,
                        manpower = pd.manpower,
                        infrastructure = pd.infrastructure,
                        railwayLevel = pd.railwayLevel,
                        portLevel = pd.portLevel,
                        airBaseLevel = pd.airBaseLevel,
                        industrySlots = pd.industrySlots,
                        builtCivilianFactories = pd.builtCivilianFactories,
                        builtMilitaryFactories = pd.builtMilitaryFactories,
                        resourceOutput = pd.resourceOutput,
                        resistance = pd.resistance,
                        compliance = pd.compliance,
                        victoryPoint = pd.victoryPoint,
                        isCapital = pd.isCapital,
                        gridX = pd.gridX,
                        gridY = pd.gridY,
                        terrain = string.IsNullOrEmpty(pd.terrain) ? TerrainType.Plain : Enum.Parse<TerrainType>(pd.terrain),
                        neighbors = pd.neighbors ?? Array.Empty<string>()
                    };
                    world.provinces[p.id] = p;
                }
            }

            if (save.units != null)
            {
                foreach (var ud in save.units)
                {
                    var u = new UnitState
                    {
                        id = ud.id,
                        unitType = ud.unitType,
                        ownerCountry = ud.ownerCountry,
                        currentProvinceId = ud.currentProvince,
                        manpower = ud.manpower,
                        equipment = ud.equipment,
                        organization = ud.organization
                    };
                    world.units[u.id] = u;
                }
            }

            return world;
        }
    }
}

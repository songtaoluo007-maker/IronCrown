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
                    warExhaustion = c.warExhaustion,
                    peaceOfferCooldown = c.peaceOfferCooldown,
                    pendingPeaceOfferFrom = c.pendingPeaceOfferFrom,
                    pendingPeaceOfferExpiry = c.pendingPeaceOfferExpiry,
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
                    unitProductionQueue = c.unitProductionQueue.Select(q => new UnitProductionOrderSaveData
                    {
                        unitType = q.unitType,
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
                    organization = u.organization,
                    maxManpower = u.maxManpower,
                    maxEquipment = u.maxEquipment,
                    maxOrganization = u.maxOrganization,
                    morale = u.morale,
                    experience = u.experience,
                    baseAttack = u.baseAttack,
                    baseDefense = u.baseDefense,
                    baseBreakthrough = u.baseBreakthrough,
                    armor = u.armor,
                    piercing = u.piercing,
                    speed = u.speed,
                    movesLeft = u.movesLeft,
                    supplyConsumption = u.supplyConsumption
                }).ToArray()
            };
            state.playerCountryId = world.playerCountryId;
            state.selectedUnitId = world.selectedUnitId;

            // 活动战斗
            state.activeBattles = world.activeBattles.Select(b => new ActiveBattleSaveData
            {
                id = b.id,
                attackerUnitId = b.attackerUnitId,
                defenderUnitId = b.defenderUnitId,
                provinceId = b.provinceId,
                turnsElapsed = b.turnsElapsed
            }).ToArray();

            // 战争关系
            state.warRelations = world.warRelations.Select(w => new WarRelationSaveData
            {
                countryA = w.countryA,
                countryB = w.countryB,
                startTurn = w.startTurn
            }).ToArray();

            // 停战和平期 (C9d)
            state.truces = world.truceUntilTurn.Select(t => new TruceEntry { key = t.Key, untilTurn = t.Value }).ToArray();

            // 游戏终局
            state.gameOverResult = world.gameOverResult;
            state.gameOverWinnerCountryId = world.gameOverWinnerCountryId;

            return state;
        }

        public static WorldState ToRuntime(GameState save)
        {
            var world = new WorldState
            {
                turnNumber = save.turnNumber,
                worldTension = 0,
                playerCountryId = save.playerCountryId,
                selectedUnitId = save.selectedUnitId
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
                        civilLevel = cd.civilLevel,
                        warExhaustion = cd.warExhaustion,
                        peaceOfferCooldown = cd.peaceOfferCooldown,
                        pendingPeaceOfferFrom = cd.pendingPeaceOfferFrom
                    };
                    if (cd.resources != null)
                        foreach (var r in cd.resources)
                            c.resources[r.key] = r.value;
                    if (cd.constructionQueue != null)
                        foreach (var q in cd.constructionQueue)
                            c.constructionQueue.Add(new ConstructionOrder { factoryKind = q.factoryKind, turnsRemaining = q.turnsRemaining });
                    if (cd.unitProductionQueue != null)
                        foreach (var q in cd.unitProductionQueue)
                            c.unitProductionQueue.Add(new UnitProductionOrder { unitType = q.unitType, turnsRemaining = q.turnsRemaining });
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
                        organization = ud.organization,
                        maxManpower = ud.maxManpower,
                        maxEquipment = ud.maxEquipment,
                        maxOrganization = ud.maxOrganization,
                        morale = ud.morale,
                        experience = ud.experience,
                        baseAttack = ud.baseAttack,
                        baseDefense = ud.baseDefense,
                        baseBreakthrough = ud.baseBreakthrough,
                        armor = ud.armor,
                        piercing = ud.piercing,
                        speed = ud.speed,
                        movesLeft = ud.movesLeft,
                        supplyConsumption = ud.supplyConsumption
                    };
                    world.units[u.id] = u;
                }
            }

            // 重建 country.unitIds（不读存档，从 units 按 owner 分组重建）
            foreach (var c in world.countries.Values)
                c.unitIds.Clear();
            foreach (var u in world.units.Values.OrderBy(u => u.id, System.StringComparer.Ordinal))
            {
                if (world.countries.TryGetValue(u.ownerCountry, out var owner))
                    owner.unitIds.Add(u.id);
            }

            // 活动战斗
            if (save.activeBattles != null)
            {
                foreach (var bd in save.activeBattles)
                {
                    world.activeBattles.Add(new ActiveBattle
                    {
                        id = bd.id,
                        attackerUnitId = bd.attackerUnitId,
                        defenderUnitId = bd.defenderUnitId,
                        provinceId = bd.provinceId,
                        turnsElapsed = bd.turnsElapsed
                    });
                }
            }

            // 战争关系
            if (save.warRelations != null)
            {
                foreach (var wd in save.warRelations)
                {
                    world.warRelations.Add(new WarRelation
                    {
                        countryA = wd.countryA,
                        countryB = wd.countryB,
                        startTurn = wd.startTurn
                    });
                }
            }

            // 停战和平期 (C9d)
            if (save.truces != null)
            {
                foreach (var t in save.truces)
                    world.truceUntilTurn[t.key] = t.untilTurn;
            }

            // 游戏终局
            world.gameOverResult = save.gameOverResult;
            world.gameOverWinnerCountryId = save.gameOverWinnerCountryId;

            return world;
        }
    }
}

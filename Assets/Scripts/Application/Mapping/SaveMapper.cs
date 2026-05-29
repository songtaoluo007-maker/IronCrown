// ============================================================================
// Application/Mapping/SaveMapper.cs — 存档 DTO ↔ 运行时 State 映射
// ============================================================================

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
                    treasury = c.treasury,
                    stability = c.stability,
                    warSupport = c.warSupport,
                    equipmentStockpile = c.equipmentStockpile,
                    activePolicies = c.activePolicies.ToArray(),
                    completedTechs = c.completedTechs.ToArray()
                }).ToArray(),
                provinces = world.provinces.Values.Select(p => new ProvinceSaveData
                {
                    id = p.id,
                    ownerCountry = p.ownerCountry,
                    controllerCountry = p.controllerCountry,
                    resistance = p.resistance,
                    compliance = p.compliance
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
                worldTension = 0
            };

            if (save.countries != null)
            {
                foreach (var cd in save.countries)
                {
                    var c = new CountryState
                    {
                        id = cd.id,
                        treasury = cd.treasury,
                        stability = cd.stability,
                        warSupport = cd.warSupport,
                        equipmentStockpile = cd.equipmentStockpile
                    };
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
                        ownerCountry = pd.ownerCountry,
                        controllerCountry = pd.controllerCountry,
                        resistance = pd.resistance,
                        compliance = pd.compliance
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

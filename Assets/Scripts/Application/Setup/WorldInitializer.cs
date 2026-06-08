// ============================================================================
// Application/Setup/WorldInitializer.cs — 从配置创建新游戏世界
// C11: 初始部队改用师模板
// ============================================================================

using System;
using System.Collections.Generic;
using IronCrown.Domain;

namespace IronCrown.Application
{
    /// <summary>世界初始化器 — 从配置表构建运行时 WorldState</summary>
    public sealed class WorldInitializer
    {
        private readonly IAppLogger _logger;

        public WorldInitializer(IAppLogger logger)
        {
            _logger = logger;
        }

        /// <summary>根据配置创建新游戏世界</summary>
        public WorldState CreateNewGame(IConfigRegistry config)
        {
            var world = new WorldState
            {
                worldTension = 10,
                turnNumber = 1
            };

            // 加载国家
            var countryConfigs = config.All<CountryConfig>();
            foreach (var cfg in countryConfigs)
            {
                var state = new CountryState
                {
                    id = cfg.id,
                    name = cfg.name,
                    capitalProvinceId = cfg.capitalProvinceId,
                    ideology = Enum.Parse<Ideology>(cfg.ideology),
                    stability = cfg.stability,
                    warSupport = cfg.warSupport,
                    legitimacy = cfg.legitimacy,
                    corruption = cfg.corruption,
                    bureaucracy = cfg.bureaucracy,
                    treasury = cfg.treasury,
                    taxIncome = cfg.taxIncome,
                    tradeIncome = cfg.tradeIncome,
                    militaryExpense = cfg.militaryExpense,
                    civilExpense = cfg.civilExpense,
                    civilianFactories = cfg.civilianFactories,
                    militaryFactories = cfg.militaryFactories,
                    dockyards = cfg.dockyards,
                    manpower = cfg.manpower,
                    totalManpower = cfg.totalManpower,
                    resources = new Dictionary<string, int>(cfg.resources)
                };
                // C16: 玩家国初始 10 张抽卡券
                if (cfg.id == "empire_north")
                    state.gachaTickets = 10;
                world.countries[cfg.id] = state;
            }

            // 加载省份
            var provinceConfigs = config.All<ProvinceConfig>();
            foreach (var cfg in provinceConfigs)
            {
                var state = new ProvinceState
                {
                    id = cfg.id,
                    name = cfg.name,
                    terrain = Enum.Parse<TerrainType>(cfg.terrain),
                    ownerCountry = cfg.ownerCountry,
                    controllerCountry = cfg.ownerCountry,
                    isCapital = cfg.isCapital,
                    population = cfg.population,
                    manpower = cfg.manpower,
                    infrastructure = cfg.infrastructure,
                    railwayLevel = cfg.railwayLevel,
                    portLevel = cfg.portLevel,
                    airBaseLevel = cfg.airBaseLevel,
                    industrySlots = cfg.industrySlots,
                    resourceOutput = cfg.resourceOutput ?? Array.Empty<string>(),
                    victoryPoint = cfg.victoryPoint,
                    gridX = cfg.gridX,
                    gridY = cfg.gridY,
                    neighbors = cfg.neighbors ?? Array.Empty<string>()
                };
                world.provinces[cfg.id] = state;

                // P2.2+P2.4: 生成 4 格 (2×2 子网格),省内地形差异化
                // 默认全部继承省地形;可通过扩展 ProvinceConfig 定义每格地形
                var tileTerrains = GetTileTerrains(cfg);
                for (int n = 0; n < 4; n++)
                {
                    string tileId = $"{cfg.id}_t{n}";
                    int tx = cfg.gridX * 2 + (n % 2);
                    int ty = cfg.gridY * 2 + (n / 2);
                    world.tiles[tileId] = new TileState
                    {
                        id = tileId,
                        gridX = tx,
                        gridY = ty,
                        terrain = tileTerrains[n],
                        provinceId = cfg.id
                    };
                    state.tileIds.Add(tileId);
                }
            }

            // C11: 初始部队 — 每国 1 个基础步兵师驻首都
            var basicDivTemplate = config.Get<DivisionTemplate>("infantry_division_basic");
            if (basicDivTemplate != null)
            {
                foreach (var country in world.countries.Values)
                {
                    string unitId = $"{country.id}_div_1";
                    var unit = UnitFactory.CreateFromDivisionTemplate(unitId, basicDivTemplate, country.id, country.capitalProvinceId, config);
                    world.units[unitId] = unit;
                    country.unitIds.Add(unitId);
                }
            }
            else
            {
                // Fallback: 无 DivisionTemplate 时（旧配置/测试），用 UnitConfig 创建单旅师
                var infConfig = config.Get<UnitConfig>("infantry");
                foreach (var country in world.countries.Values)
                {
                    string unitId = $"{country.id}_inf_1";
                    var unit = new UnitState
                    {
                        id = unitId,
                        unitType = "infantry",
                        ownerCountry = country.id,
                        currentProvinceId = country.capitalProvinceId,
                        manpower = infConfig?.hp ?? 100,
                        equipment = infConfig?.hp ?? 100,
                        organization = infConfig?.organization ?? 60,
                        maxManpower = infConfig?.hp ?? 100,
                        maxEquipment = infConfig?.hp ?? 100,
                        maxOrganization = infConfig?.organization ?? 60,
                        baseAttack = infConfig?.attack ?? 10,
                        baseDefense = infConfig?.defense ?? 15,
                        baseBreakthrough = infConfig?.breakthrough ?? 5,
                        armor = infConfig?.armor ?? 0,
                        piercing = infConfig?.piercing ?? 5,
                        speed = infConfig?.speed ?? 3,
                        movesLeft = infConfig?.speed ?? 3,
                        supplyConsumption = infConfig?.supplyConsumption ?? 10
                    };
                    // 旧模式：单旅退化
                    unit.brigades.Add(new BrigadeState
                    {
                        brigadeType = "infantry",
                        count = 1,
                        manpower = infConfig?.hp ?? 100,
                        equipment = infConfig?.hp ?? 100
                    });
                    world.units[unitId] = unit;
                    country.unitIds.Add(unitId);
                }
            }

            // C11: 每国初始装备库存 500（师级消耗更大）
            foreach (var country in world.countries.Values)
            {
                country.equipmentStockpile = 500;
            }

            _logger.Info($"[WorldInitializer] 初始化完成: {world.countries.Count} 个国家, {world.provinces.Count} 个省份, {world.units.Count} 支部队");
            return world;
        }

        /// <summary>P2.4: 获取省内地形差异化配置（默认全继承省地形）</summary>
        private static TerrainType[] GetTileTerrains(ProvinceConfig cfg)
        {
            // P2.4: 山地/丘陵省给 1 格不同地形增加视觉层次
            var baseTerrain = Enum.Parse<TerrainType>(cfg.terrain);
            var result = new TerrainType[4];
            for (int i = 0; i < 4; i++)
                result[i] = baseTerrain;

            // 示例差异化: Mountain 省给 1 格 Hills;Forest 省给 1 格 Plain
            // (可通过扩展 ProvinceConfig.tileTerrains 覆盖)
            if (baseTerrain == TerrainType.Mountain)
                result[3] = TerrainType.Hills; // 右下角格为丘陵
            else if (baseTerrain == TerrainType.Forest)
                result[0] = TerrainType.Plain; // 左上角格为平原
            else if (baseTerrain == TerrainType.Urban)
                result[2] = TerrainType.Plain; // 城市省有 1 格平原

            return result;
        }
    }
}

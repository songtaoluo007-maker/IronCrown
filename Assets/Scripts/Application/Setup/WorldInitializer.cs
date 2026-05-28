// ============================================================================
// Application/Setup/WorldInitializer.cs — 从配置创建新游戏世界
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
                    victoryPoint = cfg.victoryPoint
                };
                world.provinces[cfg.id] = state;
            }

            _logger.Info($"[WorldInitializer] 初始化完成: {world.countries.Count} 个国家, {world.provinces.Count} 个省份");
            return world;
        }
    }
}

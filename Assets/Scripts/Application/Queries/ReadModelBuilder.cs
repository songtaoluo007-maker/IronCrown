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
                foreach (var uid in b.attackerUnitIds) battleUnitIds.Add(uid);
                foreach (var uid in b.defenderUnitIds) battleUnitIds.Add(uid);
                battleProvinceIds.Add(b.provinceId);
            }

            var provinces = world.provinces.Values
                .OrderBy(p => p.id, System.StringComparer.Ordinal)
                .Select(p => BuildProvinceView(p, colorMap, sortedUnits, battleProvinceIds))
                .ToList();

            var units = world.units.Values
                .OrderBy(u => u.id, System.StringComparer.Ordinal)
                .Select(u => BuildUnitView(u, battleUnitIds, config, world.commanders))
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
                // C15a: 将领列表
                commanders = world.commanders.Values
                    .OrderBy(c => c.id, System.StringComparer.Ordinal)
                    .Select(BuildCommanderView)
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
                unitCount = c.unitIds.Count,
                taxLevel = c.taxLevel,
                civilLevel = c.civilLevel,
                warExhaustion = c.warExhaustion,
                peaceOfferCooldown = c.peaceOfferCooldown,
                pendingPeaceOfferFrom = c.pendingPeaceOfferFrom,
                    pendingPeaceOfferExpiry = c.pendingPeaceOfferExpiry,
                    gachaTickets = c.gachaTickets, // P2.1: 语义改为战功点
                    gachaPityCounter = c.gachaPityCounter // [deprecated P2.1]
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
                  resistance = p.resistance,
                  supplyCapacity = p.CalculateSupplyCapacity()
            };
        }

        public UnitView BuildUnitView(UnitState u, HashSet<string> battleUnitIds = null, IConfigRegistry config = null, Dictionary<string, CommanderState> commanders = null)
        {
            // C11: 生成旅摘要
            string brigadeSummary = "";
            if (u.brigades != null && u.brigades.Count > 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var b in u.brigades)
                {
                    var cfg = config?.Get<UnitConfig>(b.brigadeType);
                    string name = cfg?.name ?? b.brigadeType;
                    parts.Add($"{b.count} {name}");
                }
                brigadeSummary = string.Join(" + ", parts);
            }

            // C11: 师模板名
            string divTemplateName = "";
            if (!string.IsNullOrEmpty(u.divisionTemplateId) && config != null)
            {
                var divT = config.Get<DivisionTemplate>(u.divisionTemplateId);
                divTemplateName = divT?.name ?? u.divisionTemplateId;
            }

            return new UnitView
            {
                id = u.id,
                unitType = u.unitType,
                divisionTemplateName = divTemplateName,
                brigadeSummary = brigadeSummary,
                ownerCountry = u.ownerCountry,
                currentProvinceId = u.currentProvinceId,
                manpower = u.manpower,
                maxManpower = u.maxManpower,
                equipment = u.equipment,
                maxEquipment = u.maxEquipment,
                organization = u.organization,
                maxOrganization = u.maxOrganization,
                movesLeft = u.movesLeft,
                speed = u.speed,
                tacticalExp = u.tacticalExp,
                tacticalLevel = u.tacticalExp / 25,  // 0-4
                recoveryTurnsLeft = u.recoveryTurnsLeft,
                isRecovering = u.recoveryTurnsLeft > 0,
                isInBattle = battleUnitIds != null && battleUnitIds.Contains(u.id),
                isCutoff = u.isCutoff,
                cutoffTurns = u.cutoffTurns,
                isDisorganized = u.isDisorganized,
                morale = u.morale,
                // C15a: 将领信息
                commanderId = u.commanderId,
                commanderName = (commanders != null && !string.IsNullOrEmpty(u.commanderId) && commanders.TryGetValue(u.commanderId, out var cmdr)) ? cmdr.name : null,
                commanderRank = (commanders != null && !string.IsNullOrEmpty(u.commanderId) && commanders.TryGetValue(u.commanderId, out var cmdr2)) ? cmdr2.RankName : null
            };
        }

        public ActiveBattleView BuildActiveBattleView(ActiveBattle b, Dictionary<string, UnitState> units)
        {
            int atkOrg = 0, atkMaxOrg = 0, defOrg = 0, defMaxOrg = 0;
            foreach (var uid in b.attackerUnitIds)
            {
                if (units.TryGetValue(uid, out var atk))
                {
                    atkOrg += atk.organization;
                    atkMaxOrg += atk.maxOrganization;
                }
            }
            foreach (var uid in b.defenderUnitIds)
            {
                if (units.TryGetValue(uid, out var def))
                {
                    defOrg += def.organization;
                    defMaxOrg += def.maxOrganization;
                }
            }

            return new ActiveBattleView
            {
                id = b.id,
                attackerUnitIds = b.attackerUnitIds,
                defenderUnitIds = b.defenderUnitIds,
                provinceId = b.provinceId,
                attackerOwnerCountry = b.attackerOwnerCountry,
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

        // === C15a: 将领视图 ===
        public CommanderView BuildCommanderView(CommanderState c)
        {
            return new CommanderView
            {
                id = c.id,
                name = c.name,
                ownerCountry = c.ownerCountry,
                rank = c.rank,
                rankName = c.RankName,
                victories = c.victories,
                encirclements = c.encirclements,
                baseAttack = c.baseAttack,
                baseDefense = c.baseDefense,
                rankAttackBonusPct = c.RankAttackBonusPct,
                rankDefenseBonusPct = c.RankDefenseBonusPct,
                maxDivisions = c.maxDivisions,
                commandedDivisions = 0, // 由 HUD 层计算
                isActive = c.isActive,
                canPromote = c.CanPromote,
                buffDescription = c.GetBuffDescription(),
                starLevel = c.starLevel
            };
        }
    }
}

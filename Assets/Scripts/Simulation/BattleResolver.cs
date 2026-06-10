// ============================================================================
// Simulation/BattleResolver.cs — 战斗结算器（C12 整数化 + 旅级战损）
// C9c: 多兵种联合战斗
// C12: float→int 整数化 + 旅级战损分摊
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using IronCrown.Domain;
using IronCrown.Contracts;

namespace IronCrown.Simulation
{
    public sealed class BattleResolver
    {
        private readonly IRandom _rng;
        private readonly IEventPublisher _events;
        private readonly IConfigRegistry _config;
        private readonly CommanderResolver _commander; // C15a

        public BattleResolver(IRandom rng, IEventPublisher events, IConfigRegistry config = null, CommanderResolver commander = null)
        {
            _rng = rng;
            _events = events;
            _config = config;
            _commander = commander;
        }

        // =====================================================================
        // C12: 整数化战斗核心
        // =====================================================================

        /// <summary>单师攻方战力（int × 100 量级，含战役等级 + 将军 buff）</summary>
        private int SingleUnitAttackPower(UnitState unit, EconomyConfig eco = null, WorldState world = null)
        {
            int orgPct = unit.organization * 100 / Math.Max(1, unit.maxOrganization);
            int expBonus = 100 + unit.experience * 10;
            int basePower = unit.baseAttack * orgPct * expBonus / 10000;

            // C13: 战役等级加成
            if (eco != null)
            {
                int level = Math.Min(4, unit.tacticalExp / Math.Max(1, eco.tacticalExpLevelStep));
                int levelBonus = 100 + level * eco.tacticalExpAttackBonusPerLevel;
                basePower = basePower * levelBonus / 100;
            }

            // C15a: 将军军衔攻击 buff（与战役等级相乘）
            if (_commander != null && world != null && !string.IsNullOrEmpty(unit.commanderId))
            {
                int cmdrBuffPct = _commander.GetCommanderAttackBuffPct(world, unit.commanderId);
                basePower = basePower * cmdrBuffPct / 100;
            }

            // C15b: 将军卡技能攻击 buff（在军衔之后、最终缩放前）
            if (_commander != null && world != null && !string.IsNullOrEmpty(unit.commanderId) && _config != null)
            {
                var cmdr = world.commanders.TryGetValue(unit.commanderId, out var c) ? c : null;
                if (cmdr != null)
                {
                    int cardAtkPct = CommanderSkillEvaluator.EvalAttack(_config, cmdr, unit, null, world);
                    basePower = basePower * cardAtkPct / 100;

                    // C16: 星级加成（每星 +5%）
                    var gachaEco = _config.Get<EconomyConfig>("global");
                    if (gachaEco != null && cmdr.starLevel > 0)
                    {
                        int starPct = 100 + cmdr.starLevel * gachaEco.starBonusPerStar;
                        basePower = basePower * starPct / 100;
                    }
                }
            }

            return basePower;
        }

        /// <summary>单师守方战力（int × 100 量级，含战役等级 + 将军 buff）</summary>
        private int SingleUnitDefensePower(UnitState unit, EconomyConfig eco = null, WorldState world = null)
        {
            int orgPct = unit.organization * 100 / Math.Max(1, unit.maxOrganization);
            int expBonus = 100 + unit.experience * 10;
            int basePower = unit.baseDefense * orgPct * expBonus / 10000;

            // C13: 战役等级加成
            if (eco != null)
            {
                int level = Math.Min(4, unit.tacticalExp / Math.Max(1, eco.tacticalExpLevelStep));
                int levelBonus = 100 + level * eco.tacticalExpDefenseBonusPerLevel;
                basePower = basePower * levelBonus / 100;
            }

            // C15a: 将军军衔防御 buff（与战役等级相乘）
            if (_commander != null && world != null && !string.IsNullOrEmpty(unit.commanderId))
            {
                int cmdrBuffPct = _commander.GetCommanderDefenseBuffPct(world, unit.commanderId);
                basePower = basePower * cmdrBuffPct / 100;
            }

            // C15b: 将军卡技能防御 buff
            if (_commander != null && world != null && !string.IsNullOrEmpty(unit.commanderId) && _config != null)
            {
                var cmdr = world.commanders.TryGetValue(unit.commanderId, out var c) ? c : null;
                if (cmdr != null)
                {
                    int cardDefPct = CommanderSkillEvaluator.EvalDefense(_config, cmdr, unit, null, world);
                    basePower = basePower * cardDefPct / 100;

                    // C16: 星级加成（每星 +5%）
                    var gachaEco = _config.Get<EconomyConfig>("global");
                    if (gachaEco != null && cmdr.starLevel > 0)
                    {
                        int starPct = 100 + cmdr.starLevel * gachaEco.starBonusPerStar;
                        basePower = basePower * starPct / 100;
                    }
                }
            }

            return basePower;
        }

        /// <summary>地形防御倍率（int，100 = ×1.0）。P2.4: 从 config 读取，消除硬编码</summary>
        private int GetTerrainDefenseMultiplierInt(TerrainType terrain, EconomyConfig eco)
        {
            if (eco != null && eco.terrainDefenseMult.TryGetValue(terrain.ToString(), out int mult))
                return mult;
            return 100; // 默认无修正
        }

        /// <summary>装甲修正（int: 50/100/120）</summary>
        private int CalculateArmorModifierInt(List<UnitState> attackers, List<UnitState> defenders)
        {
            int atkPiercing = attackers.Max(u => u.piercing);
            int defArmor    = defenders.Max(u => u.armor);
            if (defArmor > atkPiercing) return 50;    // 防穿透不足 ×0.5
            if (atkPiercing > defArmor) return 120;   // 防穿透优势 ×1.2
            return 100;                               // 平衡 ×1.0
        }

        /// <summary>随机抖动（int ±variancePct%）</summary>
        private int ApplyRandomInt(int baseValue, int variancePct)
        {
            int range = baseValue * variancePct / 100;
            if (range <= 0) return Math.Max(1, baseValue);
            int roll = _rng.Range(-range, range + 1);
            return Math.Max(1, baseValue + roll);
        }

        // =====================================================================
        // C12: ResolveMultiBattle — 师级团战核心
        // =====================================================================

        /// <summary>多师团战结算（C12 核心 + C15a 将军 buff）</summary>
        public void ResolveMultiBattle(List<UnitState> attackers, List<UnitState> defenders, ProvinceState province, WorldState world = null)
        {
            if (attackers.Count == 0 || defenders.Count == 0) return;

            // --- 攻方总战力 ---
            var eco = _config?.Get<EconomyConfig>("global");
            int atkTotal = 0;
            foreach (var u in attackers) atkTotal += SingleUnitAttackPower(u, eco, world);

            // --- 守方总战力（含地形） ---
            int defTotal = 0;
            foreach (var u in defenders) defTotal += SingleUnitDefensePower(u, eco, world);
            // P2.4: 地形防御修正 — 由格聚合主导地形 + config 倍率
            var ecoDef = _config.Get<EconomyConfig>("global");
            TerrainType combatTerrain = TerrainAggregator.GetProvinceCombatTerrain(province, world, ecoDef);
            int terrainMult = GetTerrainDefenseMultiplierInt(combatTerrain, ecoDef);
            defTotal = defTotal * terrainMult / 100;

            // --- 装甲修正 ---
            int armorMod = CalculateArmorModifierInt(attackers, defenders);
            atkTotal = atkTotal * armorMod / 100;

            // --- 战斗比（int × 100） ---
            int combatRatioPct = atkTotal * 100 / Math.Max(1, defTotal);
            combatRatioPct = Math.Clamp(combatRatioPct, 10, 1000);

            // --- 总伤害 ---
            int totalAtkOrgDmg = ApplyRandomInt(10 * 100 / Math.Max(1, combatRatioPct), 20);
            int totalAtkStrDmg = ApplyRandomInt(5 * 100 / Math.Max(1, combatRatioPct), 20);
            int totalDefOrgDmg = ApplyRandomInt(10 * combatRatioPct / 100, 20);
            int totalDefStrDmg = ApplyRandomInt(5 * combatRatioPct / 100, 20);

            // --- 按师权重分摊伤害（brigade count 粗略反映规模） ---
            int atkBrigades = attackers.Sum(u => u.brigades != null && u.brigades.Count > 0 ? u.brigades.Sum(b => b.count) : 1);
            int defBrigades = defenders.Sum(u => u.brigades != null && u.brigades.Count > 0 ? u.brigades.Sum(b => b.count) : 1);

            foreach (var u in attackers)
            {
                int unitBrigades = (u.brigades != null && u.brigades.Count > 0) ? u.brigades.Sum(b => b.count) : 1;
                int orgShare = totalAtkOrgDmg * unitBrigades / Math.Max(1, atkBrigades);
                int strShare = totalAtkStrDmg * unitBrigades / Math.Max(1, atkBrigades);
                DistributeDamageToBrigades(u, orgShare, strShare);
            }

            foreach (var u in defenders)
            {
                int unitBrigades = (u.brigades != null && u.brigades.Count > 0) ? u.brigades.Sum(b => b.count) : 1;
                int orgShare = totalDefOrgDmg * unitBrigades / Math.Max(1, defBrigades);
                int strShare = totalDefStrDmg * unitBrigades / Math.Max(1, defBrigades);
                DistributeDamageToBrigades(u, orgShare, strShare);
            }
        }

        /// <summary>单师内伤害分摊到旅（按 manpower 权重）</summary>
        private void DistributeDamageToBrigades(UnitState unit, int orgDmg, int strDmg)
        {
            // 师级 organization 扣减
            unit.organization = Math.Max(0, unit.organization - orgDmg);

            // 无旅组成 → 走旧逻辑
            if (unit.brigades == null || unit.brigades.Count == 0)
            {
                unit.manpower  = Math.Max(0, unit.manpower  - strDmg);
                unit.equipment = Math.Max(0, unit.equipment - strDmg);
                return;
            }

            // 按旅 manpower 权重分摊 strDmg
            int totalMp = unit.brigades.Sum(b => b.manpower);
            if (totalMp > 0)
            {
                int distributed = 0;
                for (int i = 0; i < unit.brigades.Count; i++)
                {
                    var b = unit.brigades[i];
                    int share = (i == unit.brigades.Count - 1)
                        ? strDmg - distributed
                        : strDmg * b.manpower / totalMp;
                    b.TakeDamage(0, share);
                    distributed += share;
                }
            }

            // 移除被歼灭的旅
            unit.brigades.RemoveAll(b => b.IsDestroyed);

            // 旅全灭 → 师溃散
            if (unit.brigades.Count == 0)
                unit.organization = 0;

            // 旅变化后重算师属性
            if (_config != null)
                unit.RecalculateFromBrigades(_config);
        }

        // =====================================================================
        // 向后兼容：1v1 战斗（委托给 ResolveMultiBattle）
        // =====================================================================

        /// <summary>向后兼容：1v1 战斗结算（内部委托 ResolveMultiBattle）</summary>
        public BattleResult ResolveBattle(UnitState attacker, UnitState defender, ProvinceState province, WorldState world = null)
        {
            var result = new BattleResult();
            ResolveMultiBattle(
                new List<UnitState> { attacker },
                new List<UnitState> { defender },
                province,
                world);

            result.attackerWon = defender.IsShattered && !attacker.IsShattered;
            result.defenderWon = attacker.IsShattered && !defender.IsShattered;
            result.draw = attacker.IsShattered && defender.IsShattered;

            _events.Publish(new BattleResolvedEvent
            {
                AttackerId = attacker.id,
                DefenderId = defender.id,
                ProvinceId = province.id,
                AttackerWon = result.attackerWon
            });

            return result;
        }

        // =====================================================================
        // C9c: 多兵种联合战斗（InitiateAttack + TickBattles）
        // =====================================================================

        public CommandResult InitiateAttack(WorldState world, string attackerUnitId, string targetProvinceId, string playerCountryId)
        {
            if (!world.units.TryGetValue(attackerUnitId, out var attacker))
                return CommandResult.Reject("部队不存在");

            if (attacker.ownerCountry != playerCountryId)
                return CommandResult.Reject("非己方部队");

            if (!world.provinces.TryGetValue(targetProvinceId, out var target))
                return CommandResult.Reject("目标省份不存在");

            if (!world.provinces.TryGetValue(attacker.currentProvinceId, out var current))
                return CommandResult.Reject("当前省份不存在");

            if (current.neighbors == null || !current.neighbors.Contains(targetProvinceId))
                return CommandResult.Reject("非邻接省份");

            if (target.controllerCountry == attacker.ownerCountry)
                return CommandResult.Reject("非敌方控制省份");

            // C9d: 和平期内不能开战
            if (WarRegistry.IsInTruce(world, attacker.ownerCountry, target.controllerCountry, world.turnNumber))
                return CommandResult.Reject("和平期内不能开战");

            // C15a: 同省 5 师容量限制（攻方）
            int attackerCountInTarget = world.units.Values.Count(u =>
                u.currentProvinceId == targetProvinceId && u.ownerCountry == attacker.ownerCountry);
            // 加上正在向该省进攻的师
            attackerCountInTarget += world.activeBattles.Count(b =>
                b.provinceId == targetProvinceId && b.attackerOwnerCountry == attacker.ownerCountry);
            if (attackerCountInTarget >= 5)
                return CommandResult.Reject("该省攻方已达 5 师上限");

            if (attacker.movesLeft < 1)
                return CommandResult.Reject("移动力不足");

            // 检查攻方是否已在战斗中
            foreach (var b in world.activeBattles)
            {
                if (b.attackerUnitIds.Contains(attackerUnitId) || b.defenderUnitIds.Contains(attackerUnitId))
                    return CommandResult.Reject("部队正在战斗中");
            }

            // C9c: 检查目标省是否已有战斗
            var existingBattle = world.activeBattles.Find(b => b.provinceId == targetProvinceId);
            if (existingBattle != null)
            {
                if (existingBattle.attackerOwnerCountry == attacker.ownerCountry)
                {
                    attacker.movesLeft -= 1;
                    existingBattle.attackerUnitIds.Add(attackerUnitId);
                    return CommandResult.Accept();
                }
                else
                {
                    return CommandResult.Reject("敌方已对该省发起战斗");
                }
            }

            // 自动宣战
            if (WarRegistry.TryDeclareWar(world, attacker.ownerCountry, target.ownerCountry, world.turnNumber, out var newWar))
            {
                _events.Publish(new WarDeclaredEvent
                {
                    countryA = newWar.countryA,
                    countryB = newWar.countryB,
                    startTurn = newWar.startTurn
                });
            }

            var defenders = world.units.Values
                .Where(u => u.currentProvinceId == targetProvinceId && u.ownerCountry != attacker.ownerCountry)
                .OrderBy(u => u.id, StringComparer.Ordinal)
                .ToList();

            // 空城 → 立即占领
            if (defenders.Count == 0)
            {
                attacker.movesLeft -= 1;
                string prevController = target.controllerCountry;
                string attackerPrevProvince = attacker.currentProvinceId;
                attacker.currentProvinceId = targetProvinceId;
                target.controllerCountry = attacker.ownerCountry;

                // P2.5: 更新空间索引
                if (world.provinceUnitIds.TryGetValue(attackerPrevProvince, out var prevList))
                    prevList.Remove(attacker.id);
                if (!world.provinceUnitIds.ContainsKey(targetProvinceId))
                    world.provinceUnitIds[targetProvinceId] = new System.Collections.Generic.List<string>();
                world.provinceUnitIds[targetProvinceId].Add(attacker.id);

                var eco6 = _config?.Get<EconomyConfig>("global");
                target.resistance = eco6?.resistanceOnCapture ?? 50;

                _events.Publish(new ProvinceOccupiedEvent
                {
                    provinceId = targetProvinceId,
                    newControllerCountry = attacker.ownerCountry,
                    previousControllerCountry = prevController,
                    attackerUnitId = attackerUnitId
                });

                ApplyCapitalLossPenalty(world, prevController, targetProvinceId);
                return CommandResult.Accept();
            }

            // 有守方 → 创建 ActiveBattle
            attacker.movesLeft -= 1;

            var battle = new ActiveBattle
            {
                id = targetProvinceId,
                attackerUnitIds = new List<string> { attackerUnitId },
                defenderUnitIds = defenders.Select(u => u.id).ToList(),
                provinceId = targetProvinceId,
                attackerOwnerCountry = attacker.ownerCountry,
                defenderOwnerCountry = defenders[0].ownerCountry,
                turnsElapsed = 0
            };
            world.activeBattles.Add(battle);
            world.activeBattles.Sort((a, b2) => string.Compare(a.id, b2.id, StringComparison.Ordinal));

            _events.Publish(new BattleInitiatedEvent
            {
                battleId = battle.id,
                attackerUnitId = attackerUnitId,
                defenderUnitId = defenders[0].id,
                provinceId = targetProvinceId
            });

            return CommandResult.Accept();
        }

        public void TickBattles(WorldState world)
        {
            var snapshot = world.activeBattles
                .OrderBy(b => b.id, StringComparer.Ordinal)
                .ToList();

            foreach (var battle in snapshot)
            {
                battle.attackerUnitIds.RemoveAll(uid => !world.units.ContainsKey(uid));
                battle.defenderUnitIds.RemoveAll(uid => !world.units.ContainsKey(uid));

                if (!world.provinces.TryGetValue(battle.provinceId, out var province))
                {
                    world.activeBattles.Remove(battle);
                    continue;
                }

                var attackers = battle.attackerUnitIds
                    .Where(uid => world.units.TryGetValue(uid, out _))
                    .Select(uid => world.units[uid])
                    .ToList();
                var defenders = battle.defenderUnitIds
                    .Where(uid => world.units.TryGetValue(uid, out _))
                    .Select(uid => world.units[uid])
                    .ToList();

                if (attackers.Count == 0 && defenders.Count == 0)
                {
                    world.activeBattles.Remove(battle);
                    continue;
                }

                if (attackers.Count == 0)
                {
                    ResolveDefenderWin(world, battle, defenders, province);
                    world.activeBattles.Remove(battle);
                    continue;
                }
                if (defenders.Count == 0)
                {
                    ResolveAttackerWin(world, battle, attackers, province);
                    world.activeBattles.Remove(battle);
                    continue;
                }

                // 双方都有 → tick 战斗（C12: 整数化 + 旅级战损 + C15a 将军 buff）
                battle.turnsElapsed++;
                ResolveMultiBattle(attackers, defenders, province, world);

                // 移除 shattered
                var shatteredAttackers = attackers.Where(u => u.IsShattered).Select(u => u.id).ToList();
                var shatteredDefenders = defenders.Where(u => u.IsShattered).Select(u => u.id).ToList();
                foreach (var uid in shatteredAttackers)
                {
                    DestroyUnit(world, uid, "battle");
                    battle.attackerUnitIds.Remove(uid);
                }
                foreach (var uid in shatteredDefenders)
                {
                    DestroyUnit(world, uid, "battle");
                    battle.defenderUnitIds.Remove(uid);
                }

                // C13: 85% 自动溃退判定
                var eco13 = _config?.Get<EconomyConfig>("global");
                if (eco13 != null)
                {
                    CheckAutoRetreat(world, battle, battle.attackerUnitIds, eco13);
                    CheckAutoRetreat(world, battle, battle.defenderUnitIds, eco13);
                }

                // 同 tick 内判胜负
                if (battle.attackerUnitIds.Count == 0 && battle.defenderUnitIds.Count == 0)
                {
                    world.activeBattles.Remove(battle);
                }
                else if (battle.defenderUnitIds.Count == 0)
                {
                    var survivingAttackers = battle.attackerUnitIds
                        .Where(uid => world.units.TryGetValue(uid, out _))
                        .Select(uid => world.units[uid])
                        .ToList();
                    ResolveAttackerWin(world, battle, survivingAttackers, province);
                    world.activeBattles.Remove(battle);
                }
                else if (battle.attackerUnitIds.Count == 0)
                {
                    var survivingDefenders = battle.defenderUnitIds
                        .Where(uid => world.units.TryGetValue(uid, out _))
                        .Select(uid => world.units[uid])
                        .ToList();
                    ResolveDefenderWin(world, battle, survivingDefenders, province);
                    world.activeBattles.Remove(battle);
                }
            }
        }

        // =====================================================================
        // C13: 自动溃退
        // =====================================================================

        private void CheckAutoRetreat(WorldState world, ActiveBattle battle, List<string> unitIds, EconomyConfig eco)
        {
            var toRemove = new List<string>();
            foreach (var uid in unitIds.ToList())
            {
                if (!world.units.TryGetValue(uid, out var unit)) continue;

                // 检查 manpower ≤ max × threshold%（maxManpower=0 时不触发）
                if (unit.maxManpower <= 0 || unit.manpower * 100 > unit.maxManpower * eco.autoRetreatThresholdPct)
                    continue;

                // 被切断不溃退（C13 默认 false）
                if (unit.isCutoff) continue;

                // 找后方己方控制省
                string retreatProvinceId = FindRetreatProvince(unit, world);
                if (retreatProvinceId == null)
                {
                    // 无可达后方 → 消灭
                    DestroyUnit(world, uid, "no_retreat_path");
                    toRemove.Add(uid);
                    continue;
                }

                // 执行溃退
                string fromProvince = unit.currentProvinceId;
                unit.currentProvinceId = retreatProvinceId;
                unit.manpower = Math.Min(unit.manpower + eco.retreatBonusManpower, unit.maxManpower);
                unit.equipment = Math.Min(unit.equipment + eco.retreatBonusEquipment, unit.maxEquipment);
                unit.morale = eco.retreatMoraleReset;
                unit.recoveryTurnsLeft = eco.retreatRecoveryTurns;

                // P2.5: 更新空间索引
                if (world.provinceUnitIds.TryGetValue(fromProvince, out var retreatList))
                    retreatList.Remove(uid);
                if (!world.provinceUnitIds.ContainsKey(retreatProvinceId))
                    world.provinceUnitIds[retreatProvinceId] = new System.Collections.Generic.List<string>();
                world.provinceUnitIds[retreatProvinceId].Add(uid);

                // 旅级同步补充
                if (_config != null && unit.brigades != null && unit.brigades.Count > 0)
                {
                    DistributeReinforcementToBrigades(unit, eco.retreatBonusManpower, eco.retreatBonusEquipment);
                    unit.RecalculateFromBrigades(_config);
                }

                toRemove.Add(uid);
                _events.Publish(new UnitRetreatedEvent
                {
                    unitId = uid,
                    fromProvinceId = fromProvince,
                    retreatProvinceId = retreatProvinceId,
                    turnNumber = world.turnNumber
                });
            }
            foreach (var uid in toRemove)
                unitIds.Remove(uid);
        }

        /// <summary>找己方控制的邻接省（C13 简化：id 升序第一个）</summary>
        private string FindRetreatProvince(UnitState unit, WorldState world)
        {
            if (!world.provinces.TryGetValue(unit.currentProvinceId, out var current))
                return null;
            if (current.neighbors == null) return null;

            var candidates = new List<string>();
            foreach (var nid in current.neighbors)
            {
                if (world.provinces.TryGetValue(nid, out var neighbor)
                    && neighbor.controllerCountry == unit.ownerCountry)
                {
                    candidates.Add(nid);
                }
            }

            if (candidates.Count == 0) return null;
            candidates.Sort(StringComparer.Ordinal);
            return candidates[0];
        }

        /// <summary>C13: 旅级补员分摊</summary>
        private void DistributeReinforcementToBrigades(UnitState unit, int manpowerGained, int equipmentGained)
        {
            if (unit.brigades == null || unit.brigades.Count == 0) return;
            int totalMp = unit.brigades.Sum(b => b.manpower);
            if (totalMp <= 0 && manpowerGained > 0)
            {
                // 全灭旅平均分配
                int perBrigade = manpowerGained / unit.brigades.Count;
                foreach (var b in unit.brigades)
                    b.manpower += perBrigade;
                unit.brigades[0].manpower += manpowerGained - perBrigade * unit.brigades.Count;
            }
            else if (manpowerGained > 0)
            {
                int distributed = 0;
                for (int i = 0; i < unit.brigades.Count; i++)
                {
                    var b = unit.brigades[i];
                    int share = (i == unit.brigades.Count - 1)
                        ? manpowerGained - distributed
                        : manpowerGained * b.manpower / totalMp;
                    b.manpower += share;
                    distributed += share;
                }
            }

            // 装备同理
            int totalEq = unit.brigades.Sum(b => b.equipment);
            if (totalEq <= 0 && equipmentGained > 0)
            {
                int perBrigade = equipmentGained / unit.brigades.Count;
                foreach (var b in unit.brigades)
                    b.equipment += perBrigade;
                unit.brigades[0].equipment += equipmentGained - perBrigade * unit.brigades.Count;
            }
            else if (equipmentGained > 0)
            {
                int distributed = 0;
                for (int i = 0; i < unit.brigades.Count; i++)
                {
                    var b = unit.brigades[i];
                    int share = (i == unit.brigades.Count - 1)
                        ? equipmentGained - distributed
                        : equipmentGained * b.equipment / totalEq;
                    b.equipment += share;
                    distributed += share;
                }
            }
        }

        // =====================================================================
        // 胜负处理（含 C13 战役经验累积）
        // =====================================================================

        private void ResolveAttackerWin(WorldState world, ActiveBattle battle, List<UnitState> attackers, ProvinceState province)
        {
            var toClear = world.units.Values
                .Where(u => u.currentProvinceId == battle.provinceId && u.ownerCountry != battle.attackerOwnerCountry)
                .Select(u => u.id)
                .ToList();
            foreach (var clearId in toClear)
                DestroyUnit(world, clearId, "occupation");

            string prevController = province.controllerCountry;
            if (attackers.Count > 0)
            {
                var firstAttacker = attackers[0];
                string prevProv = firstAttacker.currentProvinceId;
                firstAttacker.currentProvinceId = battle.provinceId;

                // P2.5: 更新空间索引
                if (world.provinceUnitIds.TryGetValue(prevProv, out var occList))
                    occList.Remove(firstAttacker.id);
                if (!world.provinceUnitIds.ContainsKey(battle.provinceId))
                    world.provinceUnitIds[battle.provinceId] = new System.Collections.Generic.List<string>();
                world.provinceUnitIds[battle.provinceId].Add(firstAttacker.id);
            }
            province.controllerCountry = battle.attackerOwnerCountry;

            var eco6 = _config?.Get<EconomyConfig>("global");
            province.resistance = eco6?.resistanceOnCapture ?? 50;

            _events.Publish(new ProvinceOccupiedEvent
            {
                provinceId = battle.provinceId,
                newControllerCountry = battle.attackerOwnerCountry,
                previousControllerCountry = prevController,
                attackerUnitId = attackers.Count > 0 ? attackers[0].id : ""
            });

            ApplyCapitalLossPenalty(world, prevController, battle.provinceId);

            // C14: 解围 A 方案 — 占领后解除该省友方部队的切断状态
            SupplyResolver.CheckRelief(world, battle.provinceId, battle.attackerOwnerCountry);

            if (attackers.Count > 0 && !string.IsNullOrEmpty(battle.defenderOwnerCountry))
                ApplyBattleTollByCountry(world, battle.attackerOwnerCountry, battle.defenderOwnerCountry, "Attacker");

            // C13: 攻胜 → 攻方 +10, 守方 -5
            var ecoWin = _config?.Get<EconomyConfig>("global");
            if (ecoWin != null)
            {
                foreach (var uid in battle.attackerUnitIds)
                {
                    if (world.units.TryGetValue(uid, out var u))
                        u.tacticalExp = Math.Clamp(u.tacticalExp + ecoWin.tacticalExpPerVictory, 0, 100);
                }
                foreach (var uid in battle.defenderUnitIds)
                {
                    if (world.units.TryGetValue(uid, out var u))
                        u.tacticalExp = Math.Clamp(u.tacticalExp + ecoWin.tacticalExpPerDefeat, 0, 100);
                }
            }

            // C15a: 攻胜 → 攻方将领记录胜场
            if (_commander != null)
            {
                foreach (var uid in battle.attackerUnitIds)
                {
                    if (world.units.TryGetValue(uid, out var u) && !string.IsNullOrEmpty(u.commanderId))
                        _commander.RecordBattleVictory(world, u.commanderId);
                }
            }

            // P2.1: 战功点累积（原 gachaTickets,语义改名）
            if (ecoWin != null && world.countries.TryGetValue(battle.attackerOwnerCountry, out var atkCountry))
            {
                atkCountry.gachaTickets += ecoWin.gachaTicketsPerVictory;
                // 包围歼敌额外 +3
                bool wasEncirclement = battle.defenderUnitIds.Any(uid =>
                    world.units.TryGetValue(uid, out var du) && du.isCutoff);
                if (wasEncirclement)
                    atkCountry.gachaTickets += ecoWin.gachaTicketsPerEncirclement;
                // 占领首都额外 +10
                if (province != null && province.isCapital)
                    atkCountry.gachaTickets += ecoWin.gachaTicketsPerCapitalCapture;
            }

            _events.Publish(new BattleConcludedEvent
            {
                battleId = battle.id,
                provinceId = battle.provinceId,
                winnerKind = "Attacker",
                attackerUnitId = attackers.Count > 0 ? attackers[0].id : "",
                defenderUnitId = battle.defenderUnitIds.Count > 0 ? battle.defenderUnitIds[0] : "",
                turnsElapsed = battle.turnsElapsed
            });
        }

        private void ResolveDefenderWin(WorldState world, ActiveBattle battle, List<UnitState> defenders, ProvinceState province)
        {
            if (defenders.Count > 0 && !string.IsNullOrEmpty(battle.attackerOwnerCountry))
                ApplyBattleTollByCountry(world, battle.attackerOwnerCountry, battle.defenderOwnerCountry, "Defender");

            // C13: 守胜 → 守方 +10, 攻方 -5
            var ecoWin = _config?.Get<EconomyConfig>("global");
            if (ecoWin != null)
            {
                foreach (var uid in battle.defenderUnitIds)
                {
                    if (world.units.TryGetValue(uid, out var u))
                        u.tacticalExp = Math.Clamp(u.tacticalExp + ecoWin.tacticalExpPerVictory, 0, 100);
                }
                foreach (var uid in battle.attackerUnitIds)
                {
                    if (world.units.TryGetValue(uid, out var u))
                        u.tacticalExp = Math.Clamp(u.tacticalExp + ecoWin.tacticalExpPerDefeat, 0, 100);
                }
            }

            // C15a: 守胜 → 守方将领记录胜场
            if (_commander != null)
            {
                foreach (var uid in battle.defenderUnitIds)
                {
                    if (world.units.TryGetValue(uid, out var u) && !string.IsNullOrEmpty(u.commanderId))
                        _commander.RecordBattleVictory(world, u.commanderId);
                }
            }

            _events.Publish(new BattleConcludedEvent
            {
                battleId = battle.id,
                provinceId = battle.provinceId,
                winnerKind = "Defender",
                attackerUnitId = battle.attackerUnitIds.Count > 0 ? battle.attackerUnitIds[0] : "",
                defenderUnitId = defenders.Count > 0 ? defenders[0].id : "",
                turnsElapsed = battle.turnsElapsed
            });
        }

        private void DestroyUnit(WorldState world, string unitId, string cause)
        {
            if (!world.units.TryGetValue(unitId, out var unit)) return;
            string provinceId = unit.currentProvinceId;
            string owner = unit.ownerCountry;

            world.units.Remove(unitId);
            if (world.countries.TryGetValue(owner, out var country))
                country.unitIds.Remove(unitId);

            // P2.5: 从空间索引移除
            if (!string.IsNullOrEmpty(provinceId) && world.provinceUnitIds.TryGetValue(provinceId, out var unitList))
                unitList.Remove(unitId);

            _events.Publish(new UnitDestroyedEvent
            {
                unitId = unitId,
                ownerCountry = owner,
                provinceId = provinceId,
                cause = cause
            });
        }

        // =====================================================================
        // C5: 战争代价（未改动）
        // =====================================================================

        internal void ApplyBattleToll(WorldState world, UnitState attacker, UnitState defender, string winnerKind)
        {
            var eco = _config?.Get<EconomyConfig>("global");
            if (eco == null) return;
            if (!world.countries.TryGetValue(attacker.ownerCountry, out var atkCountry)) return;
            if (!world.countries.TryGetValue(defender.ownerCountry, out var defCountry)) return;

            switch (winnerKind)
            {
                case "Attacker":
                    atkCountry.warSupport = Math.Clamp(atkCountry.warSupport + eco.warSupportBonusPerVictory, 0, 100);
                    defCountry.warSupport = Math.Clamp(defCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                    break;
                case "Defender":
                    defCountry.warSupport = Math.Clamp(defCountry.warSupport + eco.warSupportBonusPerVictory, 0, 100);
                    atkCountry.warSupport = Math.Clamp(atkCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                    break;
                case "Draw":
                    atkCountry.warSupport = Math.Clamp(atkCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                    defCountry.warSupport = Math.Clamp(defCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                    break;
            }
        }

        internal void ApplyBattleTollByCountry(WorldState world, string attackerCountryId, string defenderCountryId, string winnerKind)
        {
            var eco = _config?.Get<EconomyConfig>("global");
            if (eco == null) return;
            if (!world.countries.TryGetValue(attackerCountryId, out var atkCountry)) return;
            if (!world.countries.TryGetValue(defenderCountryId, out var defCountry)) return;

            switch (winnerKind)
            {
                case "Attacker":
                    atkCountry.warSupport = Math.Clamp(atkCountry.warSupport + eco.warSupportBonusPerVictory, 0, 100);
                    defCountry.warSupport = Math.Clamp(defCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                    break;
                case "Defender":
                    defCountry.warSupport = Math.Clamp(defCountry.warSupport + eco.warSupportBonusPerVictory, 0, 100);
                    atkCountry.warSupport = Math.Clamp(atkCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                    break;
                case "Draw":
                    atkCountry.warSupport = Math.Clamp(atkCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                    defCountry.warSupport = Math.Clamp(defCountry.warSupport - eco.warSupportPenaltyPerLoss, 0, 100);
                    break;
            }
        }

        internal void ApplyCapitalLossPenalty(WorldState world, string previousControllerCountry, string provinceId)
        {
            var eco = _config?.Get<EconomyConfig>("global");
            if (eco == null) return;
            if (string.IsNullOrEmpty(previousControllerCountry)) return;
            if (!world.countries.TryGetValue(previousControllerCountry, out var country)) return;
            if (country.capitalProvinceId == provinceId)
                country.warSupport = Math.Clamp(country.warSupport - eco.warSupportPenaltyPerCapitalLoss, 0, 100);
        }
    }

    public struct BattleResult
    {
        public bool attackerWon;
        public bool defenderWon;
        public bool draw;
    }
}

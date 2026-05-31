// ============================================================================
// Simulation/BattleResolver.cs — 战斗结算器（C9c 多兵种联合战斗）
// Phase 5: RandomService → IRandom, EventBus.Instance → IEventPublisher
// float 公式保持不变
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

        public BattleResolver(IRandom rng, IEventPublisher events, IConfigRegistry config = null)
        {
            _rng = rng;
            _events = events;
            _config = config;
        }

        /// <summary>向后兼容：1v1 战斗结算（保留，不再被 TickBattles 调用）</summary>
        public BattleResult ResolveBattle(UnitState attacker, UnitState defender, ProvinceState province)
        {
            var result = new BattleResult();

            float attackValue = CalculateAttack(attacker, province);
            float defenseValue = CalculateDefense(defender, province);
            float armorModifier = CalculateArmorModifier(attacker, defender);
            float attackerSupplyMod = GetSupplyModifier(attacker);
            float defenderSupplyMod = GetSupplyModifier(defender);

            float finalAttack = attackValue * armorModifier * attackerSupplyMod;
            float finalDefense = defenseValue * defenderSupplyMod;

            float combatRatio = finalAttack / System.Math.Max(1, finalDefense);

            int attackerOrgDamage = (int)(10 * (1f / System.Math.Max(0.1f, combatRatio)));
            int attackerStrDamage = (int)(5 * (1f / System.Math.Max(0.1f, combatRatio)));
            int defenderOrgDamage = (int)(10 * combatRatio);
            int defenderStrDamage = (int)(5 * combatRatio);

            attackerOrgDamage = ApplyRandom(attackerOrgDamage, 0.2f);
            attackerStrDamage = ApplyRandom(attackerStrDamage, 0.2f);
            defenderOrgDamage = ApplyRandom(defenderOrgDamage, 0.2f);
            defenderStrDamage = ApplyRandom(defenderStrDamage, 0.2f);

            attacker.TakeDamage(attackerOrgDamage, attackerStrDamage);
            defender.TakeDamage(defenderOrgDamage, defenderStrDamage);

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

        // === C9c: 多兵种联合战斗 ===

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
                // 同阵营 → 加入攻方
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

            // 自动宣战（基于 target.ownerCountry 法理主权）
            if (WarRegistry.TryDeclareWar(world, attacker.ownerCountry, target.ownerCountry, world.turnNumber, out var newWar))
            {
                _events.Publish(new WarDeclaredEvent
                {
                    countryA = newWar.countryA,
                    countryB = newWar.countryB,
                    startTurn = newWar.startTurn
                });
            }

            // 取守方：target 省内非攻方所属部队
            var defenders = world.units.Values
                .Where(u => u.currentProvinceId == targetProvinceId && u.ownerCountry != attacker.ownerCountry)
                .OrderBy(u => u.id, StringComparer.Ordinal)
                .ToList();

            // 空城 → 立即占领
            if (defenders.Count == 0)
            {
                attacker.movesLeft -= 1;
                string prevController = target.controllerCountry;
                attacker.currentProvinceId = targetProvinceId;
                target.controllerCountry = attacker.ownerCountry;

                // C6: 占领瞬间 resistance 设为配置值
                var eco6 = _config?.Get<EconomyConfig>("global");
                target.resistance = eco6?.resistanceOnCapture ?? 50;

                _events.Publish(new ProvinceOccupiedEvent
                {
                    provinceId = targetProvinceId,
                    newControllerCountry = attacker.ownerCountry,
                    previousControllerCountry = prevController,
                    attackerUnitId = attackerUnitId
                });

                // C5: 首都丢失扣 warSupport
                ApplyCapitalLossPenalty(world, prevController, targetProvinceId);

                return CommandResult.Accept();
            }

            // 有守方 → 创建 ActiveBattle（C9c 多对多）
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
            // 拷贝以允许遍历中移除
            var snapshot = world.activeBattles
                .OrderBy(b => b.id, StringComparer.Ordinal)
                .ToList();

            foreach (var battle in snapshot)
            {
                // 防御性：移除已不存在的 unit
                battle.attackerUnitIds.RemoveAll(uid => !world.units.ContainsKey(uid));
                battle.defenderUnitIds.RemoveAll(uid => !world.units.ContainsKey(uid));

                // 获取省份
                if (!world.provinces.TryGetValue(battle.provinceId, out var province))
                {
                    world.activeBattles.Remove(battle);
                    continue;
                }

                // 获取双方存活部队快照
                var attackers = battle.attackerUnitIds
                    .Where(uid => world.units.TryGetValue(uid, out _))
                    .Select(uid => world.units[uid])
                    .ToList();
                var defenders = battle.defenderUnitIds
                    .Where(uid => world.units.TryGetValue(uid, out _))
                    .Select(uid => world.units[uid])
                    .ToList();

                // 双方都空 → 移除
                if (attackers.Count == 0 && defenders.Count == 0)
                {
                    world.activeBattles.Remove(battle);
                    continue;
                }

                // 一方空 → 判定胜负
                if (attackers.Count == 0)
                {
                    // 守胜
                    ResolveDefenderWin(world, battle, defenders, province);
                    world.activeBattles.Remove(battle);
                    continue;
                }
                if (defenders.Count == 0)
                {
                    // 攻胜
                    ResolveAttackerWin(world, battle, attackers, province);
                    world.activeBattles.Remove(battle);
                    continue;
                }

                // 双方都有 → tick 战斗
                battle.turnsElapsed++;

                // C9c: sum 战力公式
                int attackPower = 0;
                foreach (var u in attackers)
                    attackPower += (int)(u.baseAttack * u.organization * 100f / Math.Max(1, u.maxOrganization));

                int defendPower = 0;
                foreach (var u in defenders)
                    defendPower += (int)(u.baseDefense * u.organization * 100f / Math.Max(1, u.maxOrganization));

                // 地形倍率
                float terrainMod = GetTerrainDefenseMultiplier(province.terrain);
                defendPower = (int)(defendPower * terrainMod);

                float combatRatio = (float)attackPower / Math.Max(1, defendPower);

                // 双方统一伤害
                int atkOrgDmg = ApplyRandom((int)(10 * (1f / Math.Max(0.1f, combatRatio))), 0.2f);
                int atkStrDmg = ApplyRandom((int)(5 * (1f / Math.Max(0.1f, combatRatio))), 0.2f);
                int defOrgDmg = ApplyRandom((int)(10 * combatRatio), 0.2f);
                int defStrDmg = ApplyRandom((int)(5 * combatRatio), 0.2f);

                foreach (var u in attackers)
                    u.TakeDamage(atkOrgDmg, atkStrDmg);
                foreach (var u in defenders)
                    u.TakeDamage(defOrgDmg, defStrDmg);

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

                // C9c: 同 tick 内判胜负
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

        private void ResolveAttackerWin(WorldState world, ActiveBattle battle, List<UnitState> attackers, ProvinceState province)
        {
            // 清场该省所有非攻方部队
            var toClear = world.units.Values
                .Where(u => u.currentProvinceId == battle.provinceId && u.ownerCountry != battle.attackerOwnerCountry)
                .Select(u => u.id)
                .ToList();
            foreach (var clearId in toClear)
                DestroyUnit(world, clearId, "occupation");

            // 攻方首支进省
            string prevController = province.controllerCountry;
            if (attackers.Count > 0)
            {
                attackers[0].currentProvinceId = battle.provinceId;
            }
            province.controllerCountry = battle.attackerOwnerCountry;

            // C6: 占领瞬间 resistance
            var eco6 = _config?.Get<EconomyConfig>("global");
            province.resistance = eco6?.resistanceOnCapture ?? 50;

            _events.Publish(new ProvinceOccupiedEvent
            {
                provinceId = battle.provinceId,
                newControllerCountry = battle.attackerOwnerCountry,
                previousControllerCountry = prevController,
                attackerUnitId = attackers.Count > 0 ? attackers[0].id : ""
            });

            // C5: 首都丢失扣 warSupport
            ApplyCapitalLossPenalty(world, prevController, battle.provinceId);

            // C5: 战斗胜负 warSupport 变化
            if (attackers.Count > 0 && !string.IsNullOrEmpty(battle.defenderOwnerCountry))
            {
                ApplyBattleTollByCountry(world, battle.attackerOwnerCountry, battle.defenderOwnerCountry, "Attacker");
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
            // C5: 战斗胜负 warSupport 变化
            if (defenders.Count > 0 && !string.IsNullOrEmpty(battle.attackerOwnerCountry))
            {
                ApplyBattleTollByCountry(world, battle.attackerOwnerCountry, battle.defenderOwnerCountry, "Defender");
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

            _events.Publish(new UnitDestroyedEvent
            {
                unitId = unitId,
                ownerCountry = owner,
                provinceId = provinceId,
                cause = cause
            });
        }

        private float CalculateAttack(UnitState unit, ProvinceState province)
        {
            float attack = unit.baseAttack;
            attack *= 1f + (unit.experience * 0.1f);
            return attack;
        }

        private float CalculateDefense(UnitState unit, ProvinceState province)
        {
            float defense = unit.baseDefense;
            defense *= GetTerrainDefenseMultiplier(province.terrain);
            if (unit.isEntrenched)
                defense *= 1f + (unit.entrenchmentBonus * 0.05f);
            return defense;
        }

        private float CalculateArmorModifier(UnitState attacker, UnitState defender)
        {
            if (defender.armor > attacker.piercing) return 0.5f;
            if (attacker.piercing > defender.armor) return 1.2f;
            return 1.0f;
        }

        private float GetTerrainDefenseMultiplier(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Plain => 1.0f,
                TerrainType.Forest => 1.10f,
                TerrainType.Mountain => 1.25f,
                TerrainType.Hills => 1.15f,
                TerrainType.Urban => 1.30f,
                TerrainType.Swamp => 1.20f,
                TerrainType.River => 1.20f,
                _ => 1.0f
            };
        }

        private float GetSupplyModifier(UnitState unit) => 1.0f;

        private int ApplyRandom(int baseValue, float variance)
        {
            float factor = 1f + (float)(_rng.NextDouble() * 2 - 1) * variance;
            return System.Math.Max(1, (int)(baseValue * factor));
        }

        // === C5: 战争代价 ===

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

        /// <summary>C9c: 直接按国家 ID 计算战后 warSupport 变化（避免单位已被销毁无法查找）</summary>
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
            {
                country.warSupport = Math.Clamp(country.warSupport - eco.warSupportPenaltyPerCapitalLoss, 0, 100);
            }
        }
    }

    public struct BattleResult
    {
        public bool attackerWon;
        public bool defenderWon;
        public bool draw;
    }
}

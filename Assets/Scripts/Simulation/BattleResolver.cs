// ============================================================================
// Simulation/BattleResolver.cs — 战斗结算器
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

        public BattleResolver(IRandom rng, IEventPublisher events)
        {
            _rng = rng;
            _events = events;
        }

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

        // === C3: 进攻发起 & 战斗 tick ===

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

            if (attacker.movesLeft < 1)
                return CommandResult.Reject("移动力不足");

            // 检查攻方是否已在战斗中
            foreach (var b in world.activeBattles)
            {
                if (b.attackerUnitId == attackerUnitId || b.defenderUnitId == attackerUnitId)
                    return CommandResult.Reject("部队正在战斗中");
            }

            // 检查目标省是否已有战斗（防止同省多攻方导致静默消失）
            foreach (var b in world.activeBattles)
            {
                if (b.provinceId == targetProvinceId)
                    return CommandResult.Reject("该省已有战斗进行中");
            }

            // 取守方：target 省内非攻方所属部队，按 id 升序 [0] 为主战
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

                _events.Publish(new ProvinceOccupiedEvent
                {
                    provinceId = targetProvinceId,
                    newControllerCountry = attacker.ownerCountry,
                    previousControllerCountry = prevController,
                    attackerUnitId = attackerUnitId
                });

                return CommandResult.Accept();
            }

            // 有守方 → 创建 ActiveBattle
            var defender = defenders[0];
            attacker.movesLeft -= 1;

            var battle = new ActiveBattle
            {
                id = attackerUnitId + "_vs_" + defender.id,
                attackerUnitId = attackerUnitId,
                defenderUnitId = defender.id,
                provinceId = targetProvinceId,
                turnsElapsed = 0
            };
            world.activeBattles.Add(battle);
            world.activeBattles.Sort((a, b2) => string.Compare(a.id, b2.id, StringComparison.Ordinal));

            _events.Publish(new BattleInitiatedEvent
            {
                battleId = battle.id,
                attackerUnitId = attackerUnitId,
                defenderUnitId = defender.id,
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
                // 防御性：若 unit 已不存在则移除
                if (!world.units.TryGetValue(battle.attackerUnitId, out var attacker) ||
                    !world.units.TryGetValue(battle.defenderUnitId, out var defender) ||
                    !world.provinces.TryGetValue(battle.provinceId, out var province))
                {
                    world.activeBattles.Remove(battle);
                    continue;
                }

                // 跑一帧战斗
                var result = ResolveBattle(attacker, defender, province);
                battle.turnsElapsed++;

                if (result.attackerWon)
                {
                    // 守方主力消灭
                    DestroyUnit(world, battle.defenderUnitId, "battle");
                    // 清场该省其他非攻方部队
                    var toClear = world.units.Values
                        .Where(u => u.currentProvinceId == battle.provinceId && u.ownerCountry != attacker.ownerCountry)
                        .Select(u => u.id)
                        .ToList();
                    foreach (var clearId in toClear)
                        DestroyUnit(world, clearId, "occupation");
                    // 攻方进省 + 占领
                    string prevController = province.controllerCountry;
                    attacker.currentProvinceId = battle.provinceId;
                    province.controllerCountry = attacker.ownerCountry;

                    _events.Publish(new ProvinceOccupiedEvent
                    {
                        provinceId = battle.provinceId,
                        newControllerCountry = attacker.ownerCountry,
                        previousControllerCountry = prevController,
                        attackerUnitId = battle.attackerUnitId
                    });
                    _events.Publish(new BattleConcludedEvent
                    {
                        battleId = battle.id,
                        provinceId = battle.provinceId,
                        winnerKind = "Attacker",
                        attackerUnitId = battle.attackerUnitId,
                        defenderUnitId = battle.defenderUnitId,
                        turnsElapsed = battle.turnsElapsed
                    });
                    world.activeBattles.Remove(battle);
                }
                else if (result.defenderWon)
                {
                    // 攻方消灭
                    DestroyUnit(world, battle.attackerUnitId, "battle");
                    _events.Publish(new BattleConcludedEvent
                    {
                        battleId = battle.id,
                        provinceId = battle.provinceId,
                        winnerKind = "Defender",
                        attackerUnitId = battle.attackerUnitId,
                        defenderUnitId = battle.defenderUnitId,
                        turnsElapsed = battle.turnsElapsed
                    });
                    world.activeBattles.Remove(battle);
                }
                else if (result.draw)
                {
                    // 双方主力消灭
                    DestroyUnit(world, battle.attackerUnitId, "battle");
                    DestroyUnit(world, battle.defenderUnitId, "battle");
                    _events.Publish(new BattleConcludedEvent
                    {
                        battleId = battle.id,
                        provinceId = battle.provinceId,
                        winnerKind = "Draw",
                        attackerUnitId = battle.attackerUnitId,
                        defenderUnitId = battle.defenderUnitId,
                        turnsElapsed = battle.turnsElapsed
                    });
                    world.activeBattles.Remove(battle);
                }
                // else: 双方都未崩，继续下回合
            }
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
    }

    public struct BattleResult
    {
        public bool attackerWon;
        public bool defenderWon;
        public bool draw;
    }
}

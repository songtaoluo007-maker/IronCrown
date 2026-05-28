// ============================================================================
// Simulation/BattleResolver.cs — 战斗结算器
// Phase 5: RandomService → IRandom, EventBus.Instance → IEventPublisher
// float 公式保持不变
// ============================================================================

using System;
using IronCrown.Domain;

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

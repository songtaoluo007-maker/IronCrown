// ============================================================================
// Simulation/SupplyResolver.cs — 补给结算器
// Phase 5: GameState → WorldState
// ============================================================================

using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class SupplyResolver
    {
        public void CheckSupply(CountryState country, WorldState world)
        {
            foreach (var unitId in country.unitIds)
            {
                // TODO: 查找 unit，计算补给
            }
        }

        public int CalculateProvinceSupply(ProvinceState province)
        {
            int baseSupply = province.infrastructure * 10;
            int railwayBonus = province.railwayLevel * 15;
            int portBonus = province.portLevel * 10;
            int airBaseBonus = province.airBaseLevel * 5;
            return baseSupply + railwayBonus + portBonus + airBaseBonus;
        }

        public int CalculateUnitSupplyNeed(UnitState unit)
        {
            int baseNeed = unit.supplyConsumption;
            float mechFactor = unit.unitType switch
            {
                "infantry" => 1.0f,
                "artillery" => 1.2f,
                "mech_inf" => 1.8f,
                "light_tank" => 2.0f,
                "medium_tank" => 2.5f,
                "fighter" => 1.5f,
                "bomber" => 1.8f,
                _ => 1.0f
            };
            return (int)(baseNeed * mechFactor);
        }

        public float GetSupplyModifier(UnitState unit, ProvinceState province)
        {
            int supplyCapacity = CalculateProvinceSupply(province);
            int supplyNeed = CalculateUnitSupplyNeed(unit);
            if (supplyNeed <= 0) return 1.0f;

            float ratio = (float)supplyCapacity / supplyNeed;
            if (ratio >= 1.0f) return 1.0f;
            if (ratio >= 0.75f) return 0.9f;
            if (ratio >= 0.5f) return 0.7f;
            if (ratio >= 0.25f) return 0.5f;
            return 0.3f;
        }

        public void ApplySupplyPenalty(UnitState unit, float supplyModifier)
        {
            if (supplyModifier < 1.0f)
            {
                int orgRecovery = (int)(5 * supplyModifier);
                unit.RecoverOrganization(orgRecovery);
            }
            if (supplyModifier < 0.7f)
            {
                unit.movesLeft = (int)(unit.movesLeft * supplyModifier);
            }
        }
    }
}

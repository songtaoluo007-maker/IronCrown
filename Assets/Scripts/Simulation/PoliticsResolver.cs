// ============================================================================
// Simulation/PoliticsResolver.cs — 政治结算器
// B1.5: 注入 IConfigRegistry，稳定修正取自 economy.json
// ============================================================================

using System;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class PoliticsResolver
    {
        private readonly IConfigRegistry _config;

        public PoliticsResolver(IConfigRegistry config)
        {
            _config = config;
        }

        public void ResolvePolitics(CountryState country, WorldState world)
        {
            ResolveStability(country);
            ResolveWarSupport(country, world);
            ResolveCorruption(country);
            ResolveLegitimacy(country);
        }

        private void ResolveStability(CountryState country)
        {
            int baseRecovery = 1;
            int corruptionPenalty = country.corruption / 20;
            int policyMod = 0; // TODO: 遍历 activePolicies
            int change = baseRecovery - corruptionPenalty + policyMod;

            // B1.5: 税率/民生档位修正
            var eco = _config.Get<EconomyConfig>("global");
            if (eco != null)
            {
                int taxLv = Math.Clamp(country.taxLevel, 0, 2);
                int civLv = Math.Clamp(country.civilLevel, 0, 2);
                if (eco.taxStabilityDeltas != null && eco.taxStabilityDeltas.Length > taxLv)
                    change += eco.taxStabilityDeltas[taxLv];
                if (eco.civilStabilityDeltas != null && eco.civilStabilityDeltas.Length > civLv)
                    change += eco.civilStabilityDeltas[civLv];
            }

            country.stability = Clamp(country.stability + change, 0, 100);
        }

        private void ResolveWarSupport(CountryState country, WorldState world)
        {
            int change = 0;
            // TODO: 检查是否在战争中
            country.warSupport = Clamp(country.warSupport + change, 0, 100);
        }

        private void ResolveCorruption(CountryState country)
        {
            int corruptionGrowth = System.Math.Max(0, 2 - country.bureaucracy / 30);
            country.corruption = Clamp(country.corruption + corruptionGrowth, 0, 100);
        }

        private void ResolveLegitimacy(CountryState country)
        {
            int change = country.stability > 60 ? 1 : (country.stability < 30 ? -1 : 0);
            country.legitimacy = Clamp(country.legitimacy + change, 0, 100);
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}

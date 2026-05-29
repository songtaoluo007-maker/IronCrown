// ============================================================================
// Simulation/ConstructionResolver.cs — 建造结算
// 每回合结算阶段推进建造进度，完工则 +1 工厂
// ============================================================================

using System.Collections.Generic;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class ConstructionResolver
    {
        /// <summary>尝试建造（含资源校验+扣费+入队）</summary>
        public bool TryBuild(CountryState c, string kind, EconomyConfig eco)
        {
            int cost = kind == "civilian" ? eco.civilianFactoryBuildCost : eco.militaryFactoryBuildCost;
            if (c.GetResource("capital") < cost) return false;
            c.ModifyResource("capital", -cost);
            EnqueueBuild(c, kind, eco);
            return true;
        }

        /// <summary>入队建造（调用方已校验资源充足并扣费）</summary>
        public void EnqueueBuild(CountryState country, string factoryKind, EconomyConfig eco)
        {
            country.constructionQueue.Add(new ConstructionOrder
            {
                factoryKind = factoryKind,
                turnsRemaining = eco.factoryBuildTurns
            });
        }

        /// <summary>结算阶段：推进建造进度，完工则 +1 工厂</summary>
        public void ResolveConstruction(CountryState country)
        {
            var completed = new List<ConstructionOrder>();

            foreach (var order in country.constructionQueue)
            {
                order.turnsRemaining--;
                if (order.turnsRemaining <= 0)
                {
                    if (order.factoryKind == "civilian")
                        country.civilianFactories++;
                    else if (order.factoryKind == "military")
                        country.militaryFactories++;

                    completed.Add(order);
                }
            }

            foreach (var done in completed)
                country.constructionQueue.Remove(done);
        }
    }
}

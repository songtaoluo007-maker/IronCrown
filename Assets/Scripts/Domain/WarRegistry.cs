// ============================================================================
// Domain/WarRegistry.cs — 战争状态静态工具
// 纯数据操作，无副作用，无 IEventPublisher
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace IronCrown.Domain
{
    public static class WarRegistry
    {
        /// <summary>双方是否处于战争状态（双向对称）</summary>
        public static bool AreAtWar(WorldState world, string a, string b)
        {
            var (lo, hi) = Normalize(a, b);
            return world.warRelations.Any(w =>
                string.Equals(w.countryA, lo, StringComparison.Ordinal) &&
                string.Equals(w.countryB, hi, StringComparison.Ordinal));
        }

        /// <summary>尝试宣战。已 AtWar 则幂等返回 false。</summary>
        public static bool TryDeclareWar(WorldState world, string a, string b, int currentTurn, out WarRelation declared)
        {
            declared = null;
            var (lo, hi) = Normalize(a, b);

            // 已存在
            if (world.warRelations.Any(w =>
                string.Equals(w.countryA, lo, StringComparison.Ordinal) &&
                string.Equals(w.countryB, hi, StringComparison.Ordinal)))
                return false;

            declared = new WarRelation
            {
                countryA = lo,
                countryB = hi,
                startTurn = currentTurn
            };
            world.warRelations.Add(declared);
            world.warRelations.Sort((x, y) =>
            {
                int cmp = string.Compare(x.countryA, y.countryA, StringComparison.Ordinal);
                return cmp != 0 ? cmp : string.Compare(x.countryB, y.countryB, StringComparison.Ordinal);
            });
            return true;
        }

        private static (string lo, string hi) Normalize(string a, string b)
        {
            return string.Compare(a, b, StringComparison.Ordinal) <= 0 ? (a, b) : (b, a);
        }

        /// <summary>尝试结束战争。移除一对 WarRelation。</summary>
        public static bool TryEndWar(WorldState world, string a, string b, out WarRelation removed)
        {
            removed = null;
            var (lo, hi) = Normalize(a, b);
            for (int i = 0; i < world.warRelations.Count; i++)
            {
                var w = world.warRelations[i];
                if (string.Equals(w.countryA, lo, StringComparison.Ordinal) &&
                    string.Equals(w.countryB, hi, StringComparison.Ordinal))
                {
                    removed = w;
                    world.warRelations.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}

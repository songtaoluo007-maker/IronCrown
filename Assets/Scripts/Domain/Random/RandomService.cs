// ============================================================================
// Domain/Random/RandomService.cs — SplitMix64 确定性随机数
// 可序列化状态，用于存档精确续跑
// ============================================================================

using System;

namespace IronCrown.Domain
{
    /// <summary>
    /// 基于 SplitMix64 的确定性随机数生成器。
    /// 同一种子 + 同一调用序列 = 同一结果。
    /// 状态可序列化（State/RestoreState），用于存档续跑。
    /// </summary>
    public sealed class RandomService : IRandom
    {
        private int _seed;
        private ulong _state;

        public int Seed => _seed;
        public ulong State => _state;

        public RandomService(int seed)
        {
            _seed = seed;
            _state = unchecked((ulong)seed);
        }

        public void Reset()
        {
            _state = unchecked((ulong)_seed);
        }

        public void Reset(int newSeed)
        {
            _seed = newSeed;
            _state = unchecked((ulong)newSeed);
        }

        public void RestoreState(ulong state)
        {
            _state = state;
        }

        private ulong NextRaw()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be > 0");
            return (int)(NextRaw() % (ulong)maxExclusive);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        public bool Roll(int percentChance)
        {
            return Next(100) < percentChance;
        }

        public double NextDouble()
        {
            return (NextRaw() >> 11) * (1.0 / (1UL << 53));
        }

        public double RangeDouble(double min, double max)
        {
            return min + NextDouble() * (max - min);
        }
    }
}

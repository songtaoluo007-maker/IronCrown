// ============================================================================
// Domain/Random/RandomService.cs — 确定性随机数（实现 IRandom）
// 从 Core/RandomService.cs 迁移，方法体不变
// ============================================================================

using System;

namespace IronCrown.Domain
{
    /// <summary>
    /// 基于种子的确定性随机数生成器。
    /// 同一种子 + 同一调用序列 = 同一结果，用于存档一致性验证和 AI 复盘。
    /// </summary>
    public sealed class RandomService : IRandom
    {
        private Random _rng;
        private int _seed;

        public int Seed => _seed;

        public RandomService(int seed)
        {
            _seed = seed;
            _rng = new Random(seed);
        }

        public int Next(int max) => _rng.Next(max);
        public int Range(int min, int max) => _rng.Next(min, max);
        public double NextDouble() => _rng.NextDouble();
        public double RangeDouble(double min, double max) => min + _rng.NextDouble() * (max - min);
        public bool Roll(int percentChance) => _rng.Next(100) < percentChance;

        public void Reset()
        {
            _rng = new Random(_seed);
        }

        public void Reset(int newSeed)
        {
            _seed = newSeed;
            _rng = new Random(newSeed);
        }
    }
}

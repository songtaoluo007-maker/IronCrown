// ============================================================================
// Domain/Abstractions/IRandom.cs — 随机数接口
// ============================================================================

namespace IronCrown.Domain
{
    public interface IRandom
    {
        int Seed { get; }
        int Next(int maxExclusive);
        int Range(int minInclusive, int maxExclusive);
        bool Roll(int percentChance);    // 0-100
        double NextDouble();
        double RangeDouble(double min, double max);
        void Reset();
        void Reset(int newSeed);
    }
}

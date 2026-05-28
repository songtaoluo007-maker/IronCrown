// ============================================================================
// Tests/EditMode/RandomServiceTests.cs — RandomService 单元测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;

namespace IronCrown.Domain.Tests
{
    public class RandomServiceTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var rng1 = new RandomService(42);
            var rng2 = new RandomService(42);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(rng1.Next(1000), rng2.Next(1000));
            }
        }

        [Test]
        public void Reset_RestartsSequence()
        {
            var rng = new RandomService(123);
            var first = rng.Next(1000);
            var second = rng.Next(1000);

            rng.Reset();
            Assert.AreEqual(first, rng.Next(1000));
            Assert.AreEqual(second, rng.Next(1000));
        }

        [Test]
        public void Reset_NewSeed_NewSequence()
        {
            var rng = new RandomService(100);
            var oldFirst = rng.Next(1000);

            rng.Reset(200);
            var newFirst = rng.Next(1000);

            Assert.AreNotEqual(oldFirst, newFirst);
        }

        [Test]
        public void Range_InBounds()
        {
            var rng = new RandomService(7);
            for (int i = 0; i < 1000; i++)
            {
                int val = rng.Range(10, 20);
                Assert.GreaterOrEqual(val, 10);
                Assert.Less(val, 20);
            }
        }

        [Test]
        public void Roll_PercentZero_AlwaysFalse()
        {
            var rng = new RandomService(1);
            Assert.IsFalse(rng.Roll(0));
        }

        [Test]
        public void Roll_PercentHundred_AlwaysTrue()
        {
            var rng = new RandomService(1);
            Assert.IsTrue(rng.Roll(100));
        }

        [Test]
        public void StateRestore_ProducesSameSequence()
        {
            var rng = new RandomService(42);

            // Advance some steps
            for (int i = 0; i < 10; i++) rng.Next(1000);

            // Save state
            ulong savedState = rng.State;

            // Advance more
            var afterSave = new int[5];
            for (int i = 0; i < 5; i++) afterSave[i] = rng.Next(1000);

            // Restore and re-advance
            rng.RestoreState(savedState);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(afterSave[i], rng.Next(1000));
            }
        }
    }
}

// ============================================================================
// Tests/OccupationResolverTests.cs — C6 占领抵抗测试
// ============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Tests
{
    [TestFixture]
    public class OccupationResolverTests
    {
        private EconomyConfig _eco;
        private TestRngC6 _rng;

        [SetUp]
        public void SetUp()
        {
            _eco = new EconomyConfig
            {
                resistanceOnCapture = 50,
                resistanceDecayWithGarrison = -2,
                resistanceGrowWithoutGarrison = 1,
                resistanceUprisingThreshold = 30,
                resistanceUprisingChancePct = 10,
                resistanceGarrisonDamageManpower = 5,
                resistanceGarrisonDamageEquipment = 5
            };
            _rng = new TestRngC6();
        }

        // ── resistance 增减 ──

        [Test]
        public void WithGarrison_ResistanceDecaysBy2()
        {
            var world = CreateWorld(out var province, out var unit);
            province.resistance = 50;
            unit.currentProvinceId = province.id; // 驻军在省

            var resolver = CreateResolver();
            resolver.ResolveOccupation(world, _eco);

            Assert.AreEqual(48, province.resistance);
        }

        [Test]
        public void WithoutGarrison_ResistanceGrowsBy1()
        {
            var world = CreateWorld(out var province, out var unit);
            province.resistance = 50;
            unit.currentProvinceId = "other_province"; // 驻军不在省

            // 诊断
            Assert.IsTrue(province.IsOccupied, "省份应被占领");
            Assert.AreEqual("occupier", province.controllerCountry);

            var resolver = CreateResolver();
            resolver.ResolveOccupation(world, _eco);

            Assert.AreEqual(51, province.resistance);
        }

        [Test]
        public void WithGarrison_ResistanceClampedAt0()
        {
            var world = CreateWorld(out var province, out var unit);
            province.resistance = 1; // 1 + (-2) = -1 → clamp 0
            unit.currentProvinceId = province.id;

            var resolver = CreateResolver();
            resolver.ResolveOccupation(world, _eco);

            Assert.AreEqual(0, province.resistance);
        }

        [Test]
        public void WithoutGarrison_ResistanceClampedAt100()
        {
            var world = CreateWorld(out var province, out var unit);
            province.resistance = 100;
            unit.currentProvinceId = "other";

            var resolver = CreateResolver();
            resolver.ResolveOccupation(world, _eco);

            Assert.AreEqual(100, province.resistance);
        }

        [Test]
        public void NonOccupied_ProvinceSkipped()
        {
            var world = CreateWorld(out var province, out _);
            province.resistance = 0;
            province.controllerCountry = province.ownerCountry; // 非占领

            var resolver = CreateResolver();
            resolver.ResolveOccupation(world, _eco);

            Assert.AreEqual(0, province.resistance); // 无变化
        }

        // ── 起义事件 ──

        [Test]
        public void Uprising_WithGarrison_DamagesGarrison()
        {
            var world = CreateWorld(out var province, out var unit);
            province.resistance = 50; // >= 30 阈值
            unit.currentProvinceId = province.id;
            unit.manpower = 100;
            unit.equipment = 100;

            _rng.ForceNext(5); // 5 < 10% → 触发

            var events = new EventRecorderC6();
            var resolver = new OccupationResolver(events, _rng);
            resolver.ResolveOccupation(world, _eco);

            Assert.AreEqual(95, unit.manpower);  // -5
            Assert.AreEqual(95, unit.equipment); // -5
            Assert.AreEqual(1, events.Uprisings.Count);
            Assert.AreEqual("garrison_damage", events.Uprisings[0].result);
            Assert.AreEqual(province.id, events.Uprisings[0].provinceId);
        }

        [Test]
        public void Uprising_WithoutGarrison_LiberatesProvince()
        {
            var world = CreateWorld(out var province, out var unit);
            province.resistance = 50;
            unit.currentProvinceId = "other"; // 无驻军

            _rng.ForceNext(5); // 触发

            var events = new EventRecorderC6();
            var resolver = new OccupationResolver(events, _rng);
            resolver.ResolveOccupation(world, _eco);

            Assert.AreEqual(province.ownerCountry, province.controllerCountry); // 回归原主
            Assert.AreEqual(0, province.resistance); // 重置
            Assert.AreEqual(1, events.Uprisings.Count);
            Assert.AreEqual("liberated", events.Uprisings[0].result);
        }

        [Test]
        public void Uprising_NoTrigger_BelowThreshold()
        {
            var world = CreateWorld(out var province, out var unit);
            province.resistance = 29; // < 30 阈值
            unit.currentProvinceId = province.id;

            _rng.ForceNext(5); // 即使 roll < 10%，阈值不够

            var events = new EventRecorderC6();
            var resolver = new OccupationResolver(events, _rng);
            resolver.ResolveOccupation(world, _eco);

            Assert.AreEqual(0, events.Uprisings.Count);
        }

        [Test]
        public void Uprising_NoTrigger_RollTooHigh()
        {
            var world = CreateWorld(out var province, out var unit);
            province.resistance = 50;
            unit.currentProvinceId = province.id;

            _rng.ForceNext(15); // 15 >= 10% → 不触发

            var events = new EventRecorderC6();
            var resolver = new OccupationResolver(events, _rng);
            resolver.ResolveOccupation(world, _eco);

            Assert.AreEqual(0, events.Uprisings.Count);
        }

        [Test]
        public void GarrisonDamage_MultipleUnits_SpreadEvenly()
        {
            var world = CreateWorld(out var province, out var unit1);
            province.resistance = 50;
            unit1.currentProvinceId = province.id;
            unit1.manpower = 100;
            unit1.equipment = 100;

            // 添加第二个驻军单位
            var unit2 = new UnitState
            {
                id = "garrison_2",
                ownerCountry = "occupier",
                currentProvinceId = province.id,
                manpower = 100,
                equipment = 100
            };
            world.units["garrison_2"] = unit2;

            _rng.ForceNext(5); // 触发

            var resolver = CreateResolver();
            resolver.ResolveOccupation(world, _eco);

            // 5 / 2 = 2 per unit (integer division)
            Assert.AreEqual(98, unit1.manpower);
            Assert.AreEqual(98, unit1.equipment);
            Assert.AreEqual(98, unit2.manpower);
            Assert.AreEqual(98, unit2.equipment);
        }

        // ── 辅助 ──

        private OccupationResolver CreateResolver()
        {
            return new OccupationResolver(new NoOpEventPublisherC6(), _rng);
        }

        private WorldState CreateWorld(out ProvinceState province, out UnitState garrison)
        {
            var owner = new CountryState { id = "owner_country", name = "Owner" };
            var occupier = new CountryState { id = "occupier", name = "Occupier" };

            province = new ProvinceState
            {
                id = "P1",
                name = "TestProvince",
                ownerCountry = "owner_country",
                controllerCountry = "occupier", // 被占领
                resistance = 0,
                compliance = 0,
                neighbors = new[] { "P2" }
            };

            garrison = new UnitState
            {
                id = "garrison_1",
                ownerCountry = "occupier",
                currentProvinceId = "P1",
                manpower = 100,
                equipment = 100
            };

            var world = new WorldState
            {
                countries = new Dictionary<string, CountryState>
                {
                    { "owner_country", owner },
                    { "occupier", occupier }
                },
                provinces = new Dictionary<string, ProvinceState> { { "P1", province } },
                units = new Dictionary<string, UnitState> { { "garrison_1", garrison } }
            };

            return world;
        }
    }

    // ── 测试辅助类 ──

    internal class TestRngC6 : IRandom
    {
        private int _forcedValue = -1;
        public int Seed => 0;
        public ulong State { get; set; }
        public void ForceNext(int value) { _forcedValue = value; }
        public int Next(int maxExclusive) => _forcedValue >= 0 ? _forcedValue : 0;
        public int Range(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);
        public bool Roll(int percentChance) => Next(100) < percentChance;
        public double NextDouble() => _forcedValue >= 0 ? _forcedValue / 100.0 : 0;
        public double RangeDouble(double min, double max) => min + NextDouble() * (max - min);
        public void Reset() { }
        public void Reset(int newSeed) { }
        public void RestoreState(ulong state) { }
    }

    internal class NoOpEventPublisherC6 : IEventPublisher
    {
        public void Publish<T>(T evt) { }
        public void Subscribe<T>(Action<T> handler) { }
        public void Unsubscribe<T>(Action<T> handler) { }
        public void Clear() { }
    }

    internal class EventRecorderC6 : IEventPublisher
    {
        public List<ResistanceUprisingEvent> Uprisings = new();
        public void Publish<T>(T evt)
        {
            if (evt is ResistanceUprisingEvent e) Uprisings.Add(e);
        }
        public void Subscribe<T>(Action<T> handler) { }
        public void Unsubscribe<T>(Action<T> handler) { }
        public void Clear() { }
    }
}

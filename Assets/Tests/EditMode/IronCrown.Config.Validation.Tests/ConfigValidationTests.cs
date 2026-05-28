// ============================================================================
// ConfigValidationTests.cs — 配置表完整性校验
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IronCrown.Domain;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace IronCrown.Config.Validation.Tests
{
    /// <summary>配置表校验测试</summary>
    [TestFixture]
    public class ConfigValidationTests
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        };

        private List<ResourceConfig> _resources;
        private List<UnitConfig> _units;
        private List<CountryConfig> _countries;
        private List<ProvinceConfig> _provinces;

        [OneTimeSetUp]
        public void LoadAll()
        {
            _resources = LoadConfig<ResourceConfig>("resources");
            _units = LoadConfig<UnitConfig>("units");
            _countries = LoadConfig<CountryConfig>("countries");
            _provinces = LoadConfig<ProvinceConfig>("provinces");
        }

        // ========== P5.1 — schemaVersion == 1, items 非空 ==========

        [Test] public void Resources_HasItems() => Assert.IsTrue(_resources.Count > 0, "resources.json items 为空");
        [Test] public void Units_HasItems() => Assert.IsTrue(_units.Count > 0, "units.json items 为空");
        [Test] public void Countries_HasItems() => Assert.IsTrue(_countries.Count > 0, "countries.json items 为空");
        [Test] public void Provinces_HasItems() => Assert.IsTrue(_provinces.Count > 0, "provinces.json items 为空");

        // ========== P5.2 — 唯一 id ==========

        [Test] public void Resources_UniqueIds() => AssertUniqueIds(_resources.Select(r => r.id), "resources");
        [Test] public void Units_UniqueIds() => AssertUniqueIds(_units.Select(u => u.id), "units");
        [Test] public void Countries_UniqueIds() => AssertUniqueIds(_countries.Select(c => c.id), "countries");
        [Test] public void Provinces_UniqueIds() => AssertUniqueIds(_provinces.Select(p => p.id), "provinces");

        // ========== P5.3 — 枚举可解析 ==========

        [Test]
        public void Countries_IdeologyValid()
        {
            foreach (var c in _countries)
                Assert.DoesNotThrow(() => Enum.Parse<Ideology>(c.ideology),
                    $"国家 {c.id} 的 ideology '{c.ideology}' 不是合法 Ideology 枚举");
        }

        [Test]
        public void Provinces_TerrainValid()
        {
            foreach (var p in _provinces)
                Assert.DoesNotThrow(() => Enum.Parse<TerrainType>(p.terrain),
                    $"省份 {p.id} 的 terrain '{p.terrain}' 不是合法 TerrainType 枚举");
        }

        // ========== P5.4 — 外键完整 ==========

        [Test]
        public void Units_CostKeysExistInResources()
        {
            var resourceIds = _resources.Select(r => r.id).ToHashSet();
            foreach (var u in _units)
            {
                if (u.cost == null) continue;
                foreach (var key in u.cost.Keys)
                    Assert.IsTrue(resourceIds.Contains(key),
                        $"单位 {u.id} 的 cost 引用了不存在的资源 '{key}'");
            }
        }

        [Test]
        public void Countries_ResourcesKeysExistInResources()
        {
            var resourceIds = _resources.Select(r => r.id).ToHashSet();
            foreach (var c in _countries)
            {
                if (c.resources == null) continue;
                foreach (var key in c.resources.Keys)
                    Assert.IsTrue(resourceIds.Contains(key),
                        $"国家 {c.id} 的 resources 引用了不存在的资源 '{key}'");
            }
        }

        [Test]
        public void Countries_CapitalProvinceExists()
        {
            var provinceIds = _provinces.Select(p => p.id).ToHashSet();
            foreach (var c in _countries)
                Assert.IsTrue(provinceIds.Contains(c.capitalProvinceId),
                    $"国家 {c.id} 的 capitalProvinceId '{c.capitalProvinceId}' 不存在于 provinces 中");
        }

        [Test]
        public void Provinces_OwnerCountryExists()
        {
            var countryIds = _countries.Select(c => c.id).ToHashSet();
            foreach (var p in _provinces)
                Assert.IsTrue(countryIds.Contains(p.ownerCountry),
                    $"省份 {p.id} 的 ownerCountry '{p.ownerCountry}' 不存在于 countries 中");
        }

        [Test]
        public void Provinces_ResourceOutputExistsInResources()
        {
            var resourceIds = _resources.Select(r => r.id).ToHashSet();
            foreach (var p in _provinces)
            {
                if (p.resourceOutput == null) continue;
                foreach (var resId in p.resourceOutput)
                    Assert.IsTrue(resourceIds.Contains(resId),
                        $"省份 {p.id} 的 resourceOutput 引用了不存在的资源 '{resId}'");
            }
        }

        // ========== P5.5 — 必填非空 ==========

        [Test]
        public void AllTables_IdAndNameNotEmpty()
        {
            foreach (var r in _resources)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(r.id), $"resources: 某条 id 为空");
                Assert.IsFalse(string.IsNullOrWhiteSpace(r.name), $"resources: {r.id} 的 name 为空");
            }
            foreach (var u in _units)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(u.id), $"units: 某条 id 为空");
                Assert.IsFalse(string.IsNullOrWhiteSpace(u.name), $"units: {u.id} 的 name 为空");
            }
            foreach (var c in _countries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.id), $"countries: 某条 id 为空");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.name), $"countries: {c.id} 的 name 为空");
            }
            foreach (var p in _provinces)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(p.id), $"provinces: 某条 id 为空");
                Assert.IsFalse(string.IsNullOrWhiteSpace(p.name), $"provinces: {p.id} 的 name 为空");
            }
        }

        // ========== P5.6 — 数值范围 ==========

        [Test]
        public void Countries_ClampedValues()
        {
            foreach (var c in _countries)
            {
                Assert.That(c.stability, Is.InRange(0, 100), $"{c.id} stability 越界");
                Assert.That(c.warSupport, Is.InRange(0, 100), $"{c.id} warSupport 越界");
                Assert.That(c.legitimacy, Is.InRange(0, 100), $"{c.id} legitimacy 越界");
                Assert.That(c.corruption, Is.InRange(0, 100), $"{c.id} corruption 越界");
                Assert.That(c.bureaucracy, Is.InRange(0, 100), $"{c.id} bureaucracy 越界");
                Assert.That(c.civilianFactories, Is.GreaterThanOrEqualTo(0), $"{c.id} civilianFactories < 0");
                Assert.That(c.militaryFactories, Is.GreaterThanOrEqualTo(0), $"{c.id} militaryFactories < 0");
                Assert.That(c.dockyards, Is.GreaterThanOrEqualTo(0), $"{c.id} dockyards < 0");
                Assert.That(c.treasury, Is.GreaterThanOrEqualTo(0), $"{c.id} treasury < 0");
                Assert.That(c.manpower, Is.GreaterThanOrEqualTo(0), $"{c.id} manpower < 0");
                Assert.That(c.totalManpower, Is.GreaterThanOrEqualTo(0), $"{c.id} totalManpower < 0");
            }
        }

        // ========== Helper ==========

        private static List<T> LoadConfig<T>(string name)
        {
            var path = Path.Combine(Application.dataPath, "StreamingAssets", "Configs", "Json", name + ".json");
            Assert.IsTrue(File.Exists(path), $"配置文件不存在: {path}");
            var json = File.ReadAllText(path);
            var wrapper = JsonConvert.DeserializeObject<ConfigFile<T>>(json, Settings);
            Assert.IsNotNull(wrapper, $"{name}.json 反序列化失败");
            Assert.AreEqual(1, wrapper.schemaVersion, $"{name}.json schemaVersion != 1");
            Assert.IsNotNull(wrapper.items, $"{name}.json items 为 null");
            return wrapper.items;
        }

        private static void AssertUniqueIds(IEnumerable<string> ids, string tableName)
        {
            var list = ids.ToList();
            var dupes = list.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.AreEqual(0, dupes.Count,
                $"{tableName} 存在重复 id: {string.Join(", ", dupes)}");
        }
    }
}

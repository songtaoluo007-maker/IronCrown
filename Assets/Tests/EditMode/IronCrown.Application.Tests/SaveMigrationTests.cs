// ============================================================================
// Tests/EditMode/SaveMigrationTests.cs — P2.0b 存档迁移框架测试
// 验证：schemaVersion 写入/读取、Runner 链式升级、Load 流程集成、旧行兼容
// ============================================================================

using NUnit.Framework;
using IronCrown.Application;
using IronCrown.Domain;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace IronCrown.Application.Tests
{
    public class SaveMigrationTests
    {
        // ================================================================
        // 辅助：构建最小 JSON 存档
        // ================================================================

        private static JObject MakeSaveJson(int? schemaVersion = null, int turnNumber = 1)
        {
            var json = new JObject
            {
                ["turnNumber"] = turnNumber,
                ["seed"] = 12345,
                ["rngState"] = 0,
                ["phase"] = "TurnStart",
                ["playerCountryId"] = "empire_north"
            };
            if (schemaVersion.HasValue)
                json["schemaVersion"] = schemaVersion.Value;
            return json;
        }

        // ================================================================
        // Test 1: OldSaveNoVersion_DefaultsToV1_LoadsOK
        // ================================================================

        [Test]
        public void OldSaveNoVersion_DefaultsToV1_LoadsOK()
        {
            // 构造无 schemaVersion 的旧 JSON
            var raw = MakeSaveJson(schemaVersion: null);

            // 经 Runner 升级
            var runner = new SaveMigrationRunner(new ISaveMigration[] { new Migration_0to1() });
            var upgraded = runner.Upgrade(raw);

            // schemaVersion 应被补为 1
            Assert.AreEqual(1, upgraded["schemaVersion"]!.Value<int>(),
                "无 schemaVersion 的旧档应被补为 1");

            // 反序列化为 GameState 成功
            var state = upgraded.ToObject<GameState>();
            Assert.IsNotNull(state, "升级后反序列化应成功");
            Assert.AreEqual(1, state.turnNumber, "turnNumber 应保留");
            Assert.AreEqual(12345, state.seed, "seed 应保留");
            Assert.AreEqual("empire_north", state.playerCountryId, "playerCountryId 应保留");
        }

        // ================================================================
        // Test 2: CurrentVersion_NoMigration
        // ================================================================

        [Test]
        public void CurrentVersion_NoMigration()
        {
            // 当前版本档
            var raw = MakeSaveJson(schemaVersion: SaveSchema.CURRENT);

            var runner = new SaveMigrationRunner(new ISaveMigration[] { new Migration_0to1() });
            var upgraded = runner.Upgrade(raw);

            // schemaVersion 不变
            Assert.AreEqual(SaveSchema.CURRENT, upgraded["schemaVersion"]!.Value<int>(),
                "当前版本档不应被修改");

            // 内容不变
            Assert.AreEqual(1, upgraded["turnNumber"]!.Value<int>());
        }

        // ================================================================
        // Test 3: MigrationChain_RunsInOrder
        // ================================================================

        [Test]
        public void MigrationChain_RunsInOrder()
        {
            // 注册 3 个假迁移器：v1→v2→v3
            var migrations = new ISaveMigration[]
            {
                new FakeMigration(1, "migrated_1to2"),
                new FakeMigration(2, "migrated_2to3"),
                new FakeMigration(3, "migrated_3to4")
            };

            // 从 v1 档开始
            var raw = MakeSaveJson(schemaVersion: 1);
            // 注入 targetVersion=4，验证纯链式逻辑（独立于 SaveSchema.CURRENT）
            var runner = new SaveMigrationRunner(migrations, targetVersion: 4);
            var upgraded = runner.Upgrade(raw);

            // 应链式跑到 v4
            Assert.AreEqual(4, upgraded["schemaVersion"]!.Value<int>(),
                "应链式升级到 targetVersion=4");

            // 验证每步迁移都执行了（通过自定义字段）
            Assert.IsTrue(upgraded.ContainsKey("migrated_1to2"), "v1→v2 迁移应执行");
            Assert.IsTrue(upgraded.ContainsKey("migrated_2to3"), "v2→v3 迁移应执行");
            Assert.IsTrue(upgraded.ContainsKey("migrated_3to4"), "v3→v4 迁移应执行");
        }

        // ================================================================
        // Test 4: Migrated_ThenToRuntime_OK
        // ================================================================

        [Test]
        public void Migrated_ThenToRuntime_OK()
        {
            // 构造含真实数据的旧档（无 schemaVersion）
            var raw = new JObject
            {
                ["turnNumber"] = 5,
                ["seed"] = 42,
                ["rngState"] = 123UL,
                ["phase"] = "TurnStart",
                ["playerCountryId"] = "empire_north",
                ["countries"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "empire_north",
                        ["name"] = "北境帝国",
                        ["treasury"] = 500,
                        ["stability"] = 70,
                        ["warSupport"] = 50,
                        ["equipmentStockpile"] = 200,
                        ["taxLevel"] = 1,
                        ["civilLevel"] = 1,
                        ["warExhaustion"] = 10,
                        ["civilianFactories"] = 2,
                        ["militaryFactories"] = 1,
                        ["dockyards"] = 0,
                        ["manpower"] = 100000,
                        ["totalManpower"] = 200000,
                        ["gachaTickets"] = 10,
                        ["gachaPityCounter"] = 0
                    }
                },
                ["provinces"] = new JArray(),
                ["units"] = new JArray(),
                ["commanders"] = new JArray(),
                ["activeBattles"] = new JArray(),
                ["warRelations"] = new JArray(),
                ["truces"] = new JArray()
            };

            // 经迁移
            var runner = new SaveMigrationRunner(new ISaveMigration[] { new Migration_0to1() });
            var upgraded = runner.Upgrade(raw);
            Assert.AreEqual(1, upgraded["schemaVersion"]!.Value<int>());

            // 反序列化为 GameState
            var state = upgraded.ToObject<GameState>();
            Assert.IsNotNull(state);
            Assert.AreEqual(5, state.turnNumber);
            Assert.AreEqual(42, state.seed);
            Assert.AreEqual("empire_north", state.playerCountryId);

            // 经 SaveMapper.ToRuntime 重建 WorldState
            var world = SaveMapper.ToRuntime(state);
            Assert.IsNotNull(world);
            Assert.AreEqual(5, world.turnNumber);
            Assert.IsTrue(world.countries.ContainsKey("empire_north"));
            Assert.AreEqual(500, world.countries["empire_north"].treasury);
        }

        // ================================================================
        // Test 5: SaveMapper_ToSave_WritesSchemaVersion
        // ================================================================

        [Test]
        public void SaveMapper_ToSave_WritesSchemaVersion()
        {
            var world = new WorldState();
            var save = SaveMapper.ToSave(world, 42, 0, GamePhase.TurnStart);

            Assert.AreEqual(SaveSchema.CURRENT, save.schemaVersion,
                "SaveMapper.ToSave 应写入 schemaVersion = CURRENT");
        }

        // ================================================================
        // Test 6: Existing SaveLoadEquivalenceTests 不破（间接验证）
        // 本测试验证 Migration_0to1 对已含 schemaVersion=1 的档无副作用
        // ================================================================

        [Test]
        public void AlreadyV1_NoSideEffects()
        {
            var raw = MakeSaveJson(schemaVersion: 1, turnNumber: 10);
            var originalTurn = raw["turnNumber"]!.Value<int>();

            var runner = new SaveMigrationRunner(new ISaveMigration[] { new Migration_0to1() });
            var upgraded = runner.Upgrade(raw);

            Assert.AreEqual(1, upgraded["schemaVersion"]!.Value<int>());
            Assert.AreEqual(originalTurn, upgraded["turnNumber"]!.Value<int>(),
                "已含 schemaVersion 的档不应被修改其他字段");
        }

        // ================================================================
        // Test 7: Migration_1to2_GeneratesTiles
        // ================================================================

        [Test]
        public void Migration_1to2_GeneratesTiles()
        {
            // 构造 v1 档（有 2 个省，无 tiles）
            var raw = new JObject
            {
                ["schemaVersion"] = 1,
                ["turnNumber"] = 3,
                ["seed"] = 42,
                ["rngState"] = 0,
                ["phase"] = "TurnStart",
                ["countries"] = new JArray(),
                ["provinces"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "prov_a",
                        ["name"] = "A",
                        ["gridX"] = 0,
                        ["gridY"] = 0,
                        ["terrain"] = "Plain"
                    },
                    new JObject
                    {
                        ["id"] = "prov_b",
                        ["name"] = "B",
                        ["gridX"] = 1,
                        ["gridY"] = 0,
                        ["terrain"] = "Forest"
                    }
                },
                ["units"] = new JArray(),
                ["commanders"] = new JArray(),
                ["activeBattles"] = new JArray(),
                ["warRelations"] = new JArray(),
                ["truces"] = new JArray()
            };

            var runner = new SaveMigrationRunner(new ISaveMigration[] { new Migration_0to1(), new Migration_1to2() });
            var upgraded = runner.Upgrade(raw);

            // schemaVersion 应为 2
            Assert.AreEqual(2, upgraded["schemaVersion"]!.Value<int>());

            // tiles 应有 8 个 (2省×4格)
            var tiles = upgraded["tiles"] as JArray;
            Assert.IsNotNull(tiles);
            Assert.AreEqual(8, tiles.Count);

            // prov_a 的 4 格坐标
            var provATiles = tiles.Where(t => t["provinceId"]?.ToString() == "prov_a").ToList();
            Assert.AreEqual(4, provATiles.Count);
            Assert.IsTrue(provATiles.Any(t => t["gridX"]!.Value<int>() == 0 && t["gridY"]!.Value<int>() == 0));
            Assert.IsTrue(provATiles.Any(t => t["gridX"]!.Value<int>() == 1 && t["gridY"]!.Value<int>() == 0));
            Assert.IsTrue(provATiles.Any(t => t["gridX"]!.Value<int>() == 0 && t["gridY"]!.Value<int>() == 1));
            Assert.IsTrue(provATiles.Any(t => t["gridX"]!.Value<int>() == 1 && t["gridY"]!.Value<int>() == 1));

            // 省的 tileIds 应回填
            var provA = upgraded["provinces"]!.First(p => p["id"]!.ToString() == "prov_a");
            var tileIds = provA["tileIds"] as JArray;
            Assert.IsNotNull(tileIds);
            Assert.AreEqual(4, tileIds.Count);
            Assert.IsTrue(tileIds.All(t => t!.ToString().StartsWith("prov_a_t")));

            // 格地形继承省地形
            foreach (var tile in provATiles)
            {
                Assert.AreEqual("Plain", tile["terrain"]!.ToString());
            }
        }

        // ================================================================
        // Test 8: Migration_1to2_ThenToRuntime_TilesExist
        // ================================================================

        [Test]
        public void Migration_1to2_ThenToRuntime_TilesExist()
        {
            // 构造含省份的 v1 完整档
            var raw = new JObject
            {
                ["schemaVersion"] = 1,
                ["turnNumber"] = 1,
                ["seed"] = 42,
                ["rngState"] = 0,
                ["phase"] = "TurnStart",
                ["playerCountryId"] = "empire",
                ["countries"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "empire",
                        ["name"] = "帝国",
                        ["treasury"] = 100,
                        ["stability"] = 70,
                        ["warSupport"] = 50,
                        ["equipmentStockpile"] = 100,
                        ["taxLevel"] = 1,
                        ["civilLevel"] = 1,
                        ["warExhaustion"] = 0,
                        ["civilianFactories"] = 1,
                        ["militaryFactories"] = 1,
                        ["dockyards"] = 0,
                        ["manpower"] = 50000,
                        ["totalManpower"] = 100000,
                        ["gachaTickets"] = 0,
                        ["gachaPityCounter"] = 0
                    }
                },
                ["provinces"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "prov_a",
                        ["name"] = "A",
                        ["gridX"] = 0,
                        ["gridY"] = 0,
                        ["terrain"] = "Plain",
                        ["ownerCountry"] = "empire",
                        ["controllerCountry"] = "empire",
                        ["population"] = 1000,
                        ["manpower"] = 500,
                        ["infrastructure"] = 1,
                        ["railwayLevel"] = 0,
                        ["portLevel"] = 0,
                        ["airBaseLevel"] = 0,
                        ["industrySlots"] = 1,
                        ["builtCivilianFactories"] = 0,
                        ["builtMilitaryFactories"] = 0,
                        ["resourceOutput"] = new JArray(),
                        ["resistance"] = 0,
                        ["compliance"] = 100,
                        ["victoryPoint"] = 0,
                        ["isCapital"] = false,
                        ["neighbors"] = new JArray()
                    }
                },
                ["units"] = new JArray(),
                ["commanders"] = new JArray(),
                ["activeBattles"] = new JArray(),
                ["warRelations"] = new JArray(),
                ["truces"] = new JArray()
            };

            // 迁移 v0→v1→v2
            var runner = new SaveMigrationRunner(new ISaveMigration[] { new Migration_0to1(), new Migration_1to2() });
            var upgraded = runner.Upgrade(raw);

            // 反序列化
            var state = upgraded.ToObject<GameState>();
            Assert.IsNotNull(state);
            Assert.AreEqual(2, state.schemaVersion);

            // 经 SaveMapper.ToRuntime
            var world = SaveMapper.ToRuntime(state);

            // 验证 tiles
            Assert.AreEqual(4, world.tiles.Count, "应有 4 个 tiles");
            Assert.IsTrue(world.tiles.ContainsKey("prov_a_t0"));
            Assert.IsTrue(world.tiles.ContainsKey("prov_a_t3"));

            // 验证 province.tileIds
            Assert.AreEqual(4, world.provinces["prov_a"].tileIds.Count);
        }

        // ================================================================
        // 辅助类
        // ================================================================

        /// <summary>假迁移器：给 JSON 加一个标记字段，schemaVersion +1</summary>
        private sealed class FakeMigration : ISaveMigration
        {
            private readonly string _marker;
            public int FromVersion { get; }

            public FakeMigration(int fromVersion, string marker)
            {
                FromVersion = fromVersion;
                _marker = marker;
            }

            public JObject Migrate(JObject raw)
            {
                raw[_marker] = true;
                raw["schemaVersion"] = FromVersion + 1;
                return raw;
            }
        }
    }
}

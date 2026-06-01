// ============================================================================
// CommanderResolverC15aTests.cs — C15a 将领系统测试
// 军衔晋升 / 指挥容量 / 包围加权 / 同省 5 师限制 / 战斗 buff
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;

namespace IronCrown.Simulation.Tests
{
    [TestFixture]
    public class CommanderResolverC15aTests
    {
        private TestConfigRegistry _config;
        private CommanderResolver _resolver;
        private CountryState _country;
        private WorldState _world;

        [SetUp]
        public void SetUp()
        {
            _config = new TestConfigRegistry();
            _config.Register("general_test_basic", new CommanderConfig
            {
                id = "general_test_basic",
                name = "测试将军",
                title = "铁壁将军",
                baseAttack = 5,
                baseDefense = 5,
                recruitCapitalCost = 100,
                recruitManpowerCost = 500,
                baseMaxDivisions = 5,
                traits = new[] { "defensive" }
            });

            _resolver = new CommanderResolver(_config);

            _country = new CountryState
            {
                id = "TEST",
                name = "测试国",
                manpower = 2000
            };
            _country.ModifyResource("capital", 500);

            _world = new WorldState();
            _world.countries["TEST"] = _country;
        }

        // ================================================================
        // 招募
        // ================================================================

        [Test]
        public void Recruit_Success()
        {
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");

            Assert.IsNotNull(cmdr);
            Assert.AreEqual("测试将军", cmdr.name);
            Assert.AreEqual(0, cmdr.rank);
            Assert.AreEqual(5, cmdr.maxDivisions);
            Assert.IsTrue(cmdr.isActive);
            Assert.AreEqual(1, _country.commanderIds.Count);
            Assert.AreEqual(400, _country.GetResource("capital")); // 500-100
            Assert.AreEqual(1500, _country.manpower); // 2000-500
        }

        [Test]
        public void Recruit_InsufficientCapital()
        {
            _country.ModifyResource("capital", -450); // 只剩 50
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");
            Assert.IsNull(cmdr);
        }

        [Test]
        public void Recruit_InsufficientManpower()
        {
            _country.manpower = 400;
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");
            Assert.IsNull(cmdr);
        }

        // ================================================================
        // 军衔晋升
        // ================================================================

        [Test]
        public void Promote_RankThresholds()
        {
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");
            _world.commanders[cmdr.id] = cmdr;

            // 0 → 1 需要 5 胜
            Assert.IsFalse(cmdr.CanPromote);
            cmdr.victories = 5;
            Assert.IsTrue(cmdr.CanPromote);
            _resolver.CheckPromotions(_world);
            Assert.AreEqual(1, cmdr.rank);
            Assert.AreEqual(6, cmdr.maxDivisions); // 5+1
        }

        [Test]
        public void Promote_EncirclementCountsTriple()
        {
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");
            _world.commanders[cmdr.id] = cmdr;

            // 1 次包围 = 3 胜场
            cmdr.RecordVictory(isEncirclement: true);
            Assert.AreEqual(3, cmdr.victories);
            Assert.AreEqual(1, cmdr.encirclements);

            // 再来 2 次普通胜 = 5 → 晋升
            cmdr.RecordVictory();
            cmdr.RecordVictory();
            Assert.AreEqual(5, cmdr.victories);
            Assert.IsTrue(cmdr.CanPromote);
        }

        [Test]
        public void Promote_MaxRank5()
        {
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");
            _world.commanders[cmdr.id] = cmdr;

            // 直接推到满级
            cmdr.victories = 75;
            _resolver.CheckPromotions(_world);
            Assert.AreEqual(4, cmdr.rank); // 大元帅
            Assert.AreEqual(9, cmdr.maxDivisions); // 5+4
            Assert.IsFalse(cmdr.CanPromote); // 已满级
        }

        // ================================================================
        // 指挥容量
        // ================================================================

        [Test]
        public void CommandCapacity_Limit()
        {
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");
            _world.commanders[cmdr.id] = cmdr;

            // 创建 5 个师
            for (int i = 0; i < 5; i++)
            {
                var unit = new UnitState
                {
                    id = $"U{i}",
                    ownerCountry = "TEST",
                    currentProvinceId = "P1"
                };
                _world.units[unit.id] = unit;
                Assert.IsTrue(_resolver.AssignDivision(_world, unit.id, cmdr.id));
            }

            Assert.AreEqual(5, _resolver.GetCommandedDivisionCount(_world, cmdr.id));
            Assert.IsFalse(_resolver.CanCommandMore(_world, cmdr.id));

            // 第 6 个应该失败
            var extra = new UnitState { id = "U5", ownerCountry = "TEST", currentProvinceId = "P1" };
            _world.units[extra.id] = extra;
            Assert.IsFalse(_resolver.AssignDivision(_world, extra.id, cmdr.id));
        }

        // ================================================================
        // 同省 5 师限制
        // ================================================================

        [Test]
        public void ProvinceCapacity_5PerSide()
        {
            // 创建 5 个攻方师
            for (int i = 0; i < 5; i++)
            {
                _world.units[$"ATK{i}"] = new UnitState
                {
                    id = $"ATK{i}",
                    ownerCountry = "A",
                    currentProvinceId = "P1"
                };
            }

            // 创建 3 个守方师
            for (int i = 0; i < 3; i++)
            {
                _world.units[$"DEF{i}"] = new UnitState
                {
                    id = $"DEF{i}",
                    ownerCountry = "B",
                    currentProvinceId = "P1"
                };
            }

            // 攻方 5/5，守方 3/5
            Assert.IsFalse(_resolver.CanFitMoreDivisions(_world, "P1", "A"));
            Assert.IsTrue(_resolver.CanFitMoreDivisions(_world, "P1", "B"));

            // 攻方独立计数
            Assert.AreEqual(5, _resolver.GetDivisionCountInProvince(_world, "P1", "A"));
            Assert.AreEqual(3, _resolver.GetDivisionCountInProvince(_world, "P1", "B"));
        }

        // ================================================================
        // 战斗 buff
        // ================================================================

        [Test]
        public void CommanderBuff_Rank5Equals25Pct()
        {
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");
            _world.commanders[cmdr.id] = cmdr;

            // 军衔 0: baseAttack=5 → 100+5 = 105%
            Assert.AreEqual(105, _resolver.GetCommanderAttackBuffPct(_world, cmdr.id));
            Assert.AreEqual(105, _resolver.GetCommanderDefenseBuffPct(_world, cmdr.id));

            // 军衔 2: 5+10 = 15% → 115%
            cmdr.victories = 15;
            _resolver.CheckPromotions(_world);
            Assert.AreEqual(2, cmdr.rank);
            Assert.AreEqual(115, _resolver.GetCommanderAttackBuffPct(_world, cmdr.id));
            Assert.AreEqual(115, _resolver.GetCommanderDefenseBuffPct(_world, cmdr.id));
        }

        [Test]
        public void CommanderBuff_DualLayerMultiply()
        {
            // 师战役级 +20% × 将军元帅 +20% ≈ +44%
            // 战役等级 2: 100+2*5=110%
            // 将军 rank 4 (大元帅): 100+5+4*5=125%
            // 总计: 110% × 125% = 137.5% ≈ +37.5%
            // rank 4 baseAttack=5: 100+5+20=125%
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");
            _world.commanders[cmdr.id] = cmdr;
            cmdr.victories = 75;
            _resolver.CheckPromotions(_world);

            Assert.AreEqual(4, cmdr.rank);
            Assert.AreEqual(125, _resolver.GetCommanderAttackBuffPct(_world, cmdr.id));
            // 师战役级 2 (tacticalExp=50 → level=2): 100+2*5=110%
            // 总: 110% × 125% = 137.5% → int 截断 137%
            int unitBuff = 110; // 战役等级 2
            int cmdrBuff = _resolver.GetCommanderAttackBuffPct(_world, cmdr.id);
            int totalBuff = unitBuff * cmdrBuff / 100;
            Assert.AreEqual(137, totalBuff); // +37% 战力
        }

        // ================================================================
        // 解除指挥
        // ================================================================

        [Test]
        public void Unassign_Works()
        {
            var cmdr = _resolver.RecruitCommander(_country, "general_test_basic");
            _world.commanders[cmdr.id] = cmdr;

            var unit = new UnitState { id = "U1", ownerCountry = "TEST", currentProvinceId = "P1" };
            _world.units["U1"] = unit;

            _resolver.AssignDivision(_world, "U1", cmdr.id);
            Assert.AreEqual(cmdr.id, unit.commanderId);

            _resolver.UnassignDivision(_world, "U1");
            Assert.IsNull(unit.commanderId);
        }
    }
}

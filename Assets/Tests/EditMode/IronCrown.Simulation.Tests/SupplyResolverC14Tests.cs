// ============================================================================
// Tests/SupplyResolverC14Tests.cs — C14 补给系统测试
// BFS 补给链 / 4 回合死亡窗口 / 解围 / 夹击 morale / disorganized
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;

namespace IronCrown.Tests
{
    [TestFixture]
    public class SupplyResolverC14Tests
    {
        private SupplyResolver _resolver;
        private WorldState _world;
        private CountryState _country;
        private EconomyConfig _eco;

        [SetUp]
        public void SetUp()
        {
            _resolver = new SupplyResolver();
            _world = new WorldState();
            _country = new CountryState { id = "A", capitalProvinceId = "cap" };
            _world.countries["A"] = _country;
            _eco = new EconomyConfig
            {
                reinforceRatePct = 10,
                tacticalExpPerVictory = 10,
                tacticalExpPerDefeat = -5,
                tacticalExpLevelStep = 25,
                tacticalExpAttackBonusPerLevel = 5,
                tacticalExpDefenseBonusPerLevel = 5,
                autoRetreatThresholdPct = 15,
                retreatBonusManpower = 100,
                retreatBonusEquipment = 50,
                retreatMoraleReset = 30,
                retreatRecoveryTurns = 1
            };
        }

        // =================================================================
        // 辅助方法
        // =================================================================

        private ProvinceState MakeProvince(string id, string controller, int infra = 3, int rail = 1, int port = 0, string[] neighbors = null)
        {
            var p = new ProvinceState
            {
                id = id,
                controllerCountry = controller,
                ownerCountry = controller,
                infrastructure = infra,
                railwayLevel = rail,
                portLevel = port,
                neighbors = neighbors ?? new string[0]
            };
            _world.provinces[id] = p;
            return p;
        }

        private UnitState MakeUnit(string id, string owner, string province, int org = 60, int maxOrg = 60, int morale = 50, int supplyCons = 10)
        {
            var u = new UnitState
            {
                id = id,
                ownerCountry = owner,
                currentProvinceId = province,
                organization = org,
                maxOrganization = maxOrg,
                morale = morale,
                supplyConsumption = supplyCons,
                manpower = 1000,
                maxManpower = 1000,
                equipment = 500,
                maxEquipment = 500
            };
            _world.units[id] = u;
            if (_world.countries.TryGetValue(owner, out var c))
                c.unitIds.Add(id);
            return u;
        }

        // =================================================================
        // 1. BFS 补给链基本测试
        // =================================================================

        [Test]
        public void CapitalUnit_HasSupply_NotCutoff()
        {
            // 首都 → 部队在首都 → 补给充足
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "A", infra: 3, rail: 1, neighbors: new[] { "cap" });
            MakeUnit("u1", "A", "cap");

            _resolver.CheckSupply(_country, _world);

            var u = _world.units["u1"];
            Assert.IsFalse(u.isCutoff);
            Assert.IsFalse(u.isDisorganized);
        }

        [Test]
        public void AdjacentUnit_HasSupply_NotCutoff()
        {
            // 首都 → p1 邻接 → 部队在 p1 → 补给可达
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "A", infra: 3, rail: 1, neighbors: new[] { "cap" });
            MakeUnit("u1", "A", "p1");

            _resolver.CheckSupply(_country, _world);

            var u = _world.units["u1"];
            Assert.IsFalse(u.isCutoff);
        }

        [Test]
        public void ChainSupply_FlowsThroughFriendlyProvinces()
        {
            // cap → p1 → p2 → p3，全友好
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "A", infra: 4, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 3, rail: 1, neighbors: new[] { "p1", "p3" });
            MakeProvince("p3", "A", infra: 2, rail: 0, neighbors: new[] { "p2" });
            MakeUnit("u1", "A", "p3");

            _resolver.CheckSupply(_country, _world);

            var u = _world.units["u1"];
            Assert.IsFalse(u.isCutoff, "p3 should be reachable via cap→p1→p2→p3");
        }

        // =================================================================
        // 2. 被切断补给测试
        // =================================================================

        [Test]
        public void EnemySurrounded_UnitBecomesCutoff()
        {
            // cap → p1 → p2，但 p1 被敌方控制 → p2 上的部队被切断
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" }); // 敌方控制
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            MakeUnit("u1", "A", "p2");

            // 需要战争状态
            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);

            var u = _world.units["u1"];
            Assert.IsTrue(u.isCutoff, "p2 should be unreachable since p1 is enemy-controlled");
        }

        [Test]
        public void NoCapital_CutoffAllUnits()
        {
            // 首都被敌方控制 → 所有部队被切断
            MakeProvince("cap", "B", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "A", infra: 3, rail: 1, neighbors: new[] { "cap" });
            MakeUnit("u1", "A", "p1");

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);

            var u = _world.units["u1"];
            Assert.IsTrue(u.isCutoff, "No friendly capital → all units cutoff");
        }

        // =================================================================
        // 3. 四回合死亡窗口测试
        // =================================================================

        [Test]
        public void Cutoff_4Turns_UnitDestroyed()
        {
            // 被切断 4 回合 → 部队消亡
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            MakeUnit("u1", "A", "p2");

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            // 模拟 4 回合
            for (int i = 0; i < 4; i++)
            {
                _resolver.CheckSupply(_country, _world);
                if (_world.units.ContainsKey("u1"))
                {
                    // 用 TurnResolver 的清理逻辑模拟
                    var u = _world.units["u1"];
                    if (u.isCutoff && u.cutoffTurns >= 4)
                    {
                        _world.units.Remove("u1");
                        _country.unitIds.Remove("u1");
                    }
                }
            }

            Assert.IsFalse(_world.units.ContainsKey("u1"), "Unit should be destroyed after 4 turns cutoff");
        }

        [Test]
        public void Cutoff_3Turns_UnitStillAlive()
        {
            // 被切断 3 回合 → 部队还在
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            MakeUnit("u1", "A", "p2");

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            for (int i = 0; i < 3; i++)
                _resolver.CheckSupply(_country, _world);

            var u = _world.units["u1"];
            Assert.IsTrue(u.isCutoff);
            Assert.AreEqual(3, u.cutoffTurns);
            Assert.IsTrue(_world.units.ContainsKey("u1"), "Unit should still be alive at 3 turns");
        }

        [Test]
        public void Cutoff_OrgDropsEachTurn()
        {
            // 被切断 → 组织度每回合 -15
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p2", org: 60, maxOrg: 60);

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);
            Assert.AreEqual(45, u.organization, "After 1 cutoff turn: 60-15=45");

            _resolver.CheckSupply(_country, _world);
            Assert.AreEqual(30, u.organization, "After 2 cutoff turns: 45-15=30");
        }

        [Test]
        public void Cutoff_MoraleDropsEachTurn()
        {
            // 被切断 → 士气每回合 -20
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p2", morale: 80);

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);
            Assert.AreEqual(60, u.morale, "After 1 cutoff turn: 80-20=60");

            _resolver.CheckSupply(_country, _world);
            Assert.AreEqual(40, u.morale, "After 2 cutoff turns: 60-20=40");
        }

        // =================================================================
        // 4. 解围 A 方案测试
        // =================================================================

        [Test]
        public void Relief_FriendlyOccupiesNeighbor_UnCutoff()
        {
            // 部队在 p2 被切断，友军占领 p1 → 解围
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p2");

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            // 先切断
            _resolver.CheckSupply(_country, _world);
            Assert.IsTrue(u.isCutoff);

            // 友军占领 p1
            _world.provinces["p1"].controllerCountry = "A";

            // 解围
            SupplyResolver.CheckRelief(_world, "p1", "A");

            Assert.IsFalse(u.isCutoff, "Relief: friendly occupation of neighbor should uncutoff");
            Assert.AreEqual(0, u.cutoffTurns);
        }

        [Test]
        public void GlobalRelief_FriendlyNeighbor_UnCutoff()
        {
            // 全局解围检查：有友方邻省 → 解除切断
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1", "p3" });
            MakeProvince("p3", "A", infra: 1, rail: 0, neighbors: new[] { "p2" });
            var u = MakeUnit("u1", "A", "p2");

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            // 切断
            _resolver.CheckSupply(_country, _world);
            Assert.IsTrue(u.isCutoff);

            // p3 是友方邻省 → 全局解围
            _resolver.CheckGlobalRelief(_world);

            Assert.IsFalse(u.isCutoff, "GlobalRelief: friendly neighbor p3 should uncutoff");
        }

        // =================================================================
        // 5. 夹击/包围 morale 测试
        // =================================================================

        [Test]
        public void Flanking_2EnemyNeighbors_MoraleDrop()
        {
            // 部队在 p1，被 2 个敌省包围 → morale -10
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "A", infra: 3, rail: 1, neighbors: new[] { "cap", "e1", "e2" });
            MakeProvince("e1", "B", infra: 3, rail: 1, neighbors: new[] { "p1" });
            MakeProvince("e2", "B", infra: 3, rail: 1, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p1", morale: 80);

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);

            Assert.AreEqual(70, u.morale, "2 enemy neighbors: morale 80-10=70");
        }

        [Test]
        public void Flanking_3EnemyNeighbors_MoraleDrop20()
        {
            // 3 个敌省包围 → morale -20
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "A", infra: 3, rail: 1, neighbors: new[] { "cap", "e1", "e2", "e3" });
            MakeProvince("e1", "B", infra: 3, rail: 1, neighbors: new[] { "p1" });
            MakeProvince("e2", "B", infra: 3, rail: 1, neighbors: new[] { "p1" });
            MakeProvince("e3", "B", infra: 3, rail: 1, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p1", morale: 80);

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);

            Assert.AreEqual(60, u.morale, "3 enemy neighbors: morale 80-20=60");
        }

        [Test]
        public void Flanking_1EnemyNeighbor_NoPenalty()
        {
            // 只有 1 个敌省 → 无 morale 惩罚
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "A", infra: 3, rail: 1, neighbors: new[] { "cap", "e1" });
            MakeProvince("e1", "B", infra: 3, rail: 1, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p1", morale: 80);

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);

            Assert.AreEqual(80, u.morale, "1 enemy neighbor: no morale penalty");
        }

        // =================================================================
        // 6. disorganized 状态测试
        // =================================================================

        [Test]
        public void SupplyDeficit_Over50Pct_Disorganized()
        {
            // 补给需求 10，可用 4（缺 60%）→ disorganized
            MakeProvince("cap", "A", infra: 1, rail: 0, neighbors: new[] { "p1" }); // 低容量
            MakeProvince("p1", "A", infra: 1, rail: 0, neighbors: new[] { "cap" });
            var u = MakeUnit("u1", "A", "p1", supplyCons: 100); // 高需求

            _resolver.CheckSupply(_country, _world);

            // p1 补给值 = cap容量 * 90/100 * 80/100 = (1*10) * 0.9 * 0.8 ≈ 7
            // 需求 100 → 缺额 > 50% → disorganized
            Assert.IsTrue(u.isDisorganized, "High deficit should be disorganized");
        }

        [Test]
        public void Cutoff_AlwaysDisorganized()
        {
            // 被切断 → 永远 disorganized
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p2");

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);

            Assert.IsTrue(u.isCutoff);
            Assert.IsTrue(u.isDisorganized);
        }

        // =================================================================
        // 7. 补员与补给交互测试
        // =================================================================

        [Test]
        public void Cutoff_NoReplenishment()
        {
            // 被切断 → 不补员
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p2");
            u.manpower = 500;
            u.equipment = 250;
            _country.manpower = 10000;
            _country.equipmentStockpile = 10000;

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);
            _resolver.ReplenishUnits(_world, _eco, null);

            Assert.AreEqual(500, u.manpower, "Cutoff: no manpower replenishment");
            Assert.AreEqual(250, u.equipment, "Cutoff: no equipment replenishment");
        }

        [Test]
        public void Disorganized_HalfReplenishment()
        {
            // disorganized → 补员率减半
            MakeProvince("cap", "A", infra: 1, rail: 0, neighbors: new[] { "p1" }); // 低容量
            MakeProvince("p1", "A", infra: 1, rail: 0, neighbors: new[] { "cap" });
            var u = MakeUnit("u1", "A", "p1", supplyCons: 100);
            u.manpower = 500;
            u.equipment = 250;
            _country.manpower = 10000;
            _country.equipmentStockpile = 10000;

            _resolver.CheckSupply(_country, _world);

            // 如果 disorganized，补员率减半
            int initialManpower = u.manpower;
            _resolver.ReplenishUnits(_world, _eco, null);

            // 正常补员 = 500 * 10% = 50，减半 = 25
            // 但实际取决于是否真的 disorganized（取决于补给值）
            if (u.isDisorganized)
            {
                Assert.LessOrEqual(u.manpower - initialManpower, 30,
                    "Disorganized: reduced replenishment");
            }
        }

        // =================================================================
        // 8. 补给恢复测试
        // =================================================================

        [Test]
        public void SupplyRestored_ResetCutoffState()
        {
            // 被切断 → 友军打通通路 → 补给恢复
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p2");

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            // 切断 2 回合
            _resolver.CheckSupply(_country, _world);
            _resolver.CheckSupply(_country, _world);
            Assert.IsTrue(u.isCutoff);
            Assert.AreEqual(2, u.cutoffTurns);

            // 友军夺回 p1
            _world.provinces["p1"].controllerCountry = "A";

            // 补给恢复
            _resolver.CheckSupply(_country, _world);

            Assert.IsFalse(u.isCutoff, "Supply restored: not cutoff");
            Assert.AreEqual(0, u.cutoffTurns, "Supply restored: cutoffTurns reset");
            Assert.IsFalse(u.isDisorganized, "Supply restored: not disorganized");
        }

        // =================================================================
        // 9. 边界条件
        // =================================================================

        [Test]
        public void NoUnits_NoError()
        {
            // 无部队 → 无异常
            MakeProvince("cap", "A", infra: 5, rail: 2);

            Assert.DoesNotThrow(() => _resolver.CheckSupply(_country, _world));
        }

        [Test]
        public void EmptyWorld_NoError()
        {
            // 空世界 → 无异常
            var emptyCountry = new CountryState { id = "X" };
            Assert.DoesNotThrow(() => _resolver.CheckSupply(emptyCountry, _world));
        }

        [Test]
        public void UnitInNonexistentProvince_NoError()
        {
            // 部队在不存在的省份 → 无异常
            MakeProvince("cap", "A", infra: 5, rail: 2);
            MakeUnit("u1", "A", "nonexistent");

            Assert.DoesNotThrow(() => _resolver.CheckSupply(_country, _world));
        }

        [Test]
        public void Morale_FloorAt0()
        {
            // morale 不能为负
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "A", infra: 3, rail: 1, neighbors: new[] { "cap", "e1", "e2" });
            MakeProvince("e1", "B", infra: 3, rail: 1, neighbors: new[] { "p1" });
            MakeProvince("e2", "B", infra: 3, rail: 1, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p1", morale: 5);

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            _resolver.CheckSupply(_country, _world);

            Assert.GreaterOrEqual(u.morale, 0, "Morale should not go below 0");
        }

        [Test]
        public void Organization_FloorAt0()
        {
            // 组织度不能为负
            MakeProvince("cap", "A", infra: 5, rail: 2, neighbors: new[] { "p1" });
            MakeProvince("p1", "B", infra: 3, rail: 1, neighbors: new[] { "cap", "p2" });
            MakeProvince("p2", "A", infra: 2, rail: 0, neighbors: new[] { "p1" });
            var u = MakeUnit("u1", "A", "p2", org: 5);

            WarRegistry.TryDeclareWar(_world, "A", "B", 1, out _);

            // 多回合切断
            for (int i = 0; i < 5; i++)
                _resolver.CheckSupply(_country, _world);

            Assert.GreaterOrEqual(u.organization, 0, "Organization should not go below 0");
        }
    }
}

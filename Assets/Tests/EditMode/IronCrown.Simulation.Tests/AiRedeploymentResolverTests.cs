// ============================================================================
// Tests/AiRedeploymentResolverTests.cs — C8 AI 调防测试
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;
using IronCrown.Contracts;

namespace IronCrown.Tests
{
    [TestFixture]
    public class AiRedeploymentResolverTests
    {
        private EconomyConfig _eco;

        [SetUp]
        public void SetUp()
        {
            _eco = new EconomyConfig
            {
                aiRedeployVulnerableRatioPct = 80,
                aiMaxRedeploysPerTurn = 1
            };
        }

        // ── 核心调防 ──

        [Test]
        public void Redeploy_WeakFrontier_MovesUnitFromInland()
        {
            // 前线弱守，内陆有 ≥2 驻军 → 应调一支到前线
            var world = BuildWorld(out var ai, out _);
            // P1(前线,1驻军) -- P2(内陆,2驻军)
            // P1 邻接 enemy 省 E1，E1 有敌方部队
            var frontier = world.provinces["P1"];
            var inland = world.provinces["P2"];

            // 前线 1 支弱守军
            AddUnit(world, "garrison_1", "ai", "P1", org: 30, morale: 20);
            // 内陆 2 支驻军
            AddUnit(world, "aaa_redeploy", "ai", "P2", org: 80, morale: 50); // sorts first
            AddUnit(world, "garrison_2", "ai", "P2", org: 80, morale: 50);
            // 敌方 1 支强军在 E1
            AddUnit(world, "enemy_1", "enemy", "E1", org: 100, morale: 80);

            var movement = new MovementResolver();
            var resolver = new AiRedeploymentResolver(movement);
            resolver.TryRedeploy(ai, world, _eco);

            // redeploy_1 应已从 P2 移到 P1
            Assert.AreEqual("P1", world.units["aaa_redeploy"].currentProvinceId);
        }

        [Test]
        public void NoRedeploy_FrontierStrongEnough()
        {
            // 前线守军够强 → 不调防
            var world = BuildWorld(out var ai, out _);
            AddUnit(world, "g1", "ai", "P1", org: 100, morale: 80); // 强守军
            AddUnit(world, "r1", "ai", "P2", org: 80, morale: 50);
            AddUnit(world, "g2", "ai", "P2", org: 80, morale: 50);
            AddUnit(world, "e1", "enemy", "E1", org: 50, morale: 30); // 弱敌

            var movement = new MovementResolver();
            var resolver = new AiRedeploymentResolver(movement);
            resolver.TryRedeploy(ai, world, _eco);

            // 不应移动
            Assert.AreEqual("P2", world.units["r1"].currentProvinceId);
        }

        [Test]
        public void NoRedeploy_InlandOnlyOneGarrison()
        {
            // 内陆只有 1 支驻军 → 不调防（保留自卫）
            var world = BuildWorld(out var ai, out _);
            AddUnit(world, "g1", "ai", "P1", org: 30, morale: 20);
            AddUnit(world, "r1", "ai", "P2", org: 80, morale: 50); // 只有 1 支
            AddUnit(world, "e1", "enemy", "E1", org: 100, morale: 80);

            var movement = new MovementResolver();
            var resolver = new AiRedeploymentResolver(movement);
            resolver.TryRedeploy(ai, world, _eco);

            Assert.AreEqual("P2", world.units["r1"].currentProvinceId);
        }

        [Test]
        public void NoRedeploy_InlandNotFullyOwned()
        {
            // 内陆邻省有非己方控制 → 不算内陆源省
            var world = BuildWorld(out var ai, out _);
            // P2 邻接 enemy 省 E2（不是全部己方控制）
            world.provinces["P2"].neighbors = new[] { "P1", "E2" };
            world.provinces["E2"] = new ProvinceState
            {
                id = "E2", controllerCountry = "enemy", ownerCountry = "enemy",
                neighbors = new[] { "P2" }
            };

            AddUnit(world, "g1", "ai", "P1", org: 30, morale: 20);
            AddUnit(world, "r1", "ai", "P2", org: 80, morale: 50);
            AddUnit(world, "g2", "ai", "P2", org: 80, morale: 50);
            AddUnit(world, "e1", "enemy", "E1", org: 100, morale: 80);

            var movement = new MovementResolver();
            var resolver = new AiRedeploymentResolver(movement);
            resolver.TryRedeploy(ai, world, _eco);

            // P2 不算内陆 → 不调防
            Assert.AreEqual("P2", world.units["r1"].currentProvinceId);
        }

        // ── 每国上限 ──

        [Test]
        public void Redeploy_RespectsMaxPerTurn()
        {
            // maxRedeploys=1，两个前线弱守 → 只调 1 支
            _eco.aiMaxRedeploysPerTurn = 1;

            var world = BuildWorld(out var ai, out _);
            // P1 弱守 + P3 弱守
            world.provinces["P3"] = new ProvinceState
            {
                id = "P3", controllerCountry = "ai", ownerCountry = "ai",
                neighbors = new[] { "P2", "E2" }
            };
            world.provinces["E2"] = new ProvinceState
            {
                id = "E2", controllerCountry = "enemy", ownerCountry = "enemy",
                neighbors = new[] { "P3" }
            };

            AddUnit(world, "g1", "ai", "P1", org: 30, morale: 20);
            AddUnit(world, "g3", "ai", "P3", org: 30, morale: 20);
            AddUnit(world, "r1", "ai", "P2", org: 80, morale: 50);
            AddUnit(world, "r2", "ai", "P2", org: 80, morale: 50);
            AddUnit(world, "e1", "enemy", "E1", org: 100, morale: 80);
            AddUnit(world, "e2", "enemy", "E2", org: 100, morale: 80);

            var movement = new MovementResolver();
            var resolver = new AiRedeploymentResolver(movement);
            resolver.TryRedeploy(ai, world, _eco);

            // 只有 1 支被调走
            int moved = 0;
            if (world.units.ContainsKey("r1") && world.units["r1"].currentProvinceId != "P2") moved++;
            if (world.units.ContainsKey("r2") && world.units["r2"].currentProvinceId != "P2") moved++;
            Assert.AreEqual(1, moved);
        }

        // ── 跳过玩家 ──

        [Test]
        public void NoRedeploy_PlayerCountry_Skipped()
        {
            var world = BuildWorld(out _, out var player);
            world.playerCountryId = "player";
            AddUnit(world, "g1", "player", "P1", org: 30, morale: 20);
            AddUnit(world, "r1", "player", "P2", org: 80, morale: 50);
            AddUnit(world, "g2", "player", "P2", org: 80, morale: 50);
            AddUnit(world, "e1", "enemy", "E1", org: 100, morale: 80);

            // 将 P1/P2 设为 player 控制
            world.provinces["P1"].controllerCountry = "player";
            world.provinces["P1"].ownerCountry = "player";
            world.provinces["P2"].controllerCountry = "player";
            world.provinces["P2"].ownerCountry = "player";

            var movement = new MovementResolver();
            var resolver = new AiRedeploymentResolver(movement);
            resolver.TryRedeploy(player, world, _eco);

            // 玩家不触发 AI 调防
            Assert.AreEqual("P2", world.units["r1"].currentProvinceId);
        }

        // ── 战斗中部队不调防 ──

        [Test]
        public void NoRedeploy_UnitInBattle_Skipped()
        {
            var world = BuildWorld(out var ai, out _);
            AddUnit(world, "g1", "ai", "P1", org: 30, morale: 20);
            AddUnit(world, "r1", "ai", "P2", org: 80, morale: 50);
            AddUnit(world, "g2", "ai", "P2", org: 80, morale: 50);
            AddUnit(world, "e1", "enemy", "E1", org: 100, morale: 80);

            // r1 正在战斗中
            world.activeBattles.Add(new ActiveBattle
            {
                id = "battle_1",
                attackerUnitId = "r1",
                defenderUnitId = "e1",
                provinceId = "P1",
                turnsElapsed = 0
            });

            var movement = new MovementResolver();
            var resolver = new AiRedeploymentResolver(movement);
            resolver.TryRedeploy(ai, world, _eco);

            // 战斗中不调防
            Assert.AreEqual("P2", world.units["r1"].currentProvinceId);
        }

        // ── 辅助 ──

        private WorldState BuildWorld(out CountryState ai, out CountryState player)
        {
            ai = new CountryState
            {
                id = "ai", name = "AI",
                stability = 60, warSupport = 50,
                civilianFactories = 5, militaryFactories = 3
            };
            player = new CountryState
            {
                id = "player", name = "Player",
                stability = 70, warSupport = 60,
                civilianFactories = 10, militaryFactories = 5
            };
            var enemy = new CountryState
            {
                id = "enemy", name = "Enemy",
                stability = 50, warSupport = 60,
                civilianFactories = 3, militaryFactories = 2
            };

            // P1(前线,ai) -- P2(内陆,ai) -- E1(敌方)
            var p1 = new ProvinceState
            {
                id = "P1", controllerCountry = "ai", ownerCountry = "ai",
                neighbors = new[] { "P2", "E1" }
            };
            var p2 = new ProvinceState
            {
                id = "P2", controllerCountry = "ai", ownerCountry = "ai",
                neighbors = new[] { "P1" }
            };
            var e1 = new ProvinceState
            {
                id = "E1", controllerCountry = "enemy", ownerCountry = "enemy",
                neighbors = new[] { "P1" }
            };

            return new WorldState
            {
                countries = new Dictionary<string, CountryState>
                {
                    { "ai", ai }, { "player", player }, { "enemy", enemy }
                },
                provinces = new Dictionary<string, ProvinceState>
                {
                    { "P1", p1 }, { "P2", p2 }, { "E1", e1 }
                },
                units = new Dictionary<string, UnitState>(),
                activeBattles = new List<ActiveBattle>(),
                warRelations = new List<WarRelation>()
            };
        }

        private void AddUnit(WorldState world, string id, string owner, string provinceId,
            int org = 80, int morale = 50)
        {
            world.units[id] = new UnitState
            {
                id = id,
                ownerCountry = owner,
                currentProvinceId = provinceId,
                organization = org,
                maxOrganization = 100,
                morale = morale,
                experience = 1,
                movesLeft = 2,
                baseAttack = 10,
                baseDefense = 5,
                manpower = 100,
                equipment = 100
            };
        }
    }
}

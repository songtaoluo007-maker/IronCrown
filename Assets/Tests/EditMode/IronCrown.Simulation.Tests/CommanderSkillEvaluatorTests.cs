// ============================================================================
// CommanderSkillEvaluatorTests.cs — C15b 将军卡技能评估器测试
// 12 个核心技能各 1 测试
// ============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Simulation;

namespace IronCrown.Simulation.Tests
{
    public class CommanderSkillEvaluatorTests
    {
        private IConfigRegistry _config;

        [SetUp]
        public void SetUp()
        {
            var reg = new TestConfigRegistry();
            // 注册所有 12 张卡 + 测试卡
            reg.Register("general_test_basic", new CommanderConfig
            {
                id = "general_test_basic", name = "测试将军", title = "铁壁将军",
                baseAttack = 5, baseDefense = 5, recruitCapitalCost = 100,
                recruitManpowerCost = 500, baseMaxDivisions = 1, skills = new GeneralSkillEntry[0]
            });
            reg.Register("general_ironwall", new CommanderConfig
            {
                id = "general_ironwall", name = "铁壁元帅", title = "铁壁",
                baseAttack = 5, baseDefense = 15, recruitCapitalCost = 150,
                recruitManpowerCost = 600, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "defenseBonus", value = 20 },
                    new GeneralSkillEntry { type = "moraleBonus", value = 15 }
                }
            });
            reg.Register("general_blitz", new CommanderConfig
            {
                id = "general_blitz", name = "突击先锋", title = "闪电",
                baseAttack = 20, baseDefense = -5, recruitCapitalCost = 150,
                recruitManpowerCost = 600, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "attackBonus", value = 20 },
                    new GeneralSkillEntry { type = "breakthroughBonus", value = 15 },
                    new GeneralSkillEntry { type = "defenseBonus", value = -5 }
                }
            });
            reg.Register("general_armor_pioneer", new CommanderConfig
            {
                id = "general_armor_pioneer", name = "装甲先驱", title = "铁甲",
                baseAttack = 10, baseDefense = 5, recruitCapitalCost = 150,
                recruitManpowerCost = 600, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "brigadeBonus", brigadeType = "light_tank", stat = "attack", value = 25 }
                }
            });
            reg.Register("general_lightning", new CommanderConfig
            {
                id = "general_lightning", name = "闪电将军", title = "闪电",
                baseAttack = 15, baseDefense = 5, recruitCapitalCost = 150,
                recruitManpowerCost = 600, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "speedBonus", value = 1 },
                    new GeneralSkillEntry { type = "breakthroughBonus", value = 20 }
                }
            });
            reg.Register("general_mountain_hunter", new CommanderConfig
            {
                id = "general_mountain_hunter", name = "山地猎手", title = "山地",
                baseAttack = 8, baseDefense = 10, recruitCapitalCost = 120,
                recruitManpowerCost = 500, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "terrainBonus", terrain = "Mountain", value = 30 },
                    new GeneralSkillEntry { type = "terrainBonus", terrain = "Hills", value = 30 }
                }
            });
            reg.Register("general_fireman", new CommanderConfig
            {
                id = "general_fireman", name = "救火队员", title = "救火",
                baseAttack = 8, baseDefense = 8, recruitCapitalCost = 120,
                recruitManpowerCost = 500, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "cutoffDecayMultiplier", value = 50 }
                }
            });
            reg.Register("general_logistics", new CommanderConfig
            {
                id = "general_logistics", name = "后勤大师", title = "后勤",
                baseAttack = 3, baseDefense = 8, recruitCapitalCost = 120,
                recruitManpowerCost = 500, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "supplyConsumptionReduction", value = 25 },
                    new GeneralSkillEntry { type = "reinforceRateBonus", value = 20 }
                }
            });
            reg.Register("general_plains_hound", new CommanderConfig
            {
                id = "general_plains_hound", name = "平原猎犬", title = "平原",
                baseAttack = 10, baseDefense = 8, recruitCapitalCost = 120,
                recruitManpowerCost = 500, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "terrainBonus", terrain = "Plain", value = 15 },
                    new GeneralSkillEntry { type = "terrainBonus", terrain = "Coastline", value = 15 }
                }
            });
            reg.Register("general_veteran", new CommanderConfig
            {
                id = "general_veteran", name = "老将", title = "老兵",
                baseAttack = 5, baseDefense = 5, recruitCapitalCost = 80,
                recruitManpowerCost = 400, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "tacticalExpRateBonus", value = 50 }
                }
            });
            reg.Register("general_engineer", new CommanderConfig
            {
                id = "general_engineer", name = "防御工兵", title = "工兵",
                baseAttack = 3, baseDefense = 10, recruitCapitalCost = 80,
                recruitManpowerCost = 400, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "defenseBonus", value = 10 }
                }
            });
            reg.Register("general_infantry_drill", new CommanderConfig
            {
                id = "general_infantry_drill", name = "步兵教官", title = "教官",
                baseAttack = 5, baseDefense = 5, recruitCapitalCost = 80,
                recruitManpowerCost = 400, baseMaxDivisions = 1,
                skills = new[]
                {
                    new GeneralSkillEntry { type = "brigadeBonus", brigadeType = "infantry", stat = "attackDefense", value = 10 }
                }
            });
            reg.Register("general_basic_officer", new CommanderConfig
            {
                id = "general_basic_officer", name = "普通军官", title = "军官",
                baseAttack = 5, baseDefense = 5, recruitCapitalCost = 60,
                recruitManpowerCost = 300, baseMaxDivisions = 1,
                skills = new GeneralSkillEntry[0]
            });
            _config = reg;
        }

        private CommanderState MakeCommander(string cardId)
        {
            return new CommanderState
            {
                id = "cmdr_test_1",
                name = "测试",
                ownerCountry = "empire",
                generalCardId = cardId,
                rank = 0,
                isActive = true
            };
        }

        // =====================================================================
        // 攻击技能测试
        // =====================================================================

        [Test]
        public void Blitz_AttackPlus20()
        {
            var cmdr = MakeCommander("general_blitz");
            int atk = CommanderSkillEvaluator.EvalAttack(_config, cmdr, null, null, null);
            Assert.AreEqual(120, atk); // 100 + 20
        }

        // =====================================================================
        // 防御技能测试
        // =====================================================================

        [Test]
        public void Ironwall_DefensePlus20()
        {
            var cmdr = MakeCommander("general_ironwall");
            int def = CommanderSkillEvaluator.EvalDefense(_config, cmdr, null, null, null);
            Assert.AreEqual(120, def); // 100 + 20
        }

        [Test]
        public void Blitz_AttackPlus20DefenseMinus5()
        {
            var cmdr = MakeCommander("general_blitz");
            int def = CommanderSkillEvaluator.EvalDefense(_config, cmdr, null, null, null);
            Assert.AreEqual(95, def); // 100 + (-5)
        }

        // =====================================================================
        // 兵种加成测试
        // =====================================================================

        [Test]
        public void ArmorPioneer_OnlyLightTankBrigade()
        {
            var cmdr = MakeCommander("general_armor_pioneer");
            // 有 light_tank 旅的师
            var unitWithTank = new UnitState
            {
                brigades = new List<BrigadeState>
                {
                    new BrigadeState { brigadeType = "light_tank", count = 3 }
                }
            };
            // 没有 light_tank 旅的师
            var unitWithoutTank = new UnitState
            {
                brigades = new List<BrigadeState>
                {
                    new BrigadeState { brigadeType = "infantry", count = 3 }
                }
            };

            int atkWith = CommanderSkillEvaluator.EvalAttack(_config, cmdr, unitWithTank, null, null);
            int atkWithout = CommanderSkillEvaluator.EvalAttack(_config, cmdr, unitWithoutTank, null, null);

            Assert.AreEqual(125, atkWith);    // 100 + 25
            Assert.AreEqual(100, atkWithout); // 无匹配 = 0
        }

        // =====================================================================
        // 地形加成测试
        // =====================================================================

        [Test]
        public void MountainHunter_OnlyMountainTerrain()
        {
            var cmdr = MakeCommander("general_mountain_hunter");
            var mountain = new ProvinceState { terrain = TerrainType.Mountain };
            var plain = new ProvinceState { terrain = TerrainType.Plain };

            int defMountain = CommanderSkillEvaluator.EvalDefense(_config, cmdr, null, mountain, null);
            int defPlain = CommanderSkillEvaluator.EvalDefense(_config, cmdr, null, plain, null);

            Assert.AreEqual(130, defMountain); // 100 + 30
            Assert.AreEqual(100, defPlain);    // 无匹配 = 0
        }

        // =====================================================================
        // 切断衰减测试
        // =====================================================================

        [Test]
        public void Fireman_CutoffDecayHalved()
        {
            var cmdr = MakeCommander("general_fireman");
            int multiplier = CommanderSkillEvaluator.EvalCutoffDecayMultiplier(_config, cmdr);
            Assert.AreEqual(50, multiplier); // 衰减减半
        }

        [Test]
        public void NoCommander_CutoffDecayNormal()
        {
            int multiplier = CommanderSkillEvaluator.EvalCutoffDecayMultiplier(_config, null);
            Assert.AreEqual(100, multiplier); // 无将领 = 正常衰减
        }

        // =====================================================================
        // 补给消耗减少测试
        // =====================================================================

        [Test]
        public void Logistics_SupplyConsumptionReduction25()
        {
            var cmdr = MakeCommander("general_logistics");
            int reduction = CommanderSkillEvaluator.EvalSupplyConsumptionReduction(_config, cmdr);
            Assert.AreEqual(25, reduction);
        }

        // =====================================================================
        // 补员速率加成测试
        // =====================================================================

        [Test]
        public void Logistics_ReinforceRateBonus20()
        {
            var cmdr = MakeCommander("general_logistics");
            int bonus = CommanderSkillEvaluator.EvalReinforceRateBonus(_config, cmdr);
            Assert.AreEqual(20, bonus);
        }

        // =====================================================================
        // 速度加成测试
        // =====================================================================

        [Test]
        public void Lightning_SpeedBonus1()
        {
            var cmdr = MakeCommander("general_lightning");
            int speed = CommanderSkillEvaluator.EvalSpeedBonus(_config, cmdr);
            Assert.AreEqual(1, speed);
        }

        // =====================================================================
        // 突破加成测试
        // =====================================================================

        [Test]
        public void Lightning_Breakthrough20()
        {
            var cmdr = MakeCommander("general_lightning");
            int brk = CommanderSkillEvaluator.EvalBreakthrough(_config, cmdr);
            Assert.AreEqual(20, brk);
        }

        // =====================================================================
        // 经验速率测试
        // =====================================================================

        [Test]
        public void Veteran_TacticalExpRateBonus50()
        {
            var cmdr = MakeCommander("general_veteran");
            int bonus = CommanderSkillEvaluator.EvalTacticalExpRateBonus(_config, cmdr);
            Assert.AreEqual(50, bonus);
        }

        // =====================================================================
        // 士气加成测试
        // =====================================================================

        [Test]
        public void Ironwall_MoraleBonus15()
        {
            var cmdr = MakeCommander("general_ironwall");
            int morale = CommanderSkillEvaluator.EvalMorale(_config, cmdr);
            Assert.AreEqual(15, morale);
        }

        // =====================================================================
        // 无技能卡测试
        // =====================================================================

        [Test]
        public void BasicOfficer_NoSkills_ReturnsZero()
        {
            var cmdr = MakeCommander("general_basic_officer");
            Assert.AreEqual(100, CommanderSkillEvaluator.EvalAttack(_config, cmdr, null, null, null));
            Assert.AreEqual(100, CommanderSkillEvaluator.EvalDefense(_config, cmdr, null, null, null));
            Assert.AreEqual(0, CommanderSkillEvaluator.EvalMorale(_config, cmdr));
            Assert.AreEqual(0, CommanderSkillEvaluator.EvalBreakthrough(_config, cmdr));
            Assert.AreEqual(0, CommanderSkillEvaluator.EvalSpeedBonus(_config, cmdr));
        }

        // =====================================================================
        // 步兵教官兵种加成测试
        // =====================================================================

        [Test]
        public void InfantryDrill_InfantryAttackDefensePlus10()
        {
            var cmdr = MakeCommander("general_infantry_drill");
            var unitWithInf = new UnitState
            {
                brigades = new List<BrigadeState>
                {
                    new BrigadeState { brigadeType = "infantry", count = 3 }
                }
            };
            int atk = CommanderSkillEvaluator.EvalAttack(_config, cmdr, unitWithInf, null, null);
            int def = CommanderSkillEvaluator.EvalDefense(_config, cmdr, unitWithInf, null, null);
            Assert.AreEqual(110, atk); // 100 + 10
            Assert.AreEqual(110, def); // 100 + 10
        }
    }
}

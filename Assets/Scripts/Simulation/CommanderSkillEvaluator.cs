// ============================================================================
// Simulation/CommanderSkillEvaluator.cs — 将军卡技能评估器（C15b）
// 根据 generalCardId 查模板、按 skills 列表计算 buff
// ============================================================================

using System;
using System.Collections.Generic;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    /// <summary>将军卡技能静态评估工具</summary>
    public static class CommanderSkillEvaluator
    {
        // =====================================================================
        // 攻击 buff（百分比，100 = +0%）
        // =====================================================================

        /// <summary>计算将军卡攻击加成百分比（100 = ×1.0 基准）</summary>
        public static int EvalAttack(IConfigRegistry config, CommanderState commander, UnitState unit, ProvinceState province, WorldState world)
        {
            if (commander == null) return 100;
            var card = config.Get<CommanderConfig>(commander.generalCardId);
            if (card?.skills == null) return 100;

            int bonus = 0;
            foreach (var skill in card.skills)
            {
                switch (skill.type)
                {
                    case "attackBonus":
                        bonus += skill.value;
                        break;
                    case "brigadeBonus":
                        if (unit != null && skill.stat == "attack")
                            bonus += EvalBrigadeBonus(unit, skill.brigadeType, skill.value);
                        break;
                    case "terrainBonus":
                        if (province != null)
                            bonus += EvalTerrainBonus(province, skill.terrain, skill.value);
                        break;
                }
            }
            return 100 + bonus;
        }

        // =====================================================================
        // 防御 buff
        // =====================================================================

        /// <summary>计算将军卡防御加成百分比（100 = ×1.0 基准）</summary>
        public static int EvalDefense(IConfigRegistry config, CommanderState commander, UnitState unit, ProvinceState province, WorldState world)
        {
            if (commander == null) return 100;
            var card = config.Get<CommanderConfig>(commander.generalCardId);
            if (card?.skills == null) return 100;

            int bonus = 0;
            foreach (var skill in card.skills)
            {
                switch (skill.type)
                {
                    case "defenseBonus":
                        bonus += skill.value;
                        break;
                    case "brigadeBonus":
                        if (unit != null && skill.stat == "attackDefense")
                            bonus += EvalBrigadeBonus(unit, skill.brigadeType, skill.value);
                        break;
                    case "terrainBonus":
                        if (province != null)
                            bonus += EvalTerrainBonus(province, skill.terrain, skill.value);
                        break;
                }
            }
            return 100 + bonus;
        }

        // =====================================================================
        // 士气 buff
        // =====================================================================

        /// <summary>计算将军卡士气加成</summary>
        public static int EvalMorale(IConfigRegistry config, CommanderState commander)
        {
            if (commander == null) return 0;
            var card = config.Get<CommanderConfig>(commander.generalCardId);
            if (card?.skills == null) return 0;

            int bonus = 0;
            foreach (var skill in card.skills)
            {
                if (skill.type == "moraleBonus")
                    bonus += skill.value;
            }
            return bonus;
        }

        // =====================================================================
        // 补给消耗减少
        // =====================================================================

        /// <summary>补给消耗减少百分比（0-100）</summary>
        public static int EvalSupplyConsumptionReduction(IConfigRegistry config, CommanderState commander)
        {
            if (commander == null) return 0;
            var card = config.Get<CommanderConfig>(commander.generalCardId);
            if (card?.skills == null) return 0;

            int reduction = 0;
            foreach (var skill in card.skills)
            {
                if (skill.type == "supplyConsumptionReduction")
                    reduction += skill.value;
            }
            return Math.Min(75, reduction); // 上限 75%
        }

        // =====================================================================
        // 补员速率加成
        // =====================================================================

        /// <summary>补员速率加成百分比</summary>
        public static int EvalReinforceRateBonus(IConfigRegistry config, CommanderState commander)
        {
            if (commander == null) return 0;
            var card = config.Get<CommanderConfig>(commander.generalCardId);
            if (card?.skills == null) return 0;

            int bonus = 0;
            foreach (var skill in card.skills)
            {
                if (skill.type == "reinforceRateBonus")
                    bonus += skill.value;
            }
            return bonus;
        }

        // =====================================================================
        // 切断衰减倍率（50 = 组织度/士气衰减减半）
        // =====================================================================

        /// <summary>切断衰减倍率（100 = 正常，50 = 减半）</summary>
        public static int EvalCutoffDecayMultiplier(IConfigRegistry config, CommanderState commander)
        {
            if (commander == null) return 100;
            var card = config.Get<CommanderConfig>(commander.generalCardId);
            if (card?.skills == null) return 100;

            int minMultiplier = 100;
            foreach (var skill in card.skills)
            {
                if (skill.type == "cutoffDecayMultiplier")
                    minMultiplier = Math.Min(minMultiplier, skill.value);
            }
            return Math.Max(25, minMultiplier); // 下限 25%
        }

        // =====================================================================
        // 突破加成
        // =====================================================================

        /// <summary>突破加成百分比</summary>
        public static int EvalBreakthrough(IConfigRegistry config, CommanderState commander)
        {
            if (commander == null) return 0;
            var card = config.Get<CommanderConfig>(commander.generalCardId);
            if (card?.skills == null) return 0;

            int bonus = 0;
            foreach (var skill in card.skills)
            {
                if (skill.type == "breakthroughBonus")
                    bonus += skill.value;
            }
            return bonus;
        }

        // =====================================================================
        // 速度加成
        // =====================================================================

        /// <summary>速度加成（回合移动距离 +N）</summary>
        public static int EvalSpeedBonus(IConfigRegistry config, CommanderState commander)
        {
            if (commander == null) return 0;
            var card = config.Get<CommanderConfig>(commander.generalCardId);
            if (card?.skills == null) return 0;

            int bonus = 0;
            foreach (var skill in card.skills)
            {
                if (skill.type == "speedBonus")
                    bonus += skill.value;
            }
            return bonus;
        }

        // =====================================================================
        // 战役经验速率加成
        // =====================================================================

        /// <summary>战役经验获取速率加成百分比</summary>
        public static int EvalTacticalExpRateBonus(IConfigRegistry config, CommanderState commander)
        {
            if (commander == null) return 0;
            var card = config.Get<CommanderConfig>(commander.generalCardId);
            if (card?.skills == null) return 0;

            int bonus = 0;
            foreach (var skill in card.skills)
            {
                if (skill.type == "tacticalExpRateBonus")
                    bonus += skill.value;
            }
            return bonus;
        }

        // =====================================================================
        // 内部辅助
        // =====================================================================

        /// <summary>兵种加成：检查 unit 的 brigade 是否匹配指定类型</summary>
        private static int EvalBrigadeBonus(UnitState unit, string brigadeType, int value)
        {
            if (string.IsNullOrEmpty(brigadeType)) return 0;
            // 检查 unit 的兵种标签是否匹配
            if (unit.brigades != null)
            {
                foreach (var b in unit.brigades)
                {
                    if (b.brigadeType == brigadeType)
                        return value;
                }
            }
            return 0;
        }

        /// <summary>地形加成：检查省份地形是否匹配</summary>
        private static int EvalTerrainBonus(ProvinceState province, string terrain, int value)
        {
            if (string.IsNullOrEmpty(terrain)) return 0;
            if (province.terrain.ToString() == terrain)
                return value;
            return 0;
        }
    }
}

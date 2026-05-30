// ============================================================================
// Simulation/MovementResolver.cs — 部队移动校验 & 移动力重置
// C2b：单步邻接移动（仅己方控制省）
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class MovementResolver
    {
        /// <summary>
        /// 尝试将部队移动到邻接己方控制省份。
        /// 校验链：unit存在 → 归属 → target存在 → 邻接 → 己方控制 → 移动力足够。
        /// </summary>
        public CommandResult TryMove(WorldState world, string unitId, string targetProvinceId, string playerCountryId)
        {
            if (!world.units.TryGetValue(unitId, out var unit))
                return CommandResult.Reject("部队不存在");

            if (unit.ownerCountry != playerCountryId)
                return CommandResult.Reject("非己方部队");

            if (!world.provinces.TryGetValue(targetProvinceId, out var target))
                return CommandResult.Reject("目标省份不存在");

            if (!world.provinces.TryGetValue(unit.currentProvinceId, out var current))
                return CommandResult.Reject("当前省份不存在");

            if (current.neighbors == null || !current.neighbors.Contains(targetProvinceId))
                return CommandResult.Reject("非邻接省份");

            if (target.controllerCountry != unit.ownerCountry)
                return CommandResult.Reject("非己方控制省份");

            if (unit.movesLeft < 1)
                return CommandResult.Reject("移动力不足");

            unit.currentProvinceId = targetProvinceId;
            unit.movesLeft -= 1;

            return CommandResult.Accept();
        }

        /// <summary>
        /// 回合开始时重置所有部队移动力。按 unit.id 升序遍历。
        /// </summary>
        public void ResetMovement(WorldState world)
        {
            foreach (var u in world.units.Values.OrderBy(u => u.id, System.StringComparer.Ordinal))
            {
                u.movesLeft = u.speed;
            }
        }
    }
}

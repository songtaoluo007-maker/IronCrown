// ============================================================================
// Simulation/OccupationResolver.cs — 占领抵抗结算器 (C6)
// 每回合处理被占领省份的 resistance 增减 + 起义事件
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using IronCrown.Contracts;
using IronCrown.Domain;

namespace IronCrown.Simulation
{
    public sealed class OccupationResolver
    {
        private readonly IEventPublisher _events;
        private readonly IRandom _rng;

        public OccupationResolver(IEventPublisher events, IRandom rng)
        {
            _events = events;
            _rng = rng;
        }

        /// <summary>
        /// 每回合 Settlement 阶段调用。
        /// 遍历所有被占领省份，执行 resistance 增减 + 起义判定。
        /// </summary>
        public void ResolveOccupation(WorldState world, EconomyConfig eco)
        {
            // 收集被占领省份（owner != controller）
            var occupied = world.provinces.Values
                .Where(p => p.IsOccupied)
                .ToList();

            foreach (var province in occupied)
            {
                // 判定占领方在该省是否有驻军
                bool hasGarrison = world.units.Values
                    .Any(u => u.ownerCountry == province.controllerCountry
                           && u.currentProvinceId == province.id);

                // resistance 增减
                if (hasGarrison)
                {
                    province.resistance = Math.Max(0,
                        province.resistance + eco.resistanceDecayWithGarrison);
                }
                else
                {
                    province.resistance = Math.Min(100,
                        province.resistance + eco.resistanceGrowWithoutGarrison);
                }

                // 起义判定（resistance >= 阈值）
                if (province.resistance >= eco.resistanceUprisingThreshold)
                {
                    int roll = _rng.Next(100);
                    if (roll < eco.resistanceUprisingChancePct)
                    {
                        if (hasGarrison)
                        {
                            // 有驻军：扣驻军 manpower/equipment
                            ApplyGarrisonDamage(world, province,
                                eco.resistanceGarrisonDamageManpower,
                                eco.resistanceGarrisonDamageEquipment);

                            _events.Publish(new ResistanceUprisingEvent
                            {
                                provinceId = province.id,
                                controllerCountry = province.controllerCountry,
                                ownerCountry = province.ownerCountry,
                                hasGarrison = true,
                                resistance = province.resistance,
                                result = "garrison_damage"
                            });
                        }
                        else
                        {
                            // 无驻军：起义独立，省份回归原主
                            string prevController = province.controllerCountry;
                            province.controllerCountry = province.ownerCountry;
                            province.resistance = 0;

                            _events.Publish(new ResistanceUprisingEvent
                            {
                                provinceId = province.id,
                                controllerCountry = prevController,
                                ownerCountry = province.ownerCountry,
                                hasGarrison = false,
                                resistance = province.resistance,
                                result = "liberated"
                            });
                        }
                    }
                }
            }
        }

        /// <summary>扣驻军的 manpower 和 equipment（从该省所有占领方部队中扣）</summary>
        private void ApplyGarrisonDamage(WorldState world, ProvinceState province,
            int manpowerDamage, int equipmentDamage)
        {
            var garrisonUnits = world.units.Values
                .Where(u => u.ownerCountry == province.controllerCountry
                         && u.currentProvinceId == province.id)
                .ToList();

            if (garrisonUnits.Count == 0) return;

            // 平摊到所有驻军单位
            int perUnitManpower = manpowerDamage / garrisonUnits.Count;
            int perUnitEquipment = equipmentDamage / garrisonUnits.Count;

            foreach (var unit in garrisonUnits)
            {
                unit.manpower = Math.Max(0, unit.manpower - perUnitManpower);
                unit.equipment = Math.Max(0, unit.equipment - perUnitEquipment);
            }
        }
    }
}

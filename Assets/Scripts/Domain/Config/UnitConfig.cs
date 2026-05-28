// ============================================================================
// Domain/Config/UnitConfig.cs — 单位类型配置
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    /// <summary>单位类型定义（配置用）</summary>
    [System.Serializable]
    public class UnitConfig
    {
        public string id;
        public string name;
        public int attack;
        public int defense;
        public int breakthrough;
        public int speed;
        public int hp;
        public int organization;
        public int armor;
        public int piercing;
        public int supplyConsumption;
        public string equipmentType;
        public Dictionary<string, int> cost;
    }
}

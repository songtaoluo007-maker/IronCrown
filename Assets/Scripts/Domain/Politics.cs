// ============================================================================
// Domain/Politics.cs — 政治系统数据模型
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>事件配置</summary>
    [System.Serializable]
    public class EventConfig
    {
        public string id;
        public string title;
        public string description;
        public string category;          // political/military/economic/diplomatic
        public EventCondition conditions;
        public EventOption[] options;
    }

    /// <summary>事件触发条件</summary>
    [System.Serializable]
    public class EventCondition
    {
        public string targetCountry;
        public int? minStability;
        public int? maxStability;
        public int? minWarSupport;
        public int? maxWarSupport;
        public int? minTurn;
        public int? maxTurn;
        public string requiredIdeology;
        public string[] requiredPolicies;
        public string[] requiredTechs;
        public string atWar;            // 与某国交战
        public string controlsProvince; // 控制某省
    }

    /// <summary>事件选项</summary>
    [System.Serializable]
    public class EventOption
    {
        public string text;
        public EventEffect effects;
    }

    /// <summary>事件效果</summary>
    [System.Serializable]
    public class EventEffect
    {
        public int stabilityChange;
        public int warSupportChange;
        public int legitimacyChange;
        public int treasuryChange;
        public int manpowerChange;
        public string addPolicy;
        public string removePolicy;
        public string addTech;
        public string declareWarOn;
        public string allyWith;
        public string gainProvince;
        public EventResourceChange[] resourceChanges;
    }

    [System.Serializable]
    public class EventResourceChange
    {
        public string resourceId;
        public int amount;
    }

    /// <summary>将领配置</summary>
    [System.Serializable]
    public class CommanderConfig
    {
        public string id;
        public string name;
        public string title;
        public int attackBonus;
        public int defenseBonus;
        public int logisticsBonus;
        public int moraleBonus;
        public string[] traits;          // 特性标签
    }
}

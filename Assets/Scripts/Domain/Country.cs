// ============================================================================
// Domain/Country.cs — 国家数据模型
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    /// <summary>国家运行时状态</summary>
    public class CountryState
    {
        // === 基础信息 ===
        public string id;
        public string name;
        public string capitalProvinceId;
        public Ideology ideology;

        // === 政治 ===
        public int stability;          // 稳定度 0-100
        public int warSupport;         // 战争支持度 0-100
        public int legitimacy;         // 政权合法性 0-100
        public int corruption;         // 腐败度 0-100
        public int bureaucracy;        // 行政能力 0-100
        public int propagandaPower;    // 宣传能力

        // === 经济 ===
        public int treasury;           // 国库余额
        public int taxIncome;          // 税收
        public int tradeIncome;        // 贸易收入
        public int militaryExpense;    // 军费
        public int civilExpense;       // 民生支出
        public int debt;               // 债务
        public int inflation;          // 通胀 0-100

        // === 工厂 ===
        public int civilianFactories;  // 民用工厂
        public int militaryFactories;  // 军用工厂
        public int dockyards;          // 造船厂
        public int usedCivilianFactories;
        public int usedMilitaryFactories;

        // === 资源库存 ===
        public Dictionary<string, int> resources = new();

        // === 装备库存 ===
        public int equipmentStockpile;

        // === 人力 ===
        public int manpower;           // 可用人力
        public int totalManpower;      // 总人力

        // === 科技 ===
        public HashSet<string> completedTechs = new();
        public string currentResearch;

        // === 政策 ===
        public HashSet<string> activePolicies = new();

        // === 军事 ===
        public List<string> unitIds = new();  // 拥有的部队 ID 列表

        // === 建造队列 ===
        public List<ConstructionOrder> constructionQueue = new();

        // === 方法 ===

        /// <summary>获取资源数量</summary>
        public int GetResource(string resourceId)
        {
            return resources.ContainsKey(resourceId) ? resources[resourceId] : 0;
        }

        /// <summary>修改资源</summary>
        public void ModifyResource(string resourceId, int amount)
        {
            if (!resources.ContainsKey(resourceId))
                resources[resourceId] = 0;
            resources[resourceId] += amount;
            if (resources[resourceId] < 0) resources[resourceId] = 0;
        }

        /// <summary>是否有足够资源</summary>
        public bool HasResources(Dictionary<string, int> cost)
        {
            foreach (var kv in cost)
            {
                if (GetResource(kv.Key) < kv.Value) return false;
            }
            return true;
        }

        /// <summary>消耗资源</summary>
        public bool ConsumeResources(Dictionary<string, int> cost)
        {
            if (!HasResources(cost)) return false;
            foreach (var kv in cost)
                ModifyResource(kv.Key, -kv.Value);
            return true;
        }
    }

    /// <summary>意识形态</summary>
    public enum Ideology
    {
        FreeRepublic,    // 自由共和
        MilitaryGov,     // 军政府
        ImperialOrder,   // 帝国秩序
        Collectivism,    // 工团主义
        NationalRevival, // 民族复兴
        Technocrat       // 技术官僚
    }
}

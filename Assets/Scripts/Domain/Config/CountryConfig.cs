// ============================================================================
// Domain/Config/CountryConfig.cs — 国家配置
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    /// <summary>国家配置（静态数据，从 JSON 加载）</summary>
    [System.Serializable]
    public class CountryConfig
    {
        public string id;
        public string name;
        public string capitalProvinceId;
        public string ideology;
        public int stability;
        public int warSupport;
        public int legitimacy;
        public int corruption;
        public int bureaucracy;
        public int treasury;
        public int taxIncome;
        public int tradeIncome;
        public int militaryExpense;
        public int civilExpense;
        public int civilianFactories;
        public int militaryFactories;
        public int dockyards;
        public int manpower;
        public int totalManpower;
        public Dictionary<string, int> resources;
    }
}

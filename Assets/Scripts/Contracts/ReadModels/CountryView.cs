// ============================================================================
// Contracts/ReadModels/CountryView.cs — 国家只读模型
// UI 层通过此 DTO 读取国家状态，不直接引用 Domain
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Contracts
{
    public sealed class CountryView
    {
        public string id;
        public string name;
        public string ideology;       // Ideology.ToString()
        public int treasury;
        public int stability;
        public int warSupport;
        public int legitimacy;
        public int civilianFactories;
        public int militaryFactories;
        public int dockyards;
        public int manpower;
        public int equipmentStockpile;
        public Dictionary<string, int> resources;
        public int constructionQueueCount;
        public int unitProductionQueueCount;
        public int taxLevel;
        public int civilLevel;
        public int warExhaustion;

        // C7: AI 求和
        public int peaceOfferCooldown;
        public string pendingPeaceOfferFrom;
        public int pendingPeaceOfferExpiry;
    public string color;
    public string capitalProvinceId;
    }
}

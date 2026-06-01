// ============================================================================
// Contracts/Events/ShopPurchasedEvent.cs — C17 商城购买事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct ShopPurchasedEvent
    {
        public string buyerCountry;
        public string itemKind;        // "bundle_10" / "ssr_ticket" / "specific_card"
        public int cost;
        public int atTurn;
    }
}

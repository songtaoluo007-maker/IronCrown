// ============================================================================
// Domain/State/WarRelation.cs — 双边战争关系
// countryA < countryB Ordinal 升序（唯一键）
// ============================================================================

namespace IronCrown.Domain
{
    public sealed class WarRelation
    {
        public string countryA;   // Ordinal 升序
        public string countryB;
        public int startTurn;
    }
}

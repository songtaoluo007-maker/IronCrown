// ============================================================================
// Domain/Diplomacy.cs — 外交系统数据模型
// WorldState 已迁至 Domain/State/WorldState.cs
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>外交关系</summary>
    public class DiplomacyRelation
    {
        public string countryA;
        public string countryB;
        public int opinion;
        public int threat;
        public int tradeDependency;
        public int ideologySimilarity;
        public int borderTension;
        public int warExhaustion;
        public DiplomacyStatus status;
        public int turnsAtWar;
        public int turnsAllied;
    }

    /// <summary>外交状态</summary>
    public enum DiplomacyStatus
    {
        Neutral,
        NonAggressionPact,
        TradeAgreement,
        MilitaryAccess,
        Alliance,
        AtWar,
        Capitulated,
        Puppet
    }
}

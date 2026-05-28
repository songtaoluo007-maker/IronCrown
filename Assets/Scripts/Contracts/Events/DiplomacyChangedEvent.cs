// ============================================================================
// Contracts/Events/DiplomacyChangedEvent.cs — 外交关系变更事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct DiplomacyChangedEvent
    {
        public string CountryA;
        public string CountryB;
        public int OldOpinion;
        public int NewOpinion;
    }
}

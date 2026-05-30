// ============================================================================
// Contracts/Events/WarDeclaredEvent.cs — 宣战事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct WarDeclaredEvent
    {
        public string countryA;
        public string countryB;
        public int startTurn;
    }
}

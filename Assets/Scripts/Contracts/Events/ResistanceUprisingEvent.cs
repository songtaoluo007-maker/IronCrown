// ============================================================================
// Contracts/Events/ResistanceUprisingEvent.cs — 抗议/起义事件
// ============================================================================

namespace IronCrown.Contracts
{
    public struct ResistanceUprisingEvent
    {
        public string provinceId;
        public string controllerCountry;   // 占领方
        public string ownerCountry;        // 原主权国
        public bool hasGarrison;            // 有驻军？
        public int resistance;              // 触发时 resistance 值
        public string result;               // "garrison_damage" | "liberated"
    }
}

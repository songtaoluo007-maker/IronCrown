// ============================================================================
// Application/Persistence/Migration_0to1.cs — 样例迁移器：v0 → v1
// P2.0b: 给缺 schemaVersion 的最老档补 schemaVersion=1
// 真实地图迁移器（每省→多格）留 P2.2，本单不写。
// ============================================================================

using Newtonsoft.Json.Linq;

namespace IronCrown.Application
{
    /// <summary>
    /// 样例迁移器：v0 → v1。
    /// 对无 schemaVersion 的最老存档，补上 schemaVersion=1。
    /// 字段本身不变——仅证明迁移管线工作。
    /// </summary>
    public sealed class Migration_0to1 : ISaveMigration
    {
        public int FromVersion => 0;

        public JObject Migrate(JObject raw)
        {
            // 给缺字段的旧档补 schemaVersion=1
            raw["schemaVersion"] = 1;
            return raw;
        }
    }
}

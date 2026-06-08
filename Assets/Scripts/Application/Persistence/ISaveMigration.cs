// ============================================================================
// Application/Persistence/ISaveMigration.cs — 存档迁移接口
// P2.0b: 每个迁移器负责 FromVersion → FromVersion+1 的升级
// ============================================================================

using Newtonsoft.Json.Linq;

namespace IronCrown.Application
{
    /// <summary>
    /// 存档迁移器接口。
    /// 每个实现负责将 schemaVersion == FromVersion 的存档升级到 FromVersion+1。
    /// </summary>
    public interface ISaveMigration
    {
        /// <summary>该迁移器适用的源版本（升级后变为 FromVersion+1）</summary>
        int FromVersion { get; }

        /// <summary>执行迁移，返回升级后的 JObject（schemaVersion 已 +1）</summary>
        JObject Migrate(JObject raw);
    }
}

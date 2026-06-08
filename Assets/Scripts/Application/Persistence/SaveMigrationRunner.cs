// ============================================================================
// Application/Persistence/SaveMigrationRunner.cs — 存档迁移 Runner
// P2.0b: 按 FromVersion 升序链式调用迁移器，将旧档升级到 CURRENT
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace IronCrown.Application
{
    /// <summary>
    /// 存档迁移 Runner。
    /// 持有迁移器列表（按 FromVersion 升序），入口 Upgrade 链式调用。
    /// </summary>
    public sealed class SaveMigrationRunner
    {
        private readonly List<ISaveMigration> _migrations;

        public SaveMigrationRunner(IEnumerable<ISaveMigration> migrations)
        {
            // 按 FromVersion 升序排列
            _migrations = migrations.OrderBy(m => m.FromVersion).ToList();
        }

        /// <summary>
        /// 升级存档 JSON。
        /// 读 raw["schemaVersion"]（缺失则默认 1），当 version &lt; CURRENT 时
        /// 链式调用对应迁移器，每步把 schemaVersion +1。
        /// </summary>
        public JObject Upgrade(JObject raw)
        {
            // 读取 schemaVersion，缺失默认 1（兼容最老存档）
            int version = raw.ContainsKey("schemaVersion")
                ? raw["schemaVersion"]!.Value<int>()
                : 1;

            // 逐步升级到 CURRENT
            while (version < SaveSchema.CURRENT)
            {
                var migration = _migrations.FirstOrDefault(m => m.FromVersion == version);
                if (migration == null)
                {
                    // 没有对应迁移器，无法升级——直接设为 CURRENT 并跳出
                    // （防御性处理，避免死循环；生产环境应记录警告）
                    raw["schemaVersion"] = SaveSchema.CURRENT;
                    break;
                }

                raw = migration.Migrate(raw);
                // Migrate 内部已将 schemaVersion 设为 FromVersion+1
                version = raw["schemaVersion"]!.Value<int>();
            }

            return raw;
        }
    }
}

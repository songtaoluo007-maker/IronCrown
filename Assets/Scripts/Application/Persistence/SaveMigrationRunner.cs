// ============================================================================
// Application/Persistence/SaveMigrationRunner.cs — 存档迁移 Runner
// P2.0b: 按 FromVersion 升序链式调用迁移器，将旧档升级到目标版本
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
        private readonly int _targetVersion;

        /// <param name="migrations">迁移器集合（内部按 FromVersion 升序）。</param>
        /// <param name="targetVersion">升级目标版本；&lt;0 时取 <see cref="SaveSchema.CURRENT"/>。
        /// 测试可注入更高目标以验证纯链式逻辑（独立于 CURRENT）。</param>
        public SaveMigrationRunner(IEnumerable<ISaveMigration> migrations, int targetVersion = -1)
        {
            _migrations = migrations.OrderBy(m => m.FromVersion).ToList();
            _targetVersion = targetVersion < 0 ? SaveSchema.CURRENT : targetVersion;
        }

        /// <summary>
        /// 升级存档 JSON。
        /// 读 raw["schemaVersion"]（**缺失视为 0** = 版本化之前的最老档，使 v0→v1 迁移器可达），
        /// 当 version &lt; targetVersion 时链式调用对应迁移器，每步 schemaVersion +1。
        /// </summary>
        public JObject Upgrade(JObject raw)
        {
            int version = raw.ContainsKey("schemaVersion")
                ? raw["schemaVersion"]!.Value<int>()
                : 0;

            while (version < _targetVersion)
            {
                var migration = _migrations.FirstOrDefault(m => m.FromVersion == version);
                if (migration == null)
                {
                    // 无对应迁移器：直接标到目标版本并跳出（防死循环；生产环境应记警告）
                    version = _targetVersion;
                    break;
                }

                raw = migration.Migrate(raw);
                // Migrate 内部已将 schemaVersion 设为 FromVersion+1
                version = raw["schemaVersion"]!.Value<int>();
            }

            // 确保字段存在（含"已是最新、无需迁移"与"缺失被默认为 0"的档）
            raw["schemaVersion"] = version;
            return raw;
        }
    }
}

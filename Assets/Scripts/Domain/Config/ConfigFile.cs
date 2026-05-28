// ============================================================================
// Domain/Config/ConfigFile.cs — 通用配置文件外层包装
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    /// <summary>JSON 配置文件外层结构 { "schemaVersion": N, "items": [...] }</summary>
    [System.Serializable]
    public class ConfigFile<T>
    {
        public int schemaVersion;
        public List<T> items;
    }
}

// ============================================================================
// Domain/Config/IConfigRegistry.cs — 配置注册表接口
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Domain
{
    /// <summary>配置注册表 — 按类型查询配置数据</summary>
    public interface IConfigRegistry
    {
        /// <summary>按 ID 获取单条配置</summary>
        T Get<T>(string id) where T : class;

        /// <summary>获取某类型全部配置</summary>
        IReadOnlyList<T> All<T>() where T : class;

        /// <summary>是否存在某 ID 的配置</summary>
        bool Has<T>(string id) where T : class;

        /// <summary>加载全部配置（启动时调用一次）</summary>
        void LoadAll();
    }
}

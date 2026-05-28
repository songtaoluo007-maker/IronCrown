// ============================================================================
// Domain/Config/ResourceConfig.cs — 资源配置定义
// ============================================================================

namespace IronCrown.Domain
{
    /// <summary>资源配置定义</summary>
    [System.Serializable]
    public class ResourceConfig
    {
        public string id;
        public string name;
        public string description;
        public int basePrice;            // 基础贸易价格
    }
}

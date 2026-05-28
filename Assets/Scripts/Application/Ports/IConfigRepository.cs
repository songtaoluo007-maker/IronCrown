// ============================================================================
// Application/Ports/IConfigRepository.cs — 配置读取接口
// ============================================================================

using System.Collections.Generic;

namespace IronCrown.Application
{
    public interface IConfigRepository
    {
        T Load<T>(string configName) where T : class;
        List<T> LoadList<T>(string configName) where T : class;
        void ClearCache();
    }
}

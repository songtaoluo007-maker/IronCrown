// ============================================================================
// Application/Ports/IAppLogger.cs — 日志接口（取代 UnityEngine.Debug）
// ============================================================================

namespace IronCrown.Application
{
    public interface IAppLogger
    {
        void Info(string msg);
        void Warn(string msg);
        void Error(string msg);
    }
}

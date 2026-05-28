// ============================================================================
// Infrastructure/Logging/UnityAppLogger.cs — Unity 日志适配器
// ============================================================================

using IronCrown.Application;
using UnityEngine;

namespace IronCrown.Infrastructure
{
    public sealed class UnityAppLogger : IAppLogger
    {
        public void Info(string msg) => Debug.Log(msg);
        public void Warn(string msg) => Debug.LogWarning(msg);
        public void Error(string msg) => Debug.LogError(msg);
    }
}

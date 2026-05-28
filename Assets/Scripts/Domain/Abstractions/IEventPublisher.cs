// ============================================================================
// Domain/Abstractions/IEventPublisher.cs — 事件发布接口
// ============================================================================

using System;

namespace IronCrown.Domain
{
    public interface IEventPublisher
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T evt);
        void Clear();
    }
}

// ============================================================================
// Contracts/Abstractions/IEventPublisher.cs — 事件发布接口
// 从 Domain 迁入 Contracts，保持零依赖
// ============================================================================

using System;

namespace IronCrown.Contracts
{
    public interface IEventPublisher
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T evt);
        void Clear();
    }
}

// ============================================================================
// Domain/Messaging/EventBus.cs — 事件总线（实现 IEventPublisher）
// 事件结构体已迁至 Contracts/Events/，IEventPublisher 迁至 Contracts/Abstractions/
// ============================================================================

using System;
using System.Collections.Generic;
using IronCrown.Contracts;

namespace IronCrown.Domain
{
    /// <summary>
    /// 轻量级事件总线，用于 Domain / Simulation / Presentation 层之间解耦通信。
    /// 禁止在 UI 层直接修改 Domain 状态，必须通过事件驱动。
    /// </summary>
    public sealed class EventBus : IEventPublisher
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<Delegate>();
            _handlers[type].Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.ContainsKey(type))
                _handlers[type].Remove(handler);
        }

        public void Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type)) return;
            foreach (var handler in _handlers[type].ToArray())
            {
                ((Action<T>)handler)?.Invoke(eventData);
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}

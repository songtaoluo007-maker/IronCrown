// ============================================================================
// Domain/Messaging/EventBus.cs — 事件总线（实现 IEventPublisher）
// 从 Core/EventBus.cs 迁移，删除 static Instance 单例
// ============================================================================

using System;
using System.Collections.Generic;

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

    // ==========================================================================
    // 预定义事件（随 EventBus 迁入 Domain）
    // ==========================================================================

    public struct TurnStartEvent
    {
        public int TurnNumber;
    }

    public struct TurnEndEvent
    {
        public int TurnNumber;
    }

    public struct ResourceChangedEvent
    {
        public string CountryId;
        public string ResourceId;
        public int OldValue;
        public int NewValue;
    }

    public struct ProvinceOwnerChangedEvent
    {
        public string ProvinceId;
        public string OldOwner;
        public string NewOwner;
    }

    public struct BattleResolvedEvent
    {
        public string AttackerId;
        public string DefenderId;
        public string ProvinceId;
        public bool AttackerWon;
    }

    public struct PolicyChangedEvent
    {
        public string CountryId;
        public string PolicyId;
        public bool Activated;
    }

    public struct TechCompletedEvent
    {
        public string CountryId;
        public string TechId;
    }

    public struct DiplomacyChangedEvent
    {
        public string CountryA;
        public string CountryB;
        public int OldOpinion;
        public int NewOpinion;
    }
}

// ============================================================================
// Tests/EditMode/EventBusTests.cs — EventBus 单元测试
// ============================================================================

using NUnit.Framework;
using IronCrown.Domain;
using IronCrown.Contracts;

namespace IronCrown.Domain.Tests
{
    public class EventBusTests
    {
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
        }

        [Test]
        public void Subscribe_Publish_ReceivesEvent()
        {
            int received = 0;
            _bus.Subscribe<TurnStartEvent>(e => received = e.TurnNumber);
            _bus.Publish(new TurnStartEvent { TurnNumber = 5 });
            Assert.AreEqual(5, received);
        }

        [Test]
        public void Unsubscribe_StopsReceiving()
        {
            int received = 0;
            void Handler(TurnStartEvent e) => received = e.TurnNumber;
            _bus.Subscribe<TurnStartEvent>(Handler);
            _bus.Unsubscribe<TurnStartEvent>(Handler);
            _bus.Publish(new TurnStartEvent { TurnNumber = 99 });
            Assert.AreEqual(0, received);
        }

        [Test]
        public void Clear_RemovesAllSubscriptions()
        {
            int received = 0;
            _bus.Subscribe<TurnStartEvent>(e => received = e.TurnNumber);
            _bus.Clear();
            _bus.Publish(new TurnStartEvent { TurnNumber = 1 });
            Assert.AreEqual(0, received);
        }

        [Test]
        public void MultipleSubscribers_AllReceive()
        {
            int count = 0;
            _bus.Subscribe<TurnStartEvent>(_ => count++);
            _bus.Subscribe<TurnStartEvent>(_ => count++);
            _bus.Publish(new TurnStartEvent { TurnNumber = 1 });
            Assert.AreEqual(2, count);
        }
    }
}



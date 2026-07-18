using Hase.Protocol;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeProtocolNotificationSubscriptionsTests
{
    [Fact]
    public void Subscribe_NullObserver_ShouldThrow()
    {
        // Arrange
        var subscriptions =
            new RuntimeProtocolNotificationSubscriptions();

        // Act
        void Act()
        {
            subscriptions.Subscribe(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "observer",
            exception.ParamName);
    }

    [Fact]
    public void Subscribe_SameObserverTwice_ShouldAttachOnce()
    {
        // Arrange
        var subscriptions =
            new RuntimeProtocolNotificationSubscriptions();

        var source =
            new TestNotificationSource();

        var observer =
            new TestNotificationObserver();

        subscriptions.Attach(
            source);

        // Act
        subscriptions.Subscribe(
            observer);

        subscriptions.Subscribe(
            observer);

        // Assert
        Assert.Equal(
            1,
            source.SubscribeCallCount);

        Assert.Same(
            observer,
            Assert.Single(
                source.Observers));
    }

    [Fact]
    public void Attach_ReplacementSource_ShouldMoveSubscriptions()
    {
        // Arrange
        var subscriptions =
            new RuntimeProtocolNotificationSubscriptions();

        var initialSource =
            new TestNotificationSource();

        var replacementSource =
            new TestNotificationSource();

        var firstObserver =
            new TestNotificationObserver();

        var secondObserver =
            new TestNotificationObserver();

        subscriptions.Subscribe(
            firstObserver);

        subscriptions.Subscribe(
            secondObserver);

        subscriptions.Attach(
            initialSource);

        // Act
        subscriptions.Attach(
            replacementSource);

        // Assert
        Assert.Empty(
            initialSource.Observers);

        Assert.Equal(
            2,
            initialSource.UnsubscribeCallCount);

        Assert.Equal(
            2,
            replacementSource.SubscribeCallCount);

        Assert.Equal(
            new[]
            {
                firstObserver,
                secondObserver
            },
            replacementSource.Observers);
    }

    [Fact]
    public void Unsubscribe_ShouldRemoveCurrentAndPersistentSubscription()
    {
        // Arrange
        var subscriptions =
            new RuntimeProtocolNotificationSubscriptions();

        var initialSource =
            new TestNotificationSource();

        var replacementSource =
            new TestNotificationSource();

        var observer =
            new TestNotificationObserver();

        subscriptions.Subscribe(
            observer);

        subscriptions.Attach(
            initialSource);

        // Act
        subscriptions.Unsubscribe(
            observer);

        subscriptions.Attach(
            replacementSource);

        // Assert
        Assert.Empty(
            initialSource.Observers);

        Assert.Equal(
            1,
            initialSource.UnsubscribeCallCount);

        Assert.Empty(
            replacementSource.Observers);

        Assert.Equal(
            0,
            replacementSource.SubscribeCallCount);
    }

    private sealed class TestNotificationSource
        : IRuntimeProtocolNotificationSource
    {
        private readonly List<IProtocolNotificationObserver>
            _observers =
                [];

        public IReadOnlyList<IProtocolNotificationObserver>
            Observers =>
                _observers;

        public int SubscribeCallCount
        {
            get;
            private set;
        }

        public int UnsubscribeCallCount
        {
            get;
            private set;
        }

        public void SubscribeNotification(
            IProtocolNotificationObserver observer)
        {
            ArgumentNullException.ThrowIfNull(
                observer);

            SubscribeCallCount++;

            if (!_observers.Contains(
                    observer))
            {
                _observers.Add(
                    observer);
            }
        }

        public void UnsubscribeNotification(
            IProtocolNotificationObserver observer)
        {
            ArgumentNullException.ThrowIfNull(
                observer);

            UnsubscribeCallCount++;

            _observers.Remove(
                observer);
        }
    }

    private sealed class TestNotificationObserver
        : IProtocolNotificationObserver
    {
        public void OnProtocolNotification(
            ProtocolMessage notification)
        {
        }
    }
}
namespace Hase.Transport.Tests;

public sealed class TransportExchangeTracePublisherTests
{
    [Fact]
    public void Subscribe_NullObserver_ShouldThrow()
    {
        var publisher =
            new TransportExchangeTracePublisher();

        Assert.Throws<ArgumentNullException>(
            () => publisher.Subscribe(
                null!));
    }

    [Fact]
    public void Unsubscribe_NullObserver_ShouldThrow()
    {
        var publisher =
            new TransportExchangeTracePublisher();

        Assert.Throws<ArgumentNullException>(
            () => publisher.Unsubscribe(
                null!));
    }

    [Fact]
    public void Publish_SubscribedObserver_ShouldReceiveTrace()
    {
        var publisher =
            new TransportExchangeTracePublisher();

        var observer =
            new TestObserver();

        TransportExchangeTrace trace =
            CreateTrace();

        publisher.Subscribe(
            observer);

        publisher.Publish(
            trace);

        Assert.Equal(
            new[] { trace },
            observer.Traces);
    }

    [Fact]
    public void Subscribe_SameObserverTwice_ShouldNotifyOnce()
    {
        var publisher =
            new TransportExchangeTracePublisher();

        var observer =
            new TestObserver();

        TransportExchangeTrace trace =
            CreateTrace();

        publisher.Subscribe(
            observer);

        publisher.Subscribe(
            observer);

        publisher.Publish(
            trace);

        Assert.Single(
            observer.Traces);

        Assert.Same(
            trace,
            observer.Traces[0]);
    }

    [Fact]
    public void Unsubscribe_Observer_ShouldStopNotifications()
    {
        var publisher =
            new TransportExchangeTracePublisher();

        var observer =
            new TestObserver();

        TransportExchangeTrace firstTrace =
            CreateTrace(
                sequenceNumber:
                    1);

        TransportExchangeTrace secondTrace =
            CreateTrace(
                sequenceNumber:
                    2);

        publisher.Subscribe(
            observer);

        publisher.Publish(
            firstTrace);

        publisher.Unsubscribe(
            observer);

        publisher.Publish(
            secondTrace);

        Assert.Equal(
            new[] { firstTrace },
            observer.Traces);
    }

    [Fact]
    public void Unsubscribe_UnknownObserver_ShouldBeHarmless()
    {
        var publisher =
            new TransportExchangeTracePublisher();

        var subscribedObserver =
            new TestObserver();

        var unknownObserver =
            new TestObserver();

        TransportExchangeTrace trace =
            CreateTrace();

        publisher.Subscribe(
            subscribedObserver);

        publisher.Unsubscribe(
            unknownObserver);

        publisher.Publish(
            trace);

        Assert.Equal(
            new[] { trace },
            subscribedObserver.Traces);

        Assert.Empty(
            unknownObserver.Traces);
    }

    [Fact]
    public void Publish_SelfRemovingObserver_ShouldNotAffectCurrentPublication()
    {
        var publisher =
            new TransportExchangeTracePublisher();

        var selfRemovingObserver =
            new SelfRemovingObserver(
                publisher);

        var secondObserver =
            new TestObserver();

        TransportExchangeTrace firstTrace =
            CreateTrace(
                sequenceNumber:
                    1);

        TransportExchangeTrace secondTrace =
            CreateTrace(
                sequenceNumber:
                    2);

        publisher.Subscribe(
            selfRemovingObserver);

        publisher.Subscribe(
            secondObserver);

        publisher.Publish(
            firstTrace);

        publisher.Publish(
            secondTrace);

        Assert.Equal(
            new[] { firstTrace },
            selfRemovingObserver.Traces);

        Assert.Equal(
            new[]
            {
                firstTrace,
                secondTrace
            },
            secondObserver.Traces);
    }

    [Fact]
    public void Publish_ThrowingObserver_ShouldNotPreventLaterObserver()
    {
        var publisher =
            new TransportExchangeTracePublisher();

        var throwingObserver =
            new ThrowingObserver();

        var successfulObserver =
            new TestObserver();

        TransportExchangeTrace trace =
            CreateTrace();

        publisher.Subscribe(
            throwingObserver);

        publisher.Subscribe(
            successfulObserver);

        Exception? exception =
            Record.Exception(
                () => publisher.Publish(
                    trace));

        Assert.Null(
            exception);

        Assert.Equal(
            1,
            throwingObserver.CallCount);

        Assert.Equal(
            new[] { trace },
            successfulObserver.Traces);
    }

    [Fact]
    public async Task SubscriptionAndPublication_ShouldBeThreadSafe()
    {
        var publisher =
            new TransportExchangeTracePublisher();

        TestObserver[] observers =
            Enumerable.Range(
                    0,
                    50)
                .Select(
                    _ => new TestObserver())
                .ToArray();

        TransportExchangeTrace trace =
            CreateTrace();

        await Task.WhenAll(
            observers.Select(
                observer =>
                    Task.Run(
                        () => publisher.Subscribe(
                            observer))));

        await Task.WhenAll(
            Enumerable.Range(
                    0,
                    20)
                .Select(
                    _ =>
                        Task.Run(
                            () => publisher.Publish(
                                trace))));

        foreach (TestObserver observer
                 in observers)
        {
            Assert.Equal(
                20,
                observer.Traces.Count);
        }
    }

    private static TransportExchangeTrace CreateTrace(
        long sequenceNumber = 1)
    {
        DateTimeOffset startedAtUtc =
            DateTimeOffset.FromUnixTimeMilliseconds(
                1_750_000_000_000);

        return new TransportExchangeTrace(
            sequenceNumber,
            startedAtUtc,
            startedAtUtc.AddMilliseconds(
                10),
            TimeSpan.FromMilliseconds(
                10),
            requestByteCount:
                16,
            responseByteCount:
                32,
            TransportExchangeOutcome.Succeeded,
            TransportConnectionState.Connected);
    }

    private class TestObserver
        : ITransportExchangeTraceObserver
    {
        private readonly object _syncRoot =
            new();

        private readonly List<TransportExchangeTrace> _traces =
            [];

        public IReadOnlyList<TransportExchangeTrace> Traces
        {
            get
            {
                lock (_syncRoot)
                {
                    return _traces.ToArray();
                }
            }
        }

        public virtual void OnTransportExchangeCompleted(
            TransportExchangeTrace trace)
        {
            ArgumentNullException.ThrowIfNull(
                trace);

            lock (_syncRoot)
            {
                _traces.Add(
                    trace);
            }
        }
    }

    private sealed class SelfRemovingObserver
        : TestObserver
    {
        private readonly TransportExchangeTracePublisher _publisher;

        public SelfRemovingObserver(
            TransportExchangeTracePublisher publisher)
        {
            _publisher =
                publisher
                ?? throw new ArgumentNullException(
                    nameof(publisher));
        }

        public override void OnTransportExchangeCompleted(
            TransportExchangeTrace trace)
        {
            base.OnTransportExchangeCompleted(
                trace);

            _publisher.Unsubscribe(
                this);
        }
    }

    private sealed class ThrowingObserver
        : ITransportExchangeTraceObserver
    {
        public int CallCount
        {
            get;
            private set;
        }

        public void OnTransportExchangeCompleted(
            TransportExchangeTrace trace)
        {
            ArgumentNullException.ThrowIfNull(
                trace);

            CallCount++;

            throw new InvalidOperationException(
                "Observer failure.");
        }
    }
}
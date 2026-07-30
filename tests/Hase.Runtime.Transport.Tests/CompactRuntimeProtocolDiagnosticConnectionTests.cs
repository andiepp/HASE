using Hase.CompactProtocol;
using Hase.Runtime.Diagnostics;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactRuntimeProtocolDiagnosticConnectionTests
{
    [Fact]
    public async Task ExchangeAsync_SuccessReturnsSameFrameAndPublishesMetadata()
    {
        CompactSerialFrame expected =
            new(
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 7,
                payload:
                [
                    0x12,
                    0x00,
                    0xA5
                ]);

        var inner =
            new StubConnection(
                _ =>
                    Task.FromResult(
                        expected));

        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        ICompactSerialProtocolConnection connection =
            CreateDecorator(
                inner,
                collector);

        CompactSerialFrame actual =
            await connection.ExchangeAsync(
                CreateRequest());

        Assert.Same(
            expected,
            actual);

        IReadOnlyList<RuntimeDiagnosticRecord> records =
            collector.GetSnapshot();

        Assert.Equal(
            [
                "ProtocolRequestSent",
                "ProtocolResponseReceived"
            ],
            records
                .Select(
                    record =>
                        record.EventName)
                .ToArray());

        Assert.Equal(
            "ReadPropertyRequest",
            records[0].Details["messageKind"]);
        Assert.Equal(
            "ReadPropertyResponse",
            records[1].Details["messageKind"]);
        Assert.Equal(
            "7",
            records[0].Details["correlationId"]);
        Assert.Equal(
            "1",
            records[0].Details["payloadLength"]);
        Assert.Equal(
            "3",
            records[1].Details["payloadLength"]);
        Assert.Equal(
            4,
            records[1].Details.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExchangeAsync_ExceptionIsRethrownUnchangedAndClassified(
        int exceptionKind)
    {
        Exception expected =
            exceptionKind switch
            {
                0 =>
                    new TimeoutException(
                        "sensitive timeout"),

                1 =>
                    new OperationCanceledException(
                        "sensitive cancellation"),

                _ =>
                    new InvalidOperationException(
                        "sensitive failure")
            };

        var inner =
            new StubConnection(
                _ =>
                    Task.FromException<CompactSerialFrame>(
                        expected));

        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        ICompactSerialProtocolConnection connection =
            CreateDecorator(
                inner,
                collector);

        Exception actual =
            await Assert.ThrowsAnyAsync<Exception>(
                () =>
                    connection.ExchangeAsync(
                        CreateRequest()));

        Assert.Same(
            expected,
            actual);

        RuntimeDiagnosticRecord failed =
            collector.GetSnapshot()[1];

        Assert.Equal(
            exceptionKind switch
            {
                0 =>
                    RuntimeDiagnosticOutcome.TimedOut,

                1 =>
                    RuntimeDiagnosticOutcome.Cancelled,

                _ =>
                    RuntimeDiagnosticOutcome.Failed
            },
            failed.Outcome);

        Assert.DoesNotContain(
            failed.Details,
            detail =>
                detail.Value.Contains(
                    "sensitive",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OperationalOnlyCollector_PreservesExchangeWithoutRecords()
    {
        CompactSerialFrame expected =
            new(
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 7,
                payload:
                [
                    0x12,
                    0x00
                ]);

        var inner =
            new StubConnection(
                _ =>
                    Task.FromResult(
                        expected));

        BoundedRuntimeDiagnosticCollector collector =
            new(
                4,
                RuntimeDiagnosticLevel.Operational);

        ICompactSerialProtocolConnection connection =
            CreateDecorator(
                inner,
                collector);

        CompactSerialFrame actual =
            await connection.ExchangeAsync(
                CreateRequest());

        Assert.Same(
            expected,
            actual);
        Assert.Empty(
            collector.GetSnapshot());
    }

    [Fact]
    public void Create_PreservesOptionalTraceCapabilityShape()
    {
        RuntimeDiagnosticPublisher diagnostics =
            new();

        ICompactSerialProtocolConnection plain =
            CompactRuntimeProtocolDiagnosticConnection.Create(
                new StubConnection(),
                "endpoint-one",
                diagnostics);

        ICompactSerialProtocolConnection trace =
            CompactRuntimeProtocolDiagnosticConnection.Create(
                new TraceConnection(),
                "endpoint-one",
                diagnostics);

        Assert.IsNotAssignableFrom<ITransportExchangeTraceSource>(
            plain);
        Assert.IsAssignableFrom<ITransportExchangeTraceSource>(
            trace);
    }

    [Fact]
    public void Decorator_ForwardsLifecycleNotificationAndTraceMembers()
    {
        var inner =
            new TraceConnection();

        ICompactSerialProtocolConnection decorated =
            CompactRuntimeProtocolDiagnosticConnection.Create(
                inner,
                "endpoint-one",
                new RuntimeDiagnosticPublisher());

        EventHandler<TransportConnectionStateChangedEventArgs>
            stateHandler =
                (_, _) =>
                {
                };

        Action<CompactEventNotification> notificationHandler =
            _ =>
            {
            };

        decorated.StateChanged +=
            stateHandler;
        decorated.StateChanged -=
            stateHandler;

        decorated.EventNotificationReceived +=
            notificationHandler;
        decorated.EventNotificationReceived -=
            notificationHandler;

        decorated.Invalidate();

        var traceSource =
            Assert.IsAssignableFrom<ITransportExchangeTraceSource>(
                decorated);

        var traceObserver =
            new TestTraceObserver();

        traceSource.SubscribeTrace(
            traceObserver);
        traceSource.UnsubscribeTrace(
            traceObserver);

        Assert.Equal(
            1,
            inner.StateSubscribeCount);
        Assert.Equal(
            1,
            inner.StateUnsubscribeCount);
        Assert.Equal(
            1,
            inner.NotificationSubscribeCount);
        Assert.Equal(
            1,
            inner.NotificationUnsubscribeCount);
        Assert.Equal(
            1,
            inner.InvalidateCount);
        Assert.Equal(
            1,
            inner.TraceSubscribeCount);
        Assert.Equal(
            1,
            inner.TraceUnsubscribeCount);
    }

    [Fact]
    public async Task DisposeAsync_ForwardsToInnerConnection()
    {
        var inner =
            new StubConnection();

        ICompactSerialProtocolConnection decorated =
            CompactRuntimeProtocolDiagnosticConnection.Create(
                inner,
                "endpoint-one",
                new RuntimeDiagnosticPublisher());

        await decorated.DisposeAsync();

        Assert.Equal(
            1,
            inner.DisposeCount);
    }

    [Fact]
    public void NotificationObserver_PublishesPayloadFreeInboundMetadata()
    {
        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        var observer =
            new CompactProtocolNotificationDiagnosticObserver(
                "endpoint-one",
                new RuntimeDiagnosticPublisher(
                    collector));

        observer.OnEventNotification(
            new CompactEventNotification(
                eventId: 3,
                "sensitive payload"u8.ToArray()));

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot());

        Assert.Equal(
            "ProtocolNotificationReceived",
            record.EventName);
        Assert.Equal(
            RuntimeDiagnosticDirection.Inbound,
            record.Direction);
        Assert.Equal(
            "EventNotification",
            record.Details["messageKind"]);
        Assert.Equal(
            "0",
            record.Details["correlationId"]);
        Assert.Equal(
            "18",
            record.Details["payloadLength"]);
        Assert.Equal(
            4,
            record.Details.Count);
        Assert.DoesNotContain(
            record.Details,
            detail =>
                detail.Value.Contains(
                    "sensitive",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ActivatedDecorator_OwnsOneNotificationSubscription()
    {
        var inner =
            new TraceConnection();

        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        ICompactSerialProtocolConnection decorated =
            CompactRuntimeProtocolDiagnosticConnection.Create(
                inner,
                "endpoint-one",
                new RuntimeDiagnosticPublisher(
                    collector),
                observeNotifications: true);

        inner.PublishNotification(
            new CompactEventNotification(
                eventId: 3,
                value: new byte[]
                {
                    0xA5
                }));

        Assert.Single(
            collector.GetSnapshot());
        Assert.Equal(
            1,
            inner.NotificationSubscribeCount);

        await decorated.DisposeAsync();

        Assert.Equal(
            1,
            inner.NotificationUnsubscribeCount);

        inner.PublishNotification(
            new CompactEventNotification(
                eventId: 3,
                value: new byte[]
                {
                    0x5A
                }));

        Assert.Single(
            collector.GetSnapshot());
    }

    [Fact]
    public async Task ThrowingSink_DoesNotChangeExchangeResult()
    {
        CompactSerialFrame expected =
            new(
                (byte)CompactSerialMessageType.ReadPropertyResponse,
                correlationId: 7,
                payload:
                [
                    0x12,
                    0x00
                ]);

        var inner =
            new StubConnection(
                _ =>
                    Task.FromResult(
                        expected));

        ICompactSerialProtocolConnection connection =
            CompactRuntimeProtocolDiagnosticConnection.Create(
                inner,
                "endpoint-one",
                new RuntimeDiagnosticPublisher(
                    new ThrowingSink()));

        CompactSerialFrame actual =
            await connection.ExchangeAsync(
                CreateRequest());

        Assert.Same(
            expected,
            actual);
    }

    private static CompactSerialFrame CreateRequest()
    {
        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.ReadPropertyRequest,
            correlationId: 7,
            payload:
            [
                0x12
            ]);
    }

    private static BoundedRuntimeDiagnosticCollector CreateProtocolCollector()
    {
        return new BoundedRuntimeDiagnosticCollector(
            10,
            RuntimeDiagnosticLevel.Protocol);
    }

    private static ICompactSerialProtocolConnection CreateDecorator(
        ICompactSerialProtocolConnection inner,
        BoundedRuntimeDiagnosticCollector collector)
    {
        return CompactRuntimeProtocolDiagnosticConnection.Create(
            inner,
            "endpoint-one",
            new RuntimeDiagnosticPublisher(
                collector));
    }

    private class StubConnection
        : ICompactSerialProtocolConnection
    {
        private readonly Func<
            CompactSerialFrame,
            Task<CompactSerialFrame>> exchangeAsync;

        public StubConnection(
            Func<
                CompactSerialFrame,
                Task<CompactSerialFrame>>? exchangeAsync = null)
        {
            this.exchangeAsync =
                exchangeAsync
                ?? (_ =>
                    Task.FromResult(
                        new CompactSerialFrame(
                            (byte)CompactSerialMessageType
                                .ReadPropertyResponse,
                            correlationId: 7,
                            payload:
                            [
                                0x12,
                                0x00
                            ])));
        }

        public virtual event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public virtual event Action<CompactEventNotification>?
            EventNotificationReceived;

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public int DisposeCount
        {
            get;
            private set;
        }

        public int InvalidateCount
        {
            get;
            private set;
        }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            return exchangeAsync(
                request);
        }

        public void Invalidate()
        {
            InvalidateCount++;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TraceConnection
        : StubConnection,
          ITransportExchangeTraceSource
    {
        private EventHandler<TransportConnectionStateChangedEventArgs>?
            stateChanged;

        private Action<CompactEventNotification>?
            notificationReceived;

        public int StateSubscribeCount
        {
            get;
            private set;
        }

        public int StateUnsubscribeCount
        {
            get;
            private set;
        }

        public int NotificationSubscribeCount
        {
            get;
            private set;
        }

        public int NotificationUnsubscribeCount
        {
            get;
            private set;
        }

        public int TraceSubscribeCount
        {
            get;
            private set;
        }

        public int TraceUnsubscribeCount
        {
            get;
            private set;
        }

        public override event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged
        {
            add
            {
                StateSubscribeCount++;
                stateChanged +=
                    value;
            }

            remove
            {
                StateUnsubscribeCount++;
                stateChanged -=
                    value;
            }
        }

        public override event Action<CompactEventNotification>?
            EventNotificationReceived
        {
            add
            {
                NotificationSubscribeCount++;
                notificationReceived +=
                    value;
            }

            remove
            {
                NotificationUnsubscribeCount++;
                notificationReceived -=
                    value;
            }
        }

        public void SubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
            TraceSubscribeCount++;
        }

        public void UnsubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
            TraceUnsubscribeCount++;
        }

        public void PublishNotification(
            CompactEventNotification notification)
        {
            notificationReceived?.Invoke(
                notification);
        }
    }

    private sealed class TestTraceObserver
        : ITransportExchangeTraceObserver
    {
        public void OnTransportExchangeCompleted(
            TransportExchangeTrace trace)
        {
        }
    }

    private sealed class ThrowingSink
        : IRuntimeDiagnosticSink
    {
        public bool IsEnabled(
            RuntimeDiagnosticLevel level)
        {
            return true;
        }

        public void Publish(
            RuntimeDiagnosticRecord record)
        {
            throw new InvalidOperationException(
                "sink failure");
        }
    }
}

using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Diagnostics;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NativeRuntimeProtocolDiagnosticConnectionTests
{
    private static readonly CorrelationId CorrelationId =
        new(
            42);

    [Fact]
    public async Task SendAsync_SuccessReturnsSameResponseAndPublishesMetadata()
    {
        DiscoverResponse expected =
            new(
                CorrelationId,
                new EndpointId(
                    "sensitive-endpoint-value"),
                []);

        var inner =
            new StubConnection(
                _ =>
                    Task.FromResult<ProtocolMessage>(
                        expected));

        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        IRuntimeProtocolConnection connection =
            CreateDecorator(
                inner,
                collector);

        ProtocolMessage actual =
            await connection.SendAsync(
                new DiscoverRequest(
                    CorrelationId));

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
            "DiscoverRequest",
            records[0].Details["messageKind"]);
        Assert.Equal(
            "DiscoverResponse",
            records[1].Details["messageKind"]);
        Assert.Equal(
            "42",
            records[0].Details["correlationId"]);
        Assert.Equal(
            4,
            records[1].Details.Count);
        Assert.DoesNotContain(
            records[1].Details,
            detail =>
                detail.Value.Contains(
                    "sensitive",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SendAsync_ProtocolFailureReturnsSameResponseAndPublishesFailure()
    {
        WritePropertyResponse expected =
            new(
                CorrelationId,
                ProtocolResult.Rejected,
                PropertyValue: null);

        var inner =
            new StubConnection(
                _ =>
                    Task.FromResult<ProtocolMessage>(
                        expected));

        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        IRuntimeProtocolConnection connection =
            CreateDecorator(
                inner,
                collector);

        ProtocolMessage actual =
            await connection.SendAsync(
                new DiscoverRequest(
                    CorrelationId));

        Assert.Same(
            expected,
            actual);
        Assert.Equal(
            "ProtocolExchangeFailed",
            collector.GetSnapshot()[1].EventName);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Failed,
            collector.GetSnapshot()[1].Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SendAsync_ExceptionIsRethrownUnchangedAndClassified(
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
                    Task.FromException<ProtocolMessage>(
                        expected));

        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        IRuntimeProtocolConnection connection =
            CreateDecorator(
                inner,
                collector);

        Exception actual =
            await Assert.ThrowsAnyAsync<Exception>(
                () =>
                    connection.SendAsync(
                        new DiscoverRequest(
                            CorrelationId)));

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
        DiscoverResponse expected =
            new(
                CorrelationId,
                new EndpointId(
                    "endpoint-one"),
                []);

        var inner =
            new StubConnection(
                _ =>
                    Task.FromResult<ProtocolMessage>(
                        expected));

        BoundedRuntimeDiagnosticCollector collector =
            new(
                4,
                RuntimeDiagnosticLevel.Operational);

        IRuntimeProtocolConnection connection =
            CreateDecorator(
                inner,
                collector);

        ProtocolMessage actual =
            await connection.SendAsync(
                new DiscoverRequest(
                    CorrelationId));

        Assert.Same(
            expected,
            actual);
        Assert.Empty(
            collector.GetSnapshot());
    }

    [Fact]
    public void Create_PreservesOptionalCapabilityShape()
    {
        RuntimeDiagnosticPublisher diagnostics =
            new();

        IRuntimeProtocolConnection plain =
            NativeRuntimeProtocolDiagnosticConnection.Create(
                new StubConnection(),
                "endpoint-one",
                diagnostics);

        IRuntimeProtocolConnection notification =
            NativeRuntimeProtocolDiagnosticConnection.Create(
                new NotificationConnection(),
                "endpoint-one",
                diagnostics);

        IRuntimeProtocolConnection trace =
            NativeRuntimeProtocolDiagnosticConnection.Create(
                new TraceConnection(),
                "endpoint-one",
                diagnostics);

        IRuntimeProtocolConnection both =
            NativeRuntimeProtocolDiagnosticConnection.Create(
                new NotificationAndTraceConnection(),
                "endpoint-one",
                diagnostics);

        Assert.IsNotAssignableFrom<IRuntimeProtocolNotificationSource>(
            plain);
        Assert.IsNotAssignableFrom<ITransportExchangeTraceSource>(
            plain);

        Assert.IsAssignableFrom<IRuntimeProtocolNotificationSource>(
            notification);
        Assert.IsNotAssignableFrom<ITransportExchangeTraceSource>(
            notification);

        Assert.IsNotAssignableFrom<IRuntimeProtocolNotificationSource>(
            trace);
        Assert.IsAssignableFrom<ITransportExchangeTraceSource>(
            trace);

        Assert.IsAssignableFrom<IRuntimeProtocolNotificationSource>(
            both);
        Assert.IsAssignableFrom<ITransportExchangeTraceSource>(
            both);
    }

    [Fact]
    public void Create_ForwardsOptionalCapabilitySubscriptions()
    {
        var inner =
            new NotificationAndTraceConnection();

        IRuntimeProtocolConnection decorated =
            NativeRuntimeProtocolDiagnosticConnection.Create(
                inner,
                "endpoint-one",
                new RuntimeDiagnosticPublisher());

        var notificationObserver =
            new TestNotificationObserver();

        var traceObserver =
            new TestTraceObserver();

        var notificationSource =
            Assert.IsAssignableFrom<IRuntimeProtocolNotificationSource>(
                decorated);

        var traceSource =
            Assert.IsAssignableFrom<ITransportExchangeTraceSource>(
                decorated);

        notificationSource.SubscribeNotification(
            notificationObserver);

        notificationSource.UnsubscribeNotification(
            notificationObserver);

        traceSource.SubscribeTrace(
            traceObserver);

        traceSource.UnsubscribeTrace(
            traceObserver);

        Assert.Equal(
            1,
            inner.NotificationSubscribeCount);
        Assert.Equal(
            1,
            inner.NotificationUnsubscribeCount);
        Assert.Equal(
            1,
            inner.TraceSubscribeCount);
        Assert.Equal(
            1,
            inner.TraceUnsubscribeCount);
    }

    [Fact]
    public void NotificationObserver_PublishesPayloadFreeInboundMetadata()
    {
        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        var observer =
            new NativeProtocolNotificationDiagnosticObserver(
                "endpoint-one",
                new RuntimeDiagnosticPublisher(
                    collector));

        observer.OnProtocolNotification(
            new EventNotification(
                new InstrumentId(
                    "instrument-one"),
                new DescriptorPath(
                    "Button",
                    "Pressed"),
                DateTimeOffset.UtcNow,
                "sensitive payload"));

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
            "none",
            record.Details["correlationId"]);
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
    public async Task ThrowingSink_DoesNotChangeExchangeResult()
    {
        DiscoverResponse expected =
            new(
                CorrelationId,
                new EndpointId(
                    "endpoint-one"),
                []);

        var inner =
            new StubConnection(
                _ =>
                    Task.FromResult<ProtocolMessage>(
                        expected));

        IRuntimeProtocolConnection connection =
            NativeRuntimeProtocolDiagnosticConnection.Create(
                inner,
                "endpoint-one",
                new RuntimeDiagnosticPublisher(
                    new ThrowingSink()));

        ProtocolMessage actual =
            await connection.SendAsync(
                new DiscoverRequest(
                    CorrelationId));

        Assert.Same(
            expected,
            actual);
    }

    private static BoundedRuntimeDiagnosticCollector CreateProtocolCollector()
    {
        return new BoundedRuntimeDiagnosticCollector(
            10,
            RuntimeDiagnosticLevel.Protocol);
    }

    private static IRuntimeProtocolConnection CreateDecorator(
        IRuntimeProtocolConnection inner,
        BoundedRuntimeDiagnosticCollector collector)
    {
        return NativeRuntimeProtocolDiagnosticConnection.Create(
            inner,
            "endpoint-one",
            new RuntimeDiagnosticPublisher(
                collector));
    }

    private class StubConnection :
        IRuntimeProtocolConnection
    {
        private readonly Func<
            ProtocolMessage,
            Task<ProtocolMessage>> sendAsync;

        public StubConnection(
            Func<
                ProtocolMessage,
                Task<ProtocolMessage>>? sendAsync = null)
        {
            this.sendAsync =
                sendAsync
                ?? (_ =>
                    Task.FromResult<ProtocolMessage>(
                        new DiscoverResponse(
                            CorrelationId,
                            new EndpointId(
                                "endpoint-one"),
                            [])));
        }

        public Task<ProtocolMessage> SendAsync(
            ProtocolMessage request,
            CancellationToken cancellationToken = default)
        {
            return sendAsync(
                request);
        }
    }

    private sealed class NotificationConnection :
        StubConnection,
        IRuntimeProtocolNotificationSource
    {
        public void SubscribeNotification(
            IProtocolNotificationObserver observer)
        {
        }

        public void UnsubscribeNotification(
            IProtocolNotificationObserver observer)
        {
        }
    }

    private sealed class TraceConnection :
        StubConnection,
        ITransportExchangeTraceSource
    {
        public void SubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
        }

        public void UnsubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
        }
    }

    private sealed class NotificationAndTraceConnection :
        StubConnection,
        IRuntimeProtocolNotificationSource,
        ITransportExchangeTraceSource
    {
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

        public void SubscribeNotification(
            IProtocolNotificationObserver observer)
        {
            NotificationSubscribeCount++;
        }

        public void UnsubscribeNotification(
            IProtocolNotificationObserver observer)
        {
            NotificationUnsubscribeCount++;
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
    }

    private sealed class TestNotificationObserver :
        IProtocolNotificationObserver
    {
        public void OnProtocolNotification(
            ProtocolMessage notification)
        {
        }
    }

    private sealed class TestTraceObserver :
        ITransportExchangeTraceObserver
    {
        public void OnTransportExchangeCompleted(
            TransportExchangeTrace trace)
        {
        }
    }

    private sealed class ThrowingSink :
        IRuntimeDiagnosticSink
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
                "Observer failure.");
        }
    }
}

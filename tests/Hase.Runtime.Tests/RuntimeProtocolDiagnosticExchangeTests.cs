using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Tests;

public sealed class RuntimeProtocolDiagnosticExchangeTests
{
    [Fact]
    public void Construction_PublishesOutboundRequestMetadata()
    {
        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        _ =
            CreateExchange(
                collector);

        RuntimeDiagnosticRecord record =
            Assert.Single(
                collector.GetSnapshot());

        Assert.Equal(
            RuntimeDiagnosticLevel.Protocol,
            record.Level);
        Assert.Equal(
            RuntimeDiagnosticCategory.ProtocolExchange,
            record.Category);
        Assert.Equal(
            "ProtocolRequestSent",
            record.EventName);
        Assert.Equal(
            RuntimeDiagnosticDirection.Outbound,
            record.Direction);
        Assert.Equal(
            "endpoint-one",
            record.EndpointId);
        Assert.Null(
            record.OperationId);
        Assert.Null(
            record.Duration);
        Assert.Null(
            record.Outcome);
        Assert.Equal(
            "NativeProtocolV1",
            record.Details["protocolFamily"]);
        Assert.Equal(
            "ReadPropertyRequest",
            record.Details["messageKind"]);
        Assert.Equal(
            "42",
            record.Details["correlationId"]);
        Assert.Equal(
            "18",
            record.Details["payloadLength"]);
    }

    [Fact]
    public void Complete_SuccessPublishesInboundResponseWithDuration()
    {
        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        ManualTimeProvider timeProvider =
            new();

        RuntimeProtocolDiagnosticExchange exchange =
            CreateExchange(
                collector,
                timeProvider);

        timeProvider.Advance(
            TimeSpan.FromMilliseconds(
                25));

        exchange.Complete(
            "ReadPropertyResponse",
            24,
            RuntimeDiagnosticDirection.Inbound,
            RuntimeDiagnosticOutcome.Succeeded);

        RuntimeDiagnosticRecord response =
            collector.GetSnapshot()[1];

        Assert.Equal(
            "ProtocolResponseReceived",
            response.EventName);
        Assert.Equal(
            RuntimeDiagnosticDirection.Inbound,
            response.Direction);
        Assert.Equal(
            RuntimeDiagnosticOutcome.Succeeded,
            response.Outcome);
        Assert.Equal(
            TimeSpan.FromMilliseconds(
                25),
            response.Duration);
        Assert.Equal(
            "ReadPropertyResponse",
            response.Details["messageKind"]);
        Assert.Equal(
            "24",
            response.Details["payloadLength"]);
        Assert.Equal(
            "42",
            response.Details["correlationId"]);
    }

    [Theory]
    [InlineData(RuntimeDiagnosticOutcome.Failed)]
    [InlineData(RuntimeDiagnosticOutcome.Cancelled)]
    [InlineData(RuntimeDiagnosticOutcome.TimedOut)]
    public void Complete_NonSuccessPublishesFailedExchange(
        RuntimeDiagnosticOutcome outcome)
    {
        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        RuntimeProtocolDiagnosticExchange exchange =
            CreateExchange(
                collector);

        exchange.Complete(
            "ReadPropertyRequest",
            18,
            RuntimeDiagnosticDirection.Outbound,
            outcome);

        RuntimeDiagnosticRecord failed =
            collector.GetSnapshot()[1];

        Assert.Equal(
            "ProtocolExchangeFailed",
            failed.EventName);
        Assert.Equal(
            RuntimeDiagnosticSeverity.Warning,
            failed.Severity);
        Assert.Equal(
            RuntimeDiagnosticDirection.Outbound,
            failed.Direction);
        Assert.Equal(
            outcome,
            failed.Outcome);
    }

    [Fact]
    public void Complete_RepeatedPublishesOneTerminalRecord()
    {
        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        RuntimeProtocolDiagnosticExchange exchange =
            CreateExchange(
                collector);

        exchange.Complete(
            "ReadPropertyResponse",
            24,
            RuntimeDiagnosticDirection.Inbound,
            RuntimeDiagnosticOutcome.Succeeded);

        exchange.Complete(
            "ReadPropertyRequest",
            18,
            RuntimeDiagnosticDirection.Outbound,
            RuntimeDiagnosticOutcome.Failed);

        Assert.Equal(
            2,
            collector.GetSnapshot().Count);
    }

    [Fact]
    public void PublishNotification_PublishesOneInboundRecord()
    {
        BoundedRuntimeDiagnosticCollector collector =
            CreateProtocolCollector();

        RuntimeProtocolDiagnosticExchange.PublishNotification(
            new RuntimeDiagnosticPublisher(
                collector),
            "endpoint-one",
            "CompactSerialProtocolV1",
            "EventNotification",
            "none",
            12);

        RuntimeDiagnosticRecord notification =
            Assert.Single(
                collector.GetSnapshot());

        Assert.Equal(
            "ProtocolNotificationReceived",
            notification.EventName);
        Assert.Equal(
            RuntimeDiagnosticDirection.Inbound,
            notification.Direction);
        Assert.Equal(
            "CompactSerialProtocolV1",
            notification.Details["protocolFamily"]);
        Assert.Equal(
            "none",
            notification.Details["correlationId"]);
        Assert.Null(
            notification.Duration);
        Assert.Null(
            notification.Outcome);
    }

    [Fact]
    public void OperationalOnlyCollector_DoesNotRetainProtocolRecords()
    {
        BoundedRuntimeDiagnosticCollector collector =
            new(
                4,
                RuntimeDiagnosticLevel.Operational);

        RuntimeProtocolDiagnosticExchange exchange =
            CreateExchange(
                collector);

        exchange.Complete(
            "ReadPropertyResponse",
            24,
            RuntimeDiagnosticDirection.Inbound,
            RuntimeDiagnosticOutcome.Succeeded);

        RuntimeProtocolDiagnosticExchange.PublishNotification(
            new RuntimeDiagnosticPublisher(
                collector),
            "endpoint-one",
            "NativeProtocolV1",
            "EventNotification",
            "none",
            12);

        Assert.Empty(
            collector.GetSnapshot());
    }

    [Fact]
    public void ThrowingSink_DoesNotAffectExchangeReporting()
    {
        RuntimeDiagnosticPublisher diagnostics =
            new(
                new ThrowingSink());

        Exception? exception =
            Record.Exception(
                () =>
                {
                    var exchange =
                        new RuntimeProtocolDiagnosticExchange(
                            diagnostics,
                            "endpoint-one",
                            "NativeProtocolV1",
                            "ReadPropertyRequest",
                            "42",
                            18);

                    exchange.Complete(
                        "ReadPropertyResponse",
                        24,
                        RuntimeDiagnosticDirection.Inbound,
                        RuntimeDiagnosticOutcome.Succeeded);

                    RuntimeProtocolDiagnosticExchange.PublishNotification(
                        diagnostics,
                        "endpoint-one",
                        "NativeProtocolV1",
                        "EventNotification",
                        "none",
                        12);
                });

        Assert.Null(
            exception);
    }

    private static BoundedRuntimeDiagnosticCollector CreateProtocolCollector()
    {
        return new BoundedRuntimeDiagnosticCollector(
            10,
            RuntimeDiagnosticLevel.Protocol);
    }

    private static RuntimeProtocolDiagnosticExchange CreateExchange(
        BoundedRuntimeDiagnosticCollector collector,
        ManualTimeProvider? timeProvider = null)
    {
        return new RuntimeProtocolDiagnosticExchange(
            new RuntimeDiagnosticPublisher(
                collector),
            "endpoint-one",
            "NativeProtocolV1",
            "ReadPropertyRequest",
            "42",
            18,
            timeProvider ??
            new ManualTimeProvider());
    }

    private sealed class ManualTimeProvider :
        TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency =>
            TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return timestamp;
        }

        public void Advance(
            TimeSpan duration)
        {
            timestamp +=
                duration.Ticks;
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

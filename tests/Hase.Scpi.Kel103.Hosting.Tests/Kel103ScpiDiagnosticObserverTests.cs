using Hase.Runtime.Diagnostics;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103ScpiDiagnosticObserverTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 7, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OperationalLevel_PublishesNoScpiRecords()
    {
        BoundedRuntimeDiagnosticCollector collector = CreateCollector(
            RuntimeDiagnosticLevel.Operational);
        Kel103ScpiDiagnosticObserver observer = CreateObserver(collector);

        ObserveSuccessfulQuery(observer, Guid.NewGuid());

        Assert.Empty(collector.GetSnapshot());
    }

    [Fact]
    public void ProtocolLevel_PublishesSanitizedCorrelatedQueryRecords()
    {
        BoundedRuntimeDiagnosticCollector collector = CreateCollector(
            RuntimeDiagnosticLevel.Protocol);
        Kel103ScpiDiagnosticObserver observer = CreateObserver(collector);
        Guid exchangeId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        observer.Observe(new ScpiDiagnosticExchangeStarted(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query,
            ScpiDiagnosticDirection.Transmit,
            "*IDN?\r"u8));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query,
            ScpiDiagnosticDirection.Receive,
            "SENSITIVE"u8));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query,
            ScpiDiagnosticDirection.Receive,
            "\n"u8));
        observer.Observe(new ScpiDiagnosticExchangeCompleted(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query,
            TimeSpan.FromMilliseconds(12)));

        IReadOnlyList<RuntimeDiagnosticRecord> records = collector.GetSnapshot();
        Assert.Equal(2, records.Count);

        RuntimeDiagnosticRecord request = records[0];
        Assert.Equal("ProtocolRequestSent", request.EventName);
        Assert.Equal(RuntimeDiagnosticDirection.Outbound, request.Direction);
        Assert.Equal("6", request.Details["payloadLength"]);
        Assert.Null(request.ByteSnapshot);

        RuntimeDiagnosticRecord response = records[1];
        Assert.Equal("ProtocolResponseReceived", response.EventName);
        Assert.Equal(RuntimeDiagnosticDirection.Inbound, response.Direction);
        Assert.Equal(RuntimeDiagnosticOutcome.Succeeded, response.Outcome);
        Assert.Equal(TimeSpan.FromMilliseconds(12), response.Duration);
        Assert.Equal("10", response.Details["payloadLength"]);
        Assert.Equal("Succeeded", response.Details["scpiOutcome"]);
        Assert.Null(response.ByteSnapshot);

        Assert.All(records, record =>
        {
            Assert.Equal(RuntimeDiagnosticLevel.Protocol, record.Level);
            Assert.Equal(RuntimeDiagnosticCategory.ProtocolExchange, record.Category);
            Assert.Equal("kel-one", record.EndpointId);
            Assert.Equal("ScpiText", record.Details["protocolFamily"]);
            Assert.Equal("ScpiQuery", record.Details["messageKind"]);
            Assert.Equal(
                "11111111222233334444555555555555",
                record.Details["correlationId"]);
            Assert.DoesNotContain(
                "SENSITIVE",
                string.Join('|', record.Details.Values),
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public void BytesLevel_PublishesExactDirectionalSnapshotsInObservationOrder()
    {
        BoundedRuntimeDiagnosticCollector collector = CreateCollector(
            RuntimeDiagnosticLevel.Bytes);
        Kel103ScpiDiagnosticObserver observer = CreateObserver(collector);
        Guid exchangeId = Guid.NewGuid();

        ObserveSuccessfulQuery(observer, exchangeId);

        IReadOnlyList<RuntimeDiagnosticRecord> records = collector.GetSnapshot();
        Assert.Equal(
            new[]
            {
                "ProtocolRequestSent",
                "TransportBytesSent",
                "TransportBytesReceived",
                "ProtocolResponseReceived"
            },
            records.Select(record => record.EventName));

        RuntimeDiagnosticRecord sent = records[1];
        Assert.Equal(RuntimeDiagnosticDirection.Outbound, sent.Direction);
        Assert.Equal("*IDN?\r"u8.ToArray(), sent.ByteSnapshot!.ToArray());

        RuntimeDiagnosticRecord received = records[2];
        Assert.Equal(RuntimeDiagnosticDirection.Inbound, received.Direction);
        Assert.Equal("OK\n"u8.ToArray(), received.ByteSnapshot!.ToArray());
        Assert.Equal("ScpiText", received.Details["protocolFamily"]);
        Assert.Equal(
            records[0].Details["correlationId"],
            received.Details["correlationId"]);
    }

    [Fact]
    public void BytesLevel_UsesEstablishedBoundAndTruncationMetadata()
    {
        BoundedRuntimeDiagnosticCollector collector = CreateCollector(
            RuntimeDiagnosticLevel.Bytes);
        Kel103ScpiDiagnosticObserver observer = CreateObserver(collector);
        Guid exchangeId = Guid.NewGuid();
        byte[] bytes = new byte[
            RuntimeDiagnosticByteSnapshot.MaximumCapturedByteCount + 11];

        observer.Observe(new ScpiDiagnosticExchangeStarted(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Command));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Command,
            ScpiDiagnosticDirection.Transmit,
            bytes));

        RuntimeDiagnosticRecord byteRecord = collector.GetSnapshot()[1];
        RuntimeDiagnosticByteSnapshot snapshot = Assert.IsType<RuntimeDiagnosticByteSnapshot>(
            byteRecord.ByteSnapshot);
        Assert.Equal(bytes.Length, snapshot.OriginalByteCount);
        Assert.Equal(
            RuntimeDiagnosticByteSnapshot.MaximumCapturedByteCount,
            snapshot.CapturedByteCount);
        Assert.True(snapshot.IsTruncated);
        Assert.Equal("267", byteRecord.Details["originalByteCount"]);
        Assert.Equal("256", byteRecord.Details["capturedByteCount"]);
        Assert.Equal("True", byteRecord.Details["isTruncated"]);
    }

    [Fact]
    public void SuccessfulCommand_PublishesOutboundTerminalWithoutResponsePayload()
    {
        BoundedRuntimeDiagnosticCollector collector = CreateCollector(
            RuntimeDiagnosticLevel.Protocol);
        Kel103ScpiDiagnosticObserver observer = CreateObserver(collector);
        Guid exchangeId = Guid.NewGuid();

        observer.Observe(new ScpiDiagnosticExchangeStarted(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Command));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Command,
            ScpiDiagnosticDirection.Transmit,
            "INPUT ON\r"u8));
        observer.Observe(new ScpiDiagnosticExchangeCompleted(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Command,
            TimeSpan.FromMilliseconds(4)));

        RuntimeDiagnosticRecord terminal = collector.GetSnapshot()[1];
        Assert.Equal(RuntimeDiagnosticDirection.Outbound, terminal.Direction);
        Assert.Equal(RuntimeDiagnosticOutcome.Succeeded, terminal.Outcome);
        Assert.Equal("ScpiCommand", terminal.Details["messageKind"]);
        Assert.Equal("0", terminal.Details["payloadLength"]);
    }

    [Theory]
    [InlineData(ScpiDiagnosticOutcome.Failed, RuntimeDiagnosticOutcome.Failed)]
    [InlineData(ScpiDiagnosticOutcome.Canceled, RuntimeDiagnosticOutcome.Cancelled)]
    [InlineData(ScpiDiagnosticOutcome.TimedOut, RuntimeDiagnosticOutcome.TimedOut)]
    [InlineData(ScpiDiagnosticOutcome.Disposed, RuntimeDiagnosticOutcome.Failed)]
    public void Failure_MapsAvailableRuntimeOutcomeWithoutExceptionText(
        ScpiDiagnosticOutcome scpiOutcome,
        RuntimeDiagnosticOutcome runtimeOutcome)
    {
        BoundedRuntimeDiagnosticCollector collector = CreateCollector(
            RuntimeDiagnosticLevel.Protocol);
        Kel103ScpiDiagnosticObserver observer = CreateObserver(collector);
        Guid exchangeId = Guid.NewGuid();

        observer.Observe(new ScpiDiagnosticExchangeStarted(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query));
        observer.Observe(new ScpiDiagnosticExchangeFailed(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query,
            TimeSpan.FromMilliseconds(20),
            scpiOutcome,
            ScpiDiagnosticFailureKind.Timeout,
            executionMayHaveOccurred: false));

        RuntimeDiagnosticRecord failed = Assert.Single(collector.GetSnapshot());
        Assert.Equal("ProtocolExchangeFailed", failed.EventName);
        Assert.Equal(RuntimeDiagnosticSeverity.Warning, failed.Severity);
        Assert.Equal(runtimeOutcome, failed.Outcome);
        Assert.Equal(scpiOutcome.ToString(), failed.Details["scpiOutcome"]);
        Assert.Equal("Timeout", failed.Details["failureKind"]);
        Assert.Equal("False", failed.Details["executionMayHaveOccurred"]);
        Assert.DoesNotContain(
            failed.Details.Keys,
            key => key.Contains("exception", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UncertainCommand_PreservesExplicitExecutionPossibility()
    {
        BoundedRuntimeDiagnosticCollector collector = CreateCollector(
            RuntimeDiagnosticLevel.Protocol);
        Kel103ScpiDiagnosticObserver observer = CreateObserver(collector);
        Guid exchangeId = Guid.NewGuid();

        observer.Observe(new ScpiDiagnosticExchangeStarted(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Command));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Command,
            ScpiDiagnosticDirection.Transmit,
            "INPUT ON\r"u8));
        observer.Observe(new ScpiDiagnosticExchangeFailed(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Command,
            TimeSpan.FromMilliseconds(9),
            ScpiDiagnosticOutcome.Uncertain,
            ScpiDiagnosticFailureKind.InputOutput,
            executionMayHaveOccurred: true));

        RuntimeDiagnosticRecord failed = collector.GetSnapshot()[1];
        Assert.Equal(RuntimeDiagnosticOutcome.Failed, failed.Outcome);
        Assert.Equal("Uncertain", failed.Details["scpiOutcome"]);
        Assert.Equal("InputOutput", failed.Details["failureKind"]);
        Assert.Equal("True", failed.Details["executionMayHaveOccurred"]);
    }

    [Fact]
    public void TerminalObservation_RemovesCorrelationAndIgnoresDuplicateTerminal()
    {
        BoundedRuntimeDiagnosticCollector collector = CreateCollector(
            RuntimeDiagnosticLevel.Protocol);
        Kel103ScpiDiagnosticObserver observer = CreateObserver(collector);
        Guid exchangeId = Guid.NewGuid();

        ObserveSuccessfulQuery(observer, exchangeId);
        observer.Observe(new ScpiDiagnosticExchangeFailed(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query,
            TimeSpan.FromSeconds(1),
            ScpiDiagnosticOutcome.Failed,
            ScpiDiagnosticFailureKind.Unknown,
            executionMayHaveOccurred: false));

        Assert.Equal(2, collector.GetSnapshot().Count);
    }

    [Fact]
    public void InterleavedExchanges_RetainIndependentCorrelationAndLengths()
    {
        BoundedRuntimeDiagnosticCollector collector = CreateCollector(
            RuntimeDiagnosticLevel.Protocol);
        Kel103ScpiDiagnosticObserver observer = CreateObserver(collector);
        Guid first = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid second = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        observer.Observe(new ScpiDiagnosticExchangeStarted(
            first, Timestamp, ScpiDiagnosticExchangeKind.Query));
        observer.Observe(new ScpiDiagnosticExchangeStarted(
            second, Timestamp, ScpiDiagnosticExchangeKind.Command));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            second, Timestamp, ScpiDiagnosticExchangeKind.Command,
            ScpiDiagnosticDirection.Transmit, new byte[3]));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            first, Timestamp, ScpiDiagnosticExchangeKind.Query,
            ScpiDiagnosticDirection.Transmit, new byte[5]));
        observer.Observe(new ScpiDiagnosticExchangeCompleted(
            second, Timestamp, ScpiDiagnosticExchangeKind.Command, TimeSpan.Zero));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            first, Timestamp, ScpiDiagnosticExchangeKind.Query,
            ScpiDiagnosticDirection.Receive, new byte[7]));
        observer.Observe(new ScpiDiagnosticExchangeCompleted(
            first, Timestamp, ScpiDiagnosticExchangeKind.Query, TimeSpan.Zero));

        IReadOnlyList<RuntimeDiagnosticRecord> terminals = collector.GetSnapshot()
            .Where(record => record.Outcome is not null)
            .ToArray();
        Assert.Equal(2, terminals.Count);
        Assert.Equal("0", terminals[0].Details["payloadLength"]);
        Assert.Equal("7", terminals[1].Details["payloadLength"]);
        Assert.NotEqual(
            terminals[0].Details["correlationId"],
            terminals[1].Details["correlationId"]);
    }

    private static BoundedRuntimeDiagnosticCollector CreateCollector(
        RuntimeDiagnosticLevel maximumLevel) =>
        new(32, maximumLevel);

    private static Kel103ScpiDiagnosticObserver CreateObserver(
        BoundedRuntimeDiagnosticCollector collector) =>
        new("kel-one", new RuntimeDiagnosticPublisher(collector));

    private static void ObserveSuccessfulQuery(
        Kel103ScpiDiagnosticObserver observer,
        Guid exchangeId)
    {
        observer.Observe(new ScpiDiagnosticExchangeStarted(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query,
            ScpiDiagnosticDirection.Transmit,
            "*IDN?\r"u8));
        observer.Observe(new ScpiDiagnosticBytesObserved(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query,
            ScpiDiagnosticDirection.Receive,
            "OK\n"u8));
        observer.Observe(new ScpiDiagnosticExchangeCompleted(
            exchangeId,
            Timestamp,
            ScpiDiagnosticExchangeKind.Query,
            TimeSpan.FromMilliseconds(3)));
    }
}

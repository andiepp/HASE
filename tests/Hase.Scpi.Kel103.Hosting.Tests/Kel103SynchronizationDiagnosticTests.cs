using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103SynchronizationDiagnosticTests
{
    [Fact]
    public async Task SuccessfulSynchronization_PublishesCorrelatedSanitizedDuration()
    {
        var collector = new BoundedRuntimeDiagnosticCollector(
            10,
            RuntimeDiagnosticLevel.Operational);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var timeProvider = new ManualTimeProvider();
        var stream = new TimedSerialByteStream(
            timeProvider,
            "RND 320-KEL103 V3.30 SN:REDACTED\n",
            "9.0000V\n",
            "0.1000A\n",
            "0.9000W\n");
        var factory = new Kel103OperationalConnectionFactory(
            context,
            new TimedFactory(stream, timeProvider),
            timeProvider);

        await using Kel103OperationalConnection connection = await factory.OpenAsync(
            new EndpointId("kel-diagnostic-test"),
            SupportedOptions());

        IReadOnlyList<RuntimeDiagnosticRecord> records = collector.GetSnapshot();
        Assert.Equal(2, records.Count);
        Assert.Equal("InstrumentSynchronizationStarted", records[0].EventName);
        Assert.Equal("InstrumentSynchronizationCompleted", records[1].EventName);
        Assert.Equal(records[0].OperationId, records[1].OperationId);
        Assert.Equal(RuntimeDiagnosticOutcome.Succeeded, records[1].Outcome);
        Assert.Equal(TimeSpan.FromMilliseconds(17), records[1].Duration);
        Assert.All(
            records,
            record =>
            {
                Assert.Equal(RuntimeDiagnosticLevel.Operational, record.Level);
                Assert.Equal(RuntimeDiagnosticCategory.RuntimeSynchronization, record.Category);
                Assert.Equal("kel-diagnostic-test", record.EndpointId);
                Assert.Null(record.AttachmentGeneration);
                Assert.Null(record.ByteSnapshot);
                Assert.Equal("kel103-identity", record.Details["DefinitionId"]);
                Assert.Equal("2", record.Details["DefinitionVersion"]);
                Assert.Equal("5", record.Details["PropertyCount"]);
                Assert.Equal(3, record.Details.Count);
            });
    }

    [Fact]
    public async Task InvalidIdentity_PublishesFailureWithoutResponseOrExceptionDetail()
    {
        const string sensitive = "SENSITIVE_RESPONSE";
        var collector = new BoundedRuntimeDiagnosticCollector(10);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var timeProvider = new ManualTimeProvider();
        var stream = new TimedSerialByteStream(timeProvider, sensitive + "\n");
        var factory = new Kel103OperationalConnectionFactory(
            context,
            new TimedFactory(stream, timeProvider),
            timeProvider);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("kel-diagnostic-test"),
            SupportedOptions()));

        RuntimeDiagnosticRecord failed = collector.GetSnapshot().Last();
        Assert.Equal("InstrumentSynchronizationFailed", failed.EventName);
        Assert.Equal(RuntimeDiagnosticOutcome.Failed, failed.Outcome);
        Assert.DoesNotContain(
            collector.GetSnapshot(),
            record => record.Details.Values.Any(
                value => value.Contains(sensitive, StringComparison.Ordinal)));
        Assert.Equal(1, stream.DisposeCount);
    }

    [Theory]
    [InlineData(true, RuntimeDiagnosticOutcome.Cancelled)]
    [InlineData(false, RuntimeDiagnosticOutcome.TimedOut)]
    public async Task BoundedTermination_PublishesClassifiedOutcomeWithoutPublication(
        bool cancellationRequested,
        RuntimeDiagnosticOutcome expectedOutcome)
    {
        var collector = new BoundedRuntimeDiagnosticCollector(10);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var timeProvider = new ManualTimeProvider();
        var transport = new FailingFactory(new TimeoutException("SENSITIVE_EXCEPTION"));
        var factory = new Kel103OperationalConnectionFactory(context, transport, timeProvider);
        using var cancellation = new CancellationTokenSource();
        if (cancellationRequested)
        {
            cancellation.Cancel();
        }

        if (cancellationRequested)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.OpenAsync(
                new EndpointId("kel-diagnostic-test"),
                SupportedOptions(),
                cancellation.Token));
        }
        else
        {
            await Assert.ThrowsAsync<TimeoutException>(() => factory.OpenAsync(
                new EndpointId("kel-diagnostic-test"),
                SupportedOptions()));
        }

        RuntimeDiagnosticRecord terminal = collector.GetSnapshot().Last();
        Assert.Equal("InstrumentSynchronizationFailed", terminal.EventName);
        Assert.Equal(expectedOutcome, terminal.Outcome);
        Assert.Empty(context.Endpoints);
        Assert.DoesNotContain(
            collector.GetSnapshot(),
            record => record.Details.Values.Any(
                value => value.Contains("SENSITIVE", StringComparison.Ordinal)));
        Assert.Equal(cancellationRequested ? 0 : 1, transport.OpenCount);
    }

    private static SerialTransportOptions SupportedOptions() =>
        new("TEST-PORT", 115200);

    private sealed class TimedFactory(
        ISerialByteStream stream,
        ManualTimeProvider timeProvider) : ISerialByteStreamFactory
    {
        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(5));
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class FailingFactory(Exception exception) : ISerialByteStreamFactory
    {
        public int OpenCount { get; private set; }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return ValueTask.FromException<ISerialByteStream>(exception);
        }
    }

    private sealed class TimedSerialByteStream(
        ManualTimeProvider timeProvider,
        params string[] responses) : ISerialByteStream
    {
        private readonly Queue<byte[]> remaining = new(
            responses.Select(Encoding.ASCII.GetBytes));

        public int DisposeCount { get; private set; }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(2));
            byte[] response = remaining.Dequeue();
            response.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(response.Length);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow =
            new(2026, 8, 3, 22, 0, 0, TimeSpan.Zero);
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan duration)
        {
            utcNow = utcNow.Add(duration);
            timestamp += duration.Ticks;
        }
    }
}

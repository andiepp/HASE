using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103ScpiProductionDiagnosticCompositionTests
{
    [Fact]
    public async Task ProtocolCapture_ProductionSynchronizationPublishesSanitizedExchanges()
    {
        var collector = new BoundedRuntimeDiagnosticCollector(
            32,
            RuntimeDiagnosticLevel.Protocol);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var timeProvider = new ManualTimeProvider();
        var stream = SuccessfulStream(timeProvider);
        var factory = new Kel103OperationalConnectionFactory(
            context,
            new SingleStreamFactory(stream),
            timeProvider);

        await using Kel103OperationalConnection connection = await factory.OpenAsync(
            new EndpointId("kel-production-diagnostic"),
            SupportedOptions());

        RuntimeDiagnosticRecord[] protocol = collector.GetSnapshot(
                RuntimeDiagnosticLevel.Protocol,
                RuntimeDiagnosticCategory.ProtocolExchange)
            .ToArray();

        Assert.Equal(8, protocol.Length);
        Assert.Equal(
            new[]
            {
                "ProtocolRequestSent", "ProtocolResponseReceived",
                "ProtocolRequestSent", "ProtocolResponseReceived",
                "ProtocolRequestSent", "ProtocolResponseReceived",
                "ProtocolRequestSent", "ProtocolResponseReceived"
            },
            protocol.Select(record => record.EventName));
        Assert.All(protocol, record =>
        {
            Assert.Equal("kel-production-diagnostic", record.EndpointId);
            Assert.Equal("ScpiText", record.Details["protocolFamily"]);
            Assert.Equal("ScpiQuery", record.Details["messageKind"]);
            Assert.Null(record.ByteSnapshot);
            Assert.DoesNotContain(
                record.Details.Values,
                value => value.Contains("TEST-PORT", StringComparison.Ordinal));
            Assert.DoesNotContain(
                record.Details.Values,
                value => value.Contains("SENSITIVESERIAL", StringComparison.Ordinal));
            Assert.DoesNotContain(
                record.Details.Values,
                value => value.Contains("9.8864", StringComparison.Ordinal));
        });
        Assert.All(
            protocol.Where(record => record.EventName == "ProtocolResponseReceived"),
            record =>
            {
                Assert.Equal(RuntimeDiagnosticOutcome.Succeeded, record.Outcome);
                Assert.Equal(TimeSpan.FromMilliseconds(3), record.Duration);
            });

        for (int index = 0; index < protocol.Length; index += 2)
        {
            Assert.Equal(
                protocol[index].Details["correlationId"],
                protocol[index + 1].Details["correlationId"]);
        }
    }

    [Fact]
    public async Task BytesCapture_ProductionSynchronizationPublishesExactFramingAndCorrelation()
    {
        var collector = new BoundedRuntimeDiagnosticCollector(
            32,
            RuntimeDiagnosticLevel.Bytes);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var timeProvider = new ManualTimeProvider();
        var stream = SuccessfulStream(timeProvider);
        var factory = new Kel103OperationalConnectionFactory(
            context,
            new SingleStreamFactory(stream),
            timeProvider);

        await using Kel103OperationalConnection connection = await factory.OpenAsync(
            new EndpointId("kel-production-diagnostic"),
            SupportedOptions());

        IReadOnlyList<RuntimeDiagnosticRecord> all = collector.GetSnapshot();
        RuntimeDiagnosticRecord[] bytes = all
            .Where(record => record.Level == RuntimeDiagnosticLevel.Bytes)
            .ToArray();
        RuntimeDiagnosticRecord[] protocol = all
            .Where(record => record.Level == RuntimeDiagnosticLevel.Protocol)
            .ToArray();

        Assert.Equal(8, bytes.Length);
        Assert.Equal(8, protocol.Length);
        Assert.Equal("*IDN?\r"u8.ToArray(), bytes[0].ByteSnapshot!.ToArray());
        Assert.Equal(
            "RND 320-KEL103 V3.30 SN:SENSITIVESERIAL\n"u8.ToArray(),
            bytes[1].ByteSnapshot!.ToArray());
        Assert.Equal(0x0D, bytes[0].ByteSnapshot!.ToArray()[^1]);
        Assert.Equal(0x0A, bytes[1].ByteSnapshot!.ToArray()[^1]);
        Assert.Equal(RuntimeDiagnosticDirection.Outbound, bytes[0].Direction);
        Assert.Equal(RuntimeDiagnosticDirection.Inbound, bytes[1].Direction);
        Assert.All(bytes, record =>
        {
            Assert.Equal("ScpiText", record.Details["protocolFamily"]);
            Assert.False(record.ByteSnapshot!.IsTruncated);
        });
        Assert.Equal(
            protocol[0].Details["correlationId"],
            bytes[0].Details["correlationId"]);
        Assert.Equal(
            protocol[0].Details["correlationId"],
            bytes[1].Details["correlationId"]);
        Assert.Equal(
            protocol[1].Details["correlationId"],
            bytes[1].Details["correlationId"]);
    }

    private static ScriptedSerialByteStream SuccessfulStream(
        ManualTimeProvider timeProvider) =>
        new(
            timeProvider,
            "RND 320-KEL103 V3.30 SN:SENSITIVESERIAL\n",
            "9.8864V\n",
            "0.1000A\n",
            "0.9893W\n");

    private static SerialTransportOptions SupportedOptions() =>
        new(
            "TEST-PORT",
            115200,
            8,
            SerialParity.None,
            SerialStopBits.One,
            SerialHandshake.None);

    private sealed class SingleStreamFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class ScriptedSerialByteStream(
        ManualTimeProvider timeProvider,
        params string[] responses) : ISerialByteStream
    {
        private readonly Queue<byte[]> pending = new(
            responses.Select(Encoding.ASCII.GetBytes));

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(2));
            byte[] response = pending.Dequeue();
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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow =
            new(2026, 8, 7, 8, 30, 0, TimeSpan.Zero);
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan duration)
        {
            utcNow += duration;
            timestamp += duration.Ticks;
        }
    }
}

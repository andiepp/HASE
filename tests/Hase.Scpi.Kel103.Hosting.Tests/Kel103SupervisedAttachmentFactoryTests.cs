using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103SupervisedAttachmentFactoryTests
{
    [Fact]
    public async Task OpenAsync_ReadyAttachmentPerformsNoBackgroundScpiTraffic()
    {
        var collector = new BoundedRuntimeDiagnosticCollector(20);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var stream = SuccessfulStream("9.0000V\n");
        var transport = new SequenceFactory(stream);
        var factory = new Kel103SupervisedAttachmentFactory(context, transport);

        Kel103SupervisedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        await Task.Yield();

        Assert.Single(context.Endpoints);
        Assert.Equal(EndpointConnectionState.Ready, attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(1, transport.OpenCount);
        Assert.Equal(4, stream.WriteCount);
        Assert.Equal(
            RuntimeEndpointConnectionStatistics.Empty,
            attachment.GetConnectionStatistics());
        Assert.DoesNotContain(
            collector.GetSnapshot(),
            record => record.EventName == "RecoveryScheduled");

        await attachment.DisposeAsync();
        Assert.Empty(context.Endpoints);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task FaultedAttachment_IsRecoveredWithoutReplacingPublishedEndpoint()
    {
        var collector = new BoundedRuntimeDiagnosticCollector(30);
        var context = new RuntimeContext(new RuntimeDiagnosticPublisher(collector));
        var initial = SuccessfulStream("9.0000V\n");
        var replacement = SuccessfulStream("10.0000V\n");
        var transport = new SequenceFactory(initial, replacement);
        var factory = new Kel103SupervisedAttachmentFactory(context, transport);
        await using Kel103SupervisedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        RuntimeEndpoint endpoint = attachment.RuntimeEndpoint;
        object propertyPort = attachment.PropertyOperations;

        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await WaitUntilAsync(() =>
            transport.OpenCount == 2
            && endpoint.ConnectionStatus.State == EndpointConnectionState.Ready);

        Assert.Same(endpoint, attachment.RuntimeEndpoint);
        Assert.Same(propertyPort, attachment.PropertyOperations);
        Assert.Single(context.Endpoints);
        Assert.Same(endpoint, context.Endpoints.Single());
        Assert.Equal(1, initial.DisposeCount);
        Assert.Equal(0, replacement.DisposeCount);
        Assert.Equal(
            10.0000m,
            Assert.IsType<decimal>(endpoint.Instruments.Single().Properties.Single(
                property => property.Descriptor.Id == new PropertyId("measured-voltage"))
                .CurrentValue!.Value));
        RuntimeDiagnosticRecord scheduled = Assert.Single(
            collector.GetSnapshot(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeRecovery)
            .Where(record => record.EventName == "RecoveryScheduled"));
        Assert.Equal("kel-test-01", scheduled.EndpointId);
        Assert.Null(scheduled.AttachmentGeneration);
        Assert.Equal("1", scheduled.Details["AttemptNumber"]);
        Assert.Equal("0", scheduled.Details["RetryIndex"]);
        Assert.Equal("0", scheduled.Details["DelayMilliseconds"]);
        RuntimeDiagnosticRecord[] synchronizationRecords = collector.GetSnapshot(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeSynchronization)
            .Where(record => record.EventName.StartsWith(
                "InstrumentSynchronization",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(4, synchronizationRecords.Length);
        Assert.Equal(2, synchronizationRecords.Select(record => record.OperationId).Distinct().Count());
    }

    [Fact]
    public async Task OpenAsync_InitialFailureNeverPublishesAndClosesCandidate()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream("invalid\n");
        var transport = new SequenceFactory(stream);
        var factory = new Kel103SupervisedAttachmentFactory(context, transport);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions()));

        Assert.Empty(context.Endpoints);
        Assert.Equal(1, transport.OpenCount);
        Assert.Equal(1, stream.DisposeCount);
    }

    private static SerialTransportOptions SupportedOptions() =>
        new("TEST-PORT", 115200);

    private static ScriptedSerialByteStream SuccessfulStream(string voltage) => new(
        "RND 320-KEL103 V3.30 SN:REDACTED\n",
        voltage,
        "0.1000A\n",
        "0.9000W\n");

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private sealed class SequenceFactory(params ISerialByteStream[] streams)
        : ISerialByteStreamFactory
    {
        private int next;

        public int OpenCount => next;

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (next >= streams.Length)
            {
                throw new InvalidOperationException("No scripted stream remains.");
            }

            return ValueTask.FromResult(streams[next++]);
        }
    }

    private sealed class ScriptedSerialByteStream(params string[] responses)
        : ISerialByteStream
    {
        private readonly Queue<byte[]> remaining = new(
            responses.Select(Encoding.ASCII.GetBytes));

        public int WriteCount { get; private set; }
        public int DisposeCount { get; private set; }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!remaining.TryDequeue(out byte[]? response))
            {
                return ValueTask.FromResult(0);
            }

            response.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(response.Length);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}

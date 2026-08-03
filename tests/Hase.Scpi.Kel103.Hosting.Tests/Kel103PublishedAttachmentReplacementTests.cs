using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103PublishedAttachmentReplacementTests
{
    [Fact]
    public async Task ReplaceAsync_PreservesPublishedEndpointAndPortWhileReplacingSession()
    {
        var context = new RuntimeContext();
        var initial = Stream("9.0000V\n", "0.1000A\n", "0.9000W\n");
        var replacement = Stream("10.0000V\n", "0.2000A\n", "2.0000W\n");
        var transport = new SequenceFactory(initial, replacement);
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            transport,
            new FixedTimeProvider());
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        RuntimeEndpoint endpoint = attachment.RuntimeEndpoint;
        IEndpointAttachmentPropertyOperations propertyOperations = attachment.PropertyOperations;
        var states = new RecordingStatusObserver();
        endpoint.SubscribeConnectionStatus(states);
        endpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));

        await attachment.ReplaceAsync(SupportedOptions());

        Assert.Same(endpoint, attachment.RuntimeEndpoint);
        Assert.Same(propertyOperations, attachment.PropertyOperations);
        Assert.Single(context.Endpoints);
        Assert.Same(endpoint, context.Endpoints.Single());
        Assert.Equal(EndpointConnectionState.Ready, endpoint.ConnectionStatus.State);
        Assert.Equal(
            [EndpointConnectionState.Faulted, EndpointConnectionState.Reconnecting, EndpointConnectionState.Ready],
            states.States);
        Assert.Equal([0, 1], transport.PreviousDisposeCountsAtOpen);
        Assert.Equal(1, initial.DisposeCount);
        Assert.Equal(0, replacement.DisposeCount);
        Assert.Equal(
            ["KEL-103", "V3.30", 10.0000m, 0.2000m, 2.0000m],
            endpoint.Instruments.Single().Properties
                .Select(property => property.CurrentValue!.Value)
                .ToArray());
    }

    [Fact]
    public async Task ReplaceAsync_FailurePreservesCacheAndPublicationWithoutActiveConnection()
    {
        var context = new RuntimeContext();
        var initial = Stream("9.0000V\n", "0.1000A\n", "0.9000W\n");
        var replacement = new ScriptedSerialByteStream(IdentityResponse(), "invalid\n");
        var transport = new SequenceFactory(initial, replacement);
        var factory = new Kel103PublishedAttachmentFactory(context, transport);
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        object?[] cachedValues = attachment.RuntimeEndpoint.Instruments.Single().Properties
            .Select(property => property.CurrentValue!.Value)
            .ToArray();
        attachment.RuntimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            attachment.ReplaceAsync(SupportedOptions()));

        Assert.Single(context.Endpoints);
        Assert.Equal(EndpointConnectionState.Faulted, attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(
            "The KEL-103 connection replacement failed.",
            attachment.RuntimeEndpoint.ConnectionStatus.Detail);
        Assert.Equal(
            cachedValues,
            attachment.RuntimeEndpoint.Instruments.Single().Properties
                .Select(property => property.CurrentValue!.Value)
                .ToArray());
        Assert.Equal(1, initial.DisposeCount);
        Assert.Equal(1, replacement.DisposeCount);

        EndpointAttachmentPropertyOperationResult read = await attachment.PropertyOperations.ReadAsync(
            new InstrumentId("electronic-load-01"),
            new PropertyId("measured-voltage"));
        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Unavailable, read.Status);
    }

    [Fact]
    public async Task ReplaceAsync_ConcurrentAttemptsAreSerialized()
    {
        var context = new RuntimeContext();
        var initial = Stream("9.0000V\n", "0.1000A\n", "0.9000W\n");
        var replacement = Stream("10.0000V\n", "0.2000A\n", "2.0000W\n");
        var transport = new SequenceFactory(initial, replacement);
        var factory = new Kel103PublishedAttachmentFactory(context, transport);
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        attachment.RuntimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));

        Task first = attachment.ReplaceAsync(SupportedOptions());
        Task second = attachment.ReplaceAsync(SupportedOptions());
        await first;
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);

        Assert.Equal(2, transport.OpenCount);
        Assert.Equal(EndpointConnectionState.Ready, attachment.RuntimeEndpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task ReplaceAsync_AfterFailedAttemptCanTryAgain()
    {
        var context = new RuntimeContext();
        var initial = Stream("9.0000V\n", "0.1000A\n", "0.9000W\n");
        var failed = new ScriptedSerialByteStream(IdentityResponse(), "invalid\n");
        var recovered = Stream("10.0000V\n", "0.2000A\n", "2.0000W\n");
        var transport = new SequenceFactory(initial, failed, recovered);
        var factory = new Kel103PublishedAttachmentFactory(context, transport);
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        attachment.RuntimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            attachment.ReplaceAsync(SupportedOptions()));

        await attachment.ReplaceAsync(SupportedOptions());

        Assert.Equal(3, transport.OpenCount);
        Assert.Equal(EndpointConnectionState.Ready, attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Single(context.Endpoints);
        Assert.Equal(1, initial.DisposeCount);
        Assert.Equal(1, failed.DisposeCount);
        Assert.Equal(0, recovered.DisposeCount);
    }

    [Fact]
    public async Task ReplaceAsync_CancellationDisposesCandidateAndLeavesRecoverableFault()
    {
        var context = new RuntimeContext();
        var initial = Stream("9.0000V\n", "0.1000A\n", "0.9000W\n");
        using var cancellation = new CancellationTokenSource();
        var replacement = new CancelingSerialByteStream(cancellation);
        var transport = new SequenceFactory(initial, replacement);
        var factory = new Kel103PublishedAttachmentFactory(context, transport);
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        attachment.RuntimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(EndpointConnectionState.Faulted));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            attachment.ReplaceAsync(SupportedOptions(), cancellation.Token));

        Assert.Single(context.Endpoints);
        Assert.Equal(EndpointConnectionState.Faulted, attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(1, initial.DisposeCount);
        Assert.Equal(1, replacement.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_PreventsLaterReadsAndReplacement()
    {
        var context = new RuntimeContext();
        var initial = Stream("9.0000V\n", "0.1000A\n", "0.9000W\n");
        var transport = new SequenceFactory(initial);
        var factory = new Kel103PublishedAttachmentFactory(context, transport);
        Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());
        IEndpointAttachmentPropertyOperations port = attachment.PropertyOperations;

        await attachment.DisposeAsync();

        EndpointAttachmentPropertyOperationResult read = await port.ReadAsync(
            new InstrumentId("electronic-load-01"),
            new PropertyId("measured-voltage"));
        Assert.Equal(EndpointAttachmentPropertyOperationStatus.Unavailable, read.Status);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            attachment.ReplaceAsync(SupportedOptions()));
        Assert.Empty(context.Endpoints);
        Assert.Equal(1, initial.DisposeCount);
    }

    private static SerialTransportOptions SupportedOptions() =>
        new("TEST-PORT", 115200, 8, SerialParity.None, SerialStopBits.One, SerialHandshake.None);

    private static ScriptedSerialByteStream Stream(
        string voltage,
        string current,
        string power) =>
        new(IdentityResponse(), voltage, current, power);

    private static string IdentityResponse() => "RND 320-KEL103 V3.30 SN:REDACTED\n";

    private sealed class SequenceFactory(params ISerialByteStream[] streams)
        : ISerialByteStreamFactory
    {
        private int next;

        public int OpenCount => next;
        public List<int> PreviousDisposeCountsAtOpen { get; } = [];

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreviousDisposeCountsAtOpen.Add(
                streams.Take(next).OfType<IRecordingDisposal>().Sum(stream => stream.DisposeCount));
            if (next >= streams.Length)
            {
                throw new InvalidOperationException("No scripted stream remains.");
            }

            return ValueTask.FromResult(streams[next++]);
        }
    }

    private interface IRecordingDisposal
    {
        int DisposeCount { get; }
    }

    private sealed class ScriptedSerialByteStream(params string[] responses)
        : ISerialByteStream,
          IRecordingDisposal
    {
        private readonly Queue<byte[]> remaining = new(
            responses.Select(Encoding.ASCII.GetBytes));

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
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancelingSerialByteStream(CancellationTokenSource cancellation)
        : ISerialByteStream,
          IRecordingDisposal
    {
        public int DisposeCount { get; private set; }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingStatusObserver : IEndpointConnectionStatusObserver
    {
        public List<EndpointConnectionState> States { get; } = [];

        public void OnEndpointConnectionStatusChanged(EndpointConnectionStatusChanged change) =>
            States.Add(change.CurrentStatus.State);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 3, 21, 0, 0, TimeSpan.Zero);
    }
}

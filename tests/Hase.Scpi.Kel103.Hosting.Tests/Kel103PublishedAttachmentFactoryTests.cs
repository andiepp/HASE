using System.Text;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport.Serial;

namespace Hase.Scpi.Kel103.Hosting.Tests;

public sealed class Kel103PublishedAttachmentFactoryTests
{
    [Fact]
    public async Task OpenAsync_PublishesOnlyCompletelySynchronizedReadyEndpoint()
    {
        var context = new RuntimeContext();
        var stream = SuccessfulStream();
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            new RecordingFactory(stream),
            new FixedTimeProvider());

        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());

        Assert.Single(context.Endpoints);
        Assert.Same(attachment.RuntimeEndpoint, context.Endpoints.Single());
        Assert.Equal(EndpointConnectionState.Ready, attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.All(
            attachment.RuntimeEndpoint.Instruments.Single().Properties,
            property =>
            {
                Assert.NotNull(property.CurrentValue);
                Assert.Equal(FixedTimeProvider.Timestamp, property.CurrentValue.TimestampUtc);
            });
    }

    [Fact]
    public async Task OpenAsync_SynchronizationFailureNeverPublishesAndClosesStream()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream(IdentityResponse(), "invalid\n");
        var transport = new RecordingFactory(stream);
        var factory = new Kel103PublishedAttachmentFactory(context, transport);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions()));

        Assert.Empty(context.Endpoints);
        Assert.Equal(1, transport.OpenCount);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task OpenAsync_DuplicateIdentityLeavesExistingEndpointAndClosesNewStream()
    {
        var context = new RuntimeContext();
        RuntimeEndpoint existing = context.AddEndpoint(
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(
                new EndpointId("kel-test-01")));
        var stream = SuccessfulStream();
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            new RecordingFactory(stream));

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions()));

        Assert.Single(context.Endpoints);
        Assert.Same(existing, context.Endpoints.Single());
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_RemovesEndpointMarksDisconnectedAndClosesStream()
    {
        var context = new RuntimeContext();
        var stream = SuccessfulStream();
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            new RecordingFactory(stream));
        Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());

        await attachment.DisposeAsync();

        Assert.Empty(context.Endpoints);
        Assert.Equal(EndpointConnectionState.Disconnected, attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_IsConcurrentAndIdempotent()
    {
        var context = new RuntimeContext();
        var stream = SuccessfulStream();
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            new RecordingFactory(stream));
        Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());

        await Task.WhenAll(
            attachment.DisposeAsync().AsTask(),
            attachment.DisposeAsync().AsTask(),
            attachment.DisposeAsync().AsTask());

        Assert.Empty(context.Endpoints);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task PropertyOperations_AreBoundToPublishedConnection()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream(
            IdentityResponse(), "9.8864V\n", "0.1000A\n", "0.9893W\n", "10.0000V\n");
        var factory = new Kel103PublishedAttachmentFactory(
            context,
            new RecordingFactory(stream));
        await using Kel103PublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions());

        var result = await attachment.PropertyOperations.ReadAsync(
            new InstrumentId("electronic-load-01"),
            new PropertyId("measured-voltage"));

        Assert.True(result.IsSuccess);
        Assert.Equal(10.0000m, result.ConfirmedValue!.Value);
        Assert.Equal(5, stream.Writes.Count);
    }

    [Fact]
    public async Task OpenAsync_PreCanceledOperationNeverOpensOrPublishes()
    {
        var context = new RuntimeContext();
        var stream = SuccessfulStream();
        var transport = new RecordingFactory(stream);
        var factory = new Kel103PublishedAttachmentFactory(context, transport);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.OpenAsync(
            new EndpointId("kel-test-01"),
            SupportedOptions(),
            cancellation.Token));

        Assert.Empty(context.Endpoints);
        Assert.Equal(0, transport.OpenCount);
        Assert.Equal(0, stream.DisposeCount);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentFactory(null!, new RecordingFactory(SuccessfulStream())));
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103PublishedAttachmentFactory(new RuntimeContext(), null!));
    }

    [Fact]
    public void Assembly_DoesNotReferencePresentationOrRemoteLayers()
    {
        string[] references = typeof(Kel103PublishedAttachmentFactory).Assembly
            .GetReferencedAssemblies()
            .Select(value => value.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.Contains("Grpc", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name == "Hase.Client");
        Assert.DoesNotContain(references, name => name == "Hase.DesktopHost");
    }

    private static SerialTransportOptions SupportedOptions() =>
        new("TEST-PORT", 115200, 8, SerialParity.None, SerialStopBits.One, SerialHandshake.None);

    private static ScriptedSerialByteStream SuccessfulStream() => new(
        IdentityResponse(),
        "9.8864V\n",
        "0.1000A\n",
        "0.9893W\n");

    private static string IdentityResponse() => "RND 320-KEL103 V3.30 SN:REDACTED\n";

    private sealed class RecordingFactory(ISerialByteStream stream) : ISerialByteStreamFactory
    {
        public int OpenCount { get; private set; }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class ScriptedSerialByteStream(params string[] responses) : ISerialByteStream
    {
        private readonly Queue<byte[]> remaining = new(
            responses.Select(Encoding.ASCII.GetBytes));

        public List<string> Writes { get; } = [];
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
            Writes.Add(Encoding.ASCII.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public static DateTimeOffset Timestamp { get; } =
            new(2026, 8, 3, 18, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Timestamp;
    }
}

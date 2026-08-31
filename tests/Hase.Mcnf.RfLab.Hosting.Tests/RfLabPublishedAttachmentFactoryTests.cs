using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;

namespace Hase.Mcnf.RfLab.Hosting.Tests;

public sealed class RfLabPublishedAttachmentFactoryTests
{
    private static RfLabPublishedAttachmentFactory CreateFactory(
        RuntimeContext context,
        RecordingSerialFactory serialFactory,
        TimeProvider? timeProvider = null) =>
        new(
            context,
            serialFactory,
            settleDelay: TimeSpan.Zero,
            timeProvider);

    [Fact]
    public async Task OpenAsync_PublishesOnlyCompletelySynchronizedReadyEndpoint()
    {
        var context = new RuntimeContext();
        var serialFactory = new RecordingSerialFactory(
            RfLabHostingTestSupport.SuccessfulOpenStream());
        RfLabPublishedAttachmentFactory factory =
            CreateFactory(context, serialFactory, new FixedTimeProvider());

        await using RfLabPublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions());

        Assert.Single(context.Endpoints);
        Assert.Same(attachment.RuntimeEndpoint, context.Endpoints.Single());
        Assert.Equal(
            EndpointConnectionState.Ready,
            attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.All(
            attachment.RuntimeEndpoint.Instruments.Single().Properties,
            property =>
            {
                Assert.NotNull(property.CurrentValue);
                Assert.Equal(
                    FixedTimeProvider.Timestamp,
                    property.CurrentValue.TimestampUtc);
            });
    }

    [Fact]
    public async Task OpenAsync_SynchronizationFailureNeverPublishesAndClosesStream()
    {
        var context = new RuntimeContext();
        var stream = new ScriptedSerialByteStream(
            RfLabHostingTestSupport.ConnectivityResponse(),
            RfLabHostingTestSupport.SuccessResponse(0xAE, 0x63, 0x05, 0x80));
        var serialFactory = new RecordingSerialFactory(stream);
        RfLabPublishedAttachmentFactory factory = CreateFactory(context, serialFactory);

        await Assert.ThrowsAsync<InvalidDataException>(() => factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions()));

        Assert.Empty(context.Endpoints);
        Assert.Equal(1, serialFactory.OpenCount);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task OpenAsync_DuplicateIdentityLeavesExistingEndpointAndClosesNewStream()
    {
        var context = new RuntimeContext();
        RuntimeEndpoint existing = context.AddEndpoint(
            RfLabReadOnlyDefinition.EndpointDefinition.Materialize(
                new EndpointId("rflab-test-01")));
        var stream = RfLabHostingTestSupport.SuccessfulOpenStream();
        RfLabPublishedAttachmentFactory factory =
            CreateFactory(context, new RecordingSerialFactory(stream));

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions()));

        Assert.Single(context.Endpoints);
        Assert.Same(existing, context.Endpoints.Single());
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_RemovesEndpointMarksDisconnectedAndClosesStream()
    {
        var context = new RuntimeContext();
        var stream = RfLabHostingTestSupport.SuccessfulOpenStream();
        RfLabPublishedAttachmentFactory factory =
            CreateFactory(context, new RecordingSerialFactory(stream));
        RfLabPublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions());

        await attachment.DisposeAsync();

        Assert.Empty(context.Endpoints);
        Assert.Equal(
            EndpointConnectionState.Disconnected,
            attachment.RuntimeEndpoint.ConnectionStatus.State);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_IsConcurrentAndIdempotent()
    {
        var context = new RuntimeContext();
        var stream = RfLabHostingTestSupport.SuccessfulOpenStream();
        RfLabPublishedAttachmentFactory factory =
            CreateFactory(context, new RecordingSerialFactory(stream));
        RfLabPublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions());

        await Task.WhenAll(
            attachment.DisposeAsync().AsTask(),
            attachment.DisposeAsync().AsTask(),
            attachment.DisposeAsync().AsTask());

        Assert.Empty(context.Endpoints);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task PropertyOperations_AreBoundToThePublishedConnection()
    {
        var context = new RuntimeContext();
        var stream = RfLabHostingTestSupport.SuccessfulOpenStream(
            RfLabHostingTestSupport.SensorResponse(1000));
        RfLabPublishedAttachmentFactory factory =
            CreateFactory(context, new RecordingSerialFactory(stream));
        await using RfLabPublishedAttachment attachment = await factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions());

        var result = await attachment.PropertyOperations.ReadAsync(
            new InstrumentId("rf-minilab-01"),
            new PropertyId("sensor-voltage"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2500.0, result.ConfirmedValue!.Value);
        Assert.Equal(5, stream.Writes.Count);
        Assert.Same(attachment.PropertyOperations, attachment.CommandOperations);
    }

    [Fact]
    public async Task OpenAsync_PreCanceledOperationNeverOpensOrPublishes()
    {
        var context = new RuntimeContext();
        var serialFactory = new RecordingSerialFactory(
            RfLabHostingTestSupport.SuccessfulOpenStream());
        RfLabPublishedAttachmentFactory factory = CreateFactory(context, serialFactory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.OpenAsync(
            new EndpointId("rflab-test-01"),
            RfLabHostingTestSupport.SupportedOptions(),
            cancellation.Token));

        Assert.Empty(context.Endpoints);
        Assert.Equal(0, serialFactory.OpenCount);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RfLabPublishedAttachmentFactory(null!, new RecordingSerialFactory()));
        Assert.Throws<ArgumentNullException>(() =>
            new RfLabPublishedAttachmentFactory(new RuntimeContext(), null!));
    }

    [Fact]
    public void Assembly_DoesNotReferencePresentationOrRemoteLayers()
    {
        string[] references = typeof(RfLabPublishedAttachmentFactory).Assembly
            .GetReferencedAssemblies()
            .Select(value => value.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.Contains("Grpc", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.Contains("Wpf", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name == "Hase.Client");
        Assert.DoesNotContain(references, name => name == "Hase.DesktopHost");
    }
}

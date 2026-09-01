using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.Hosting;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Hase.Transport.Tcp;

namespace Hase.DesktopHost.Tests.Hosting;

public sealed class DesktopRuntimeHostEndpointProviderRegistryTests
{
    [Fact]
    public void Constructor_WithoutProviders_RegistersNothing()
    {
        var registry = new DesktopRuntimeHostEndpointProviderRegistry();

        Assert.Empty(registry.RegisteredProviderIds);
        Assert.False(registry.TryResolve("anything", out _));
    }

    [Fact]
    public void Constructor_RejectsDuplicateProviderIdentifiers()
    {
        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostEndpointProviderRegistry(
                [
                    new StubProvider("duplicate"),
                    new StubProvider("duplicate")
                ]));
    }

    [Fact]
    public void Constructor_RejectsEmptyProviderIdentifiers()
    {
        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostEndpointProviderRegistry(
                [new StubProvider("   ")]));
    }

    [Fact]
    public void TryResolve_FindsEachRegisteredProvider()
    {
        var first = new StubProvider("first");
        var second = new StubProvider("second");
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [first, second]);

        Assert.True(registry.TryResolve("second", out var resolved));
        Assert.Same(second, resolved);
        Assert.False(registry.TryResolve("third", out _));
        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { "first", "second" },
            registry.RegisteredProviderIds);
    }

    [Fact]
    public async Task ResolveAsync_PreservesRegistrationOrder()
    {
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [
                new StubProvider("first", "endpoint-a", "endpoint-b"),
                new StubProvider("second", "endpoint-c")
            ]);

        DesktopRuntimeHostEndpointResolution resolution =
            await registry.ResolveAsync(CreateContext());

        Assert.Equal(
            ["endpoint-a", "endpoint-b", "endpoint-c"],
            resolution.Attachments.Select(attachment => attachment.EndpointId));
    }

    [Fact]
    public async Task ResolveAsync_NamesOnlyTheProvidersThatContributed()
    {
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [
                new StubProvider("configured", "endpoint-a"),
                new StubProvider("unconfigured")
            ]);

        DesktopRuntimeHostEndpointResolution resolution =
            await registry.ResolveAsync(CreateContext());

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { "configured" },
            resolution.ContributingProviderIds);
    }

    [Fact]
    public async Task ResolveAsync_RejectsDuplicateEndpointIdentities()
    {
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [
                new StubProvider("first", "endpoint-a"),
                new StubProvider("second", "endpoint-a")
            ]);

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => registry.ResolveAsync(CreateContext()));

        Assert.Contains("endpoint-a", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAsync_WithoutProviders_ResolvesNothing()
    {
        var registry = new DesktopRuntimeHostEndpointProviderRegistry();

        DesktopRuntimeHostEndpointResolution resolution =
            await registry.ResolveAsync(CreateContext());

        Assert.Empty(resolution.Attachments);
        Assert.Empty(resolution.ContributingProviderIds);
    }

    [Fact]
    public async Task CreateAttachmentService_NeverAsksAnUncontributingProvider()
    {
        var unconfigured = new StubProvider("unconfigured")
        {
            AttachmentService = new RecordingService(),
            SupportedDefinition = typeof(NetworkEndpointConnectionDefinition)
        };
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [unconfigured]);

        DesktopRuntimeHostEndpointResolution resolution =
            await registry.ResolveAsync(CreateContext());
        IEndpointAttachmentService service = registry.CreateAttachmentService(
            CreateRuntimeContext(),
            resolution);

        Assert.Equal(0, unconfigured.AttachmentServiceRequests);
        await Assert.ThrowsAsync<NotSupportedException>(
            () => service.AttachAsync(NetworkRequest()));
    }

    [Fact]
    public async Task CreateAttachmentService_RoutesToTheSupportingProvider()
    {
        var networkService = new RecordingService();
        var serialService = new RecordingService();
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [
                new StubProvider("network", "endpoint-a")
                {
                    AttachmentService = networkService,
                    SupportedDefinition =
                        typeof(NetworkEndpointConnectionDefinition)
                },
                new StubProvider("serial", "endpoint-b")
                {
                    AttachmentService = serialService,
                    SupportedDefinition =
                        typeof(SerialEndpointConnectionDefinition)
                }
            ]);

        DesktopRuntimeHostEndpointResolution resolution =
            await registry.ResolveAsync(CreateContext());
        IEndpointAttachmentService service = registry.CreateAttachmentService(
            CreateRuntimeContext(),
            resolution);

        await Assert.ThrowsAsync<ExpectedServiceException>(
            () => service.AttachAsync(NetworkRequest()));
        await Assert.ThrowsAsync<ExpectedServiceException>(
            () => service.AttachAsync(SerialRequest()));

        Assert.Equal(1, networkService.AttachCount);
        Assert.Equal(1, serialService.AttachCount);
        Assert.IsType<NetworkEndpointConnectionDefinition>(
            networkService.LastRequest!.ConnectionDefinition);
        Assert.IsType<SerialEndpointConnectionDefinition>(
            serialService.LastRequest!.ConnectionDefinition);
    }

    [Fact]
    public async Task CreateAttachmentService_RejectsUnregisteredDefinitions()
    {
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [
                new StubProvider("network", "endpoint-a")
                {
                    AttachmentService = new RecordingService(),
                    SupportedDefinition =
                        typeof(NetworkEndpointConnectionDefinition)
                }
            ]);

        DesktopRuntimeHostEndpointResolution resolution =
            await registry.ResolveAsync(CreateContext());
        IEndpointAttachmentService service = registry.CreateAttachmentService(
            CreateRuntimeContext(),
            resolution);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => service.AttachAsync(SerialRequest()));
    }

    internal static DesktopRuntimeHostEndpointProviderContext CreateContext(
        DesktopRuntimeHostEndpointCompositionProfile? endpointComposition = null) =>
        new(
            endpointComposition ?? CreateComposition(),
            new InMemoryCompactEndpointDefinitionRepository([]));

    internal static DesktopRuntimeHostEndpointCompositionProfile
        CreateComposition() =>
        new(
            [
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "native-01",
                    "127.0.0.1",
                    5000)
            ],
            []);

    private static RuntimeContext CreateRuntimeContext() =>
        new(new RuntimeDiagnosticPublisher());

    private static EndpointAttachmentRequest NetworkRequest() => new(
        NetworkEndpointConnectionDefinition.FromConfiguration(
            new TcpTransportOptions("127.0.0.1", 5000),
            new EndpointId("native-01")),
        EndpointProvidedDescriptorSource.Instance);

    private static EndpointAttachmentRequest SerialRequest() => new(
        SerialEndpointConnectionDefinition.FromConfiguration(
            new SerialTransportOptions("COM9", 115200),
            new EndpointId("compact-01")),
        HostRepositoryDescriptorSource.Instance);

    private sealed class StubProvider
        : IDesktopRuntimeHostEndpointProvider
    {
        private readonly IReadOnlyList<string> endpointIds;

        public StubProvider(string providerId, params string[] endpointIds)
        {
            ProviderId = providerId;
            this.endpointIds = endpointIds;
        }

        public string ProviderId { get; }

        public IEndpointAttachmentService? AttachmentService { get; init; }

        public Type? SupportedDefinition { get; init; }

        public int AttachmentServiceRequests { get; private set; }

        public bool Supports(IEndpointConnectionDefinition connectionDefinition) =>
            SupportedDefinition is not null
            && SupportedDefinition.IsInstanceOfType(connectionDefinition);

        public IEndpointAttachmentService? CreateAttachmentService(
            RuntimeContext runtimeContext)
        {
            AttachmentServiceRequests++;
            return AttachmentService;
        }

        public Task<IReadOnlyList<DesktopRuntimeHostEndpointAttachment>>
            ResolveAttachmentsAsync(
                DesktopRuntimeHostEndpointProviderContext context,
                CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DesktopRuntimeHostEndpointAttachment>>(
                endpointIds
                    .Select(endpointId =>
                        new DesktopRuntimeHostEndpointAttachment(
                            endpointId,
                            "Stub",
                            (_, _) => Task.CompletedTask))
                    .ToArray());
    }

    private sealed class ExpectedServiceException : Exception
    {
    }

    private sealed class RecordingService : IEndpointAttachmentService
    {
        public int AttachCount { get; private set; }

        public EndpointAttachmentRequest? LastRequest { get; private set; }

        public Task<IEndpointAttachmentSession> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            AttachCount++;
            LastRequest = request;
            throw new ExpectedServiceException();
        }
    }

    internal sealed class StubAttachmentInventory
        : IRuntimeEndpointAttachmentInventory
    {
        public List<EndpointAttachmentRequest> Requests { get; } = [];

        public Task<RuntimeEndpointAttachmentInventoryEntry> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            throw new NotSupportedException(
                "The stub inventory establishes no attachment session.");
        }

        public RuntimeEndpointAttachmentInventoryEntry? Find(
            EndpointId endpointId) => null;

        public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List() =>
            [];

        public Task<bool> DetachAsync(
            EndpointId endpointId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

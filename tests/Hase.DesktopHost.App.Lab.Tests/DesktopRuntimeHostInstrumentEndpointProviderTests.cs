using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.App.Lab;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.Hosting;
using Hase.Mcnf.RfLab;
using Hase.Mcnf.RfLab.DesktopHost;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103;
using Hase.Scpi.Kel103.DesktopHost;
using Hase.Transport.Serial;
using Hase.Transport.Tcp;

namespace Hase.DesktopHost.App.Lab.Tests;

/// <summary>
/// Covers the instrument families now supplied through the endpoint-provider
/// registry rather than named by the Runtime Host application.
/// </summary>
public sealed class DesktopRuntimeHostInstrumentEndpointProviderTests
{
    [Fact]
    public async Task Kel103Provider_ResolvesEveryConfiguredEndpoint()
    {
        var provider = new DesktopRuntimeHostKel103EndpointProvider();

        IReadOnlyList<DesktopRuntimeHostEndpointAttachment> attachments =
            await provider.ResolveAttachmentsAsync(CreateContext());

        DesktopRuntimeHostEndpointAttachment attachment =
            Assert.Single(attachments);

        Assert.Equal("kel-01", attachment.EndpointId);
        Assert.Equal("Kel103Serial", attachment.EndpointKind);
    }

    [Fact]
    public async Task RfLabProvider_ResolvesEveryConfiguredEndpoint()
    {
        var provider = new DesktopRuntimeHostRfLabEndpointProvider();

        IReadOnlyList<DesktopRuntimeHostEndpointAttachment> attachments =
            await provider.ResolveAttachmentsAsync(CreateContext());

        DesktopRuntimeHostEndpointAttachment attachment =
            Assert.Single(attachments);

        Assert.Equal("rflab-01", attachment.EndpointId);
        Assert.Equal("RfLabSerial", attachment.EndpointKind);
    }

    [Fact]
    public async Task Providers_ResolveNothingForAnUnconfiguredFamily()
    {
        DesktopRuntimeHostEndpointProviderContext context =
            CreateContext(GenericOnlyComposition());

        Assert.Empty(
            await new DesktopRuntimeHostKel103EndpointProvider()
                .ResolveAttachmentsAsync(context));
        Assert.Empty(
            await new DesktopRuntimeHostRfLabEndpointProvider()
                .ResolveAttachmentsAsync(context));
    }

    [Fact]
    public async Task AnUnconfiguredFamilyIsNeverAskedForAnAttachmentService()
    {
        int kel103ServiceRequests = 0;
        int rfLabServiceRequests = 0;
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [
                new DesktopRuntimeHostNativeNetworkEndpointProvider(),
                new DesktopRuntimeHostKel103EndpointProvider(
                    _ =>
                    {
                        kel103ServiceRequests++;
                        throw new InvalidOperationException(
                            "The KEL-103 service was not expected.");
                    }),
                new DesktopRuntimeHostRfLabEndpointProvider(
                    _ =>
                    {
                        rfLabServiceRequests++;
                        throw new InvalidOperationException(
                            "The RF-Lab service was not expected.");
                    })
            ]);

        DesktopRuntimeHostEndpointResolution resolution =
            await registry.ResolveAsync(CreateContext(GenericOnlyComposition()));
        registry.CreateAttachmentService(new RuntimeContext(), resolution);

        Assert.Equal(0, kel103ServiceRequests);
        Assert.Equal(0, rfLabServiceRequests);
        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { "native-network" },
            resolution.ContributingProviderIds);
    }

    [Fact]
    public async Task AConfiguredFamilyRoutesItsOwnConnectionDefinition()
    {
        var kel103Service = new RecordingService();
        var rfLabService = new RecordingService();
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [
                new DesktopRuntimeHostKel103EndpointProvider(
                    _ => kel103Service),
                new DesktopRuntimeHostRfLabEndpointProvider(
                    _ => rfLabService)
            ]);

        DesktopRuntimeHostEndpointResolution resolution =
            await registry.ResolveAsync(CreateContext());
        IEndpointAttachmentService service = registry.CreateAttachmentService(
            new RuntimeContext(),
            resolution);

        await Assert.ThrowsAsync<ExpectedServiceException>(
            () => service.AttachAsync(Kel103Request()));
        await Assert.ThrowsAsync<ExpectedServiceException>(
            () => service.AttachAsync(RfLabRequest()));

        Assert.Equal(1, kel103Service.AttachCount);
        Assert.Equal(1, rfLabService.AttachCount);
        Assert.IsType<DesktopRuntimeHostKel103ConnectionDefinition>(
            kel103Service.LastRequest!.ConnectionDefinition);
        Assert.IsType<DesktopRuntimeHostRfLabConnectionDefinition>(
            rfLabService.LastRequest!.ConnectionDefinition);
    }

    [Fact]
    public void Providers_SupportOnlyTheirOwnConnectionDefinitions()
    {
        var kel103Provider = new DesktopRuntimeHostKel103EndpointProvider();
        var rfLabProvider = new DesktopRuntimeHostRfLabEndpointProvider();

        Assert.True(kel103Provider.Supports(Kel103Definition()));
        Assert.False(kel103Provider.Supports(RfLabDefinition()));
        Assert.True(rfLabProvider.Supports(RfLabDefinition()));
        Assert.False(rfLabProvider.Supports(Kel103Definition()));
        Assert.False(
            kel103Provider.Supports(
                NetworkEndpointConnectionDefinition.FromConfiguration(
                    new TcpTransportOptions("127.0.0.1", 5000),
                    new EndpointId("native-01"))));
    }

    [Fact]
    public void TheLabCompositionRegistersEveryFamilyThisHostOperates()
    {
        DesktopRuntimeHostEndpointProviderRegistry registry =
            LabApp.CreateLabEndpointProviders();

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "native-network",
                "compact-serial",
                "kel-103-serial",
                "rf-lab-serial"
            },
            registry.RegisteredProviderIds);
    }

    [Fact]
    public void InstrumentHostProjects_DoNotReachIntoTheClientOrPresentation()
    {
        foreach (Type provider in new[]
            {
                typeof(DesktopRuntimeHostKel103EndpointProvider),
                typeof(DesktopRuntimeHostRfLabEndpointProvider)
            })
        {
            string[] references = provider.Assembly
                .GetReferencedAssemblies()
                .Select(value => value.Name ?? string.Empty)
                .ToArray();

            Assert.DoesNotContain(
                references,
                name => name.Contains("Wpf", StringComparison.Ordinal));
            Assert.DoesNotContain(references, name => name == "Hase.Client");
        }
    }

    private static DesktopRuntimeHostEndpointProviderContext CreateContext(
        DesktopRuntimeHostEndpointCompositionProfile? endpointComposition =
            null) =>
        new(
            endpointComposition ?? CreateComposition(),
            new InMemoryCompactEndpointDefinitionRepository([]));

    private static DesktopRuntimeHostEndpointCompositionProfile
        CreateComposition() =>
        new(
            [],
            [],
            [
                new DesktopRuntimeHostKel103SerialEndpointProfile(
                    "kel-01",
                    Kel103ReadOnlyMeasurementDefinition.Reference.Id.Value,
                    Kel103ReadOnlyMeasurementDefinition.Reference.Version,
                    "external-target",
                    115200)
            ],
            [
                new DesktopRuntimeHostRfLabSerialEndpointProfile(
                    "rflab-01",
                    RfLabReadOnlyDefinition.Reference.Id.Value,
                    RfLabReadOnlyDefinition.Reference.Version,
                    "external-target",
                    115200)
            ]);

    private static DesktopRuntimeHostEndpointCompositionProfile
        GenericOnlyComposition() =>
        new(
            [
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "native-01",
                    "127.0.0.1",
                    5000)
            ],
            []);

    private static DesktopRuntimeHostKel103ConnectionDefinition
        Kel103Definition() =>
        new(
            new EndpointId("kel-01"),
            new SerialTransportOptions("external-target", 115200));

    private static DesktopRuntimeHostRfLabConnectionDefinition
        RfLabDefinition() =>
        new(
            new EndpointId("rflab-01"),
            new SerialTransportOptions("external-target", 115200));

    private static EndpointAttachmentRequest Kel103Request() => new(
        Kel103Definition(),
        HostRepositoryDescriptorSource.Instance);

    private static EndpointAttachmentRequest RfLabRequest() => new(
        RfLabDefinition(),
        HostRepositoryDescriptorSource.Instance);

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
}

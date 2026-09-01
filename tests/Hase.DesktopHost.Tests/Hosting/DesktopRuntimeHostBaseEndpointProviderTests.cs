using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;
using Hase.DesktopHost.Hosting;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Hase.Transport.Tcp;

namespace Hase.DesktopHost.Tests.Hosting;

/// <summary>
/// Covers the endpoint providers the Runtime Host application composes for
/// the endpoint kinds that carry no instrument knowledge.
/// </summary>
public sealed class DesktopRuntimeHostBaseEndpointProviderTests
{
    [Fact]
    public async Task NativeNetworkProvider_ResolvesEveryConfiguredEndpoint()
    {
        var provider = new DesktopRuntimeHostNativeNetworkEndpointProvider();

        IReadOnlyList<DesktopRuntimeHostEndpointAttachment> attachments =
            await provider.ResolveAttachmentsAsync(CreateContext());

        Assert.Equal(
            ["native-01", "native-02"],
            attachments.Select(attachment => attachment.EndpointId));
        Assert.All(
            attachments,
            attachment =>
                Assert.Equal("NativeNetwork", attachment.EndpointKind));
    }

    [Fact]
    public async Task CompactSerialProvider_ResolvesEveryConfiguredEndpoint()
    {
        var provider = new DesktopRuntimeHostCompactSerialEndpointProvider();

        IReadOnlyList<DesktopRuntimeHostEndpointAttachment> attachments =
            await provider.ResolveAttachmentsAsync(CreateContext());

        DesktopRuntimeHostEndpointAttachment attachment =
            Assert.Single(attachments);

        Assert.Equal("compact-01", attachment.EndpointId);
        Assert.Equal("CompactSerial", attachment.EndpointKind);
    }

    [Fact]
    public async Task Providers_ResolveNothingForAnEmptyFamily()
    {
        var composition = new DesktopRuntimeHostEndpointCompositionProfile(
            [
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "native-01",
                    "127.0.0.1",
                    5000)
            ],
            []);
        DesktopRuntimeHostEndpointProviderContext context =
            CreateContext(composition);

        Assert.Empty(
            await new DesktopRuntimeHostCompactSerialEndpointProvider()
                .ResolveAttachmentsAsync(context));
        Assert.Single(
            await new DesktopRuntimeHostNativeNetworkEndpointProvider()
                .ResolveAttachmentsAsync(context));
    }

    [Fact]
    public void Providers_SupportOnlyTheirOwnConnectionDefinitions()
    {
        var nativeProvider =
            new DesktopRuntimeHostNativeNetworkEndpointProvider();
        var compactProvider =
            new DesktopRuntimeHostCompactSerialEndpointProvider();
        NetworkEndpointConnectionDefinition networkDefinition =
            NetworkEndpointConnectionDefinition.FromConfiguration(
                new TcpTransportOptions("127.0.0.1", 5000),
                new EndpointId("native-01"));
        SerialEndpointConnectionDefinition serialDefinition =
            SerialEndpointConnectionDefinition.FromConfiguration(
                new SerialTransportOptions("COM9", 115200),
                new EndpointId("compact-01"));

        Assert.True(nativeProvider.Supports(networkDefinition));
        Assert.False(nativeProvider.Supports(serialDefinition));
        Assert.True(compactProvider.Supports(serialDefinition));
        Assert.False(compactProvider.Supports(networkDefinition));
    }

    [Fact]
    public void Providers_ContributeNoAttachmentServiceOfTheirOwn()
    {
        var runtimeContext = new RuntimeContext();

        Assert.Null(
            new DesktopRuntimeHostNativeNetworkEndpointProvider()
                .CreateAttachmentService(runtimeContext));
        Assert.Null(
            new DesktopRuntimeHostCompactSerialEndpointProvider()
                .CreateAttachmentService(runtimeContext));
    }

    [Fact]
    public void Providers_AreRegisteredUnderDistinctIdentifiers()
    {
        var registry = new DesktopRuntimeHostEndpointProviderRegistry(
            [
                new DesktopRuntimeHostNativeNetworkEndpointProvider(),
                new DesktopRuntimeHostCompactSerialEndpointProvider()
            ]);

        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "native-network",
                "compact-serial"
            },
            registry.RegisteredProviderIds);
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
            [
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "native-01",
                    "127.0.0.1",
                    5000),
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "native-02",
                    "127.0.0.2",
                    5001)
            ],
            [
                new DesktopRuntimeHostCompactSerialEndpointProfile(
                    "compact-01",
                    vendorId: 0x2341,
                    productId: 0x0043,
                    baudRate: 115200,
                    verificationTimeout: TimeSpan.FromSeconds(5))
            ]);
}

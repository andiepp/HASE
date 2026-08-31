using Hase.Core.Domain.Identity;
using Hase.DesktopHost.App.Hosting;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostInstrumentAttachmentRouterTests
{
    private static EndpointAttachmentRequest Kel103Request() => new(
        new DesktopRuntimeHostKel103ConnectionDefinition(
            new EndpointId("kel-01"),
            new SerialTransportOptions("kel-target", 115200)),
        HostRepositoryDescriptorSource.Instance);

    private static EndpointAttachmentRequest RfLabRequest() => new(
        new DesktopRuntimeHostRfLabConnectionDefinition(
            new EndpointId("rflab-01"),
            new SerialTransportOptions("rflab-target", 115200)),
        HostRepositoryDescriptorSource.Instance);

    [Fact]
    public async Task AttachAsync_DispatchesEachFamilyToItsService()
    {
        var kel103Service = new RecordingService();
        var rfLabService = new RecordingService();
        var router = new DesktopRuntimeHostInstrumentAttachmentRouter(
            kel103Service,
            rfLabService);

        await Assert.ThrowsAsync<ExpectedServiceException>(
            () => router.AttachAsync(Kel103Request()));
        await Assert.ThrowsAsync<ExpectedServiceException>(
            () => router.AttachAsync(RfLabRequest()));

        Assert.Equal(1, kel103Service.AttachCount);
        Assert.Equal(1, rfLabService.AttachCount);
        Assert.IsType<DesktopRuntimeHostKel103ConnectionDefinition>(
            kel103Service.LastRequest!.ConnectionDefinition);
        Assert.IsType<DesktopRuntimeHostRfLabConnectionDefinition>(
            rfLabService.LastRequest!.ConnectionDefinition);
    }

    [Fact]
    public async Task AttachAsync_RejectsFamiliesWithoutARegisteredService()
    {
        var kel103Only = new DesktopRuntimeHostInstrumentAttachmentRouter(
            new RecordingService(),
            rfLabService: null);
        var rfLabOnly = new DesktopRuntimeHostInstrumentAttachmentRouter(
            kel103Service: null,
            new RecordingService());

        await Assert.ThrowsAsync<NotSupportedException>(
            () => kel103Only.AttachAsync(RfLabRequest()));
        await Assert.ThrowsAsync<NotSupportedException>(
            () => rfLabOnly.AttachAsync(Kel103Request()));
    }

    [Fact]
    public async Task AttachAsync_RejectsForeignConnectionDefinitions()
    {
        var router = new DesktopRuntimeHostInstrumentAttachmentRouter(
            new RecordingService(),
            new RecordingService());
        var request = new EndpointAttachmentRequest(
            new ForeignConnectionDefinition(),
            HostRepositoryDescriptorSource.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => router.AttachAsync(request));
    }

    [Fact]
    public void Constructor_RequiresAtLeastOneService()
    {
        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostInstrumentAttachmentRouter(null, null));
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

    private sealed class ForeignConnectionDefinition : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin => EndpointConnectionOrigin.Configured;
        public EndpointId? ExpectedEndpointId => null;
    }
}

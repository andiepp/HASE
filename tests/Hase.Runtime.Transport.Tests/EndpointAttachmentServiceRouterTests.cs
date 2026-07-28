using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Hase.Transport.Tcp;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointAttachmentServiceRouterTests
{
    [Fact]
    public async Task AttachAsync_NetworkRequest_ShouldUseNativeRoute()
    {
        var nativeService =
            new RecordingAttachmentService();
        var compactService =
            new RecordingAttachmentService();
        var router =
            new EndpointAttachmentServiceRouter(
                nativeService,
                compactService);
        EndpointAttachmentRequest request =
            CreateNetworkRequest();

        await Assert.ThrowsAsync<ExpectedRouteException>(
            () => router.AttachAsync(
                request));

        Assert.Same(
            request,
            nativeService.LastRequest);
        Assert.Null(
            compactService.LastRequest);
    }

    [Fact]
    public async Task AttachAsync_SerialRequest_ShouldUseCompactRoute()
    {
        var nativeService =
            new RecordingAttachmentService();
        var compactService =
            new RecordingAttachmentService();
        var router =
            new EndpointAttachmentServiceRouter(
                nativeService,
                compactService);
        EndpointAttachmentRequest request =
            CreateSerialRequest();

        await Assert.ThrowsAsync<ExpectedRouteException>(
            () => router.AttachAsync(
                request));

        Assert.Null(
            nativeService.LastRequest);
        Assert.Same(
            request,
            compactService.LastRequest);
    }

    [Fact]
    public async Task AttachAsync_InProcessRequest_ShouldUseInProcessRoute()
    {
        var nativeService =
            new RecordingAttachmentService();
        var compactService =
            new RecordingAttachmentService();
        var inProcessService =
            new RecordingAttachmentService();
        var router =
            new EndpointAttachmentServiceRouter(
                nativeService,
                compactService,
                inProcessService);
        var request =
            new EndpointAttachmentRequest(
                new InProcessEndpointConnectionDefinition(
                    new EndpointDescriptor(
                        new EndpointId(
                            "simulation-endpoint")),
                    _ => throw new InvalidOperationException()),
                InProcessEndpointDescriptorSource.Instance);

        await Assert.ThrowsAsync<ExpectedRouteException>(
            () => router.AttachAsync(
                request));

        Assert.Null(
            nativeService.LastRequest);
        Assert.Null(
            compactService.LastRequest);
        Assert.Same(
            request,
            inProcessService.LastRequest);
    }

    [Fact]
    public async Task AttachAsync_InProcessRequestWithoutRoute_ShouldThrow()
    {
        var router =
            new EndpointAttachmentServiceRouter(
                new RecordingAttachmentService(),
                new RecordingAttachmentService());
        var request =
            new EndpointAttachmentRequest(
                new InProcessEndpointConnectionDefinition(
                    new EndpointDescriptor(
                        new EndpointId(
                            "simulation-endpoint")),
                    _ => throw new InvalidOperationException()),
                InProcessEndpointDescriptorSource.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => router.AttachAsync(
                request));
    }

    [Fact]
    public async Task AttachAsync_UnsupportedRequest_ShouldThrow()
    {
        var router =
            new EndpointAttachmentServiceRouter(
                new RecordingAttachmentService(),
                new RecordingAttachmentService());
        var request =
            new EndpointAttachmentRequest(
                new UnsupportedConnectionDefinition(),
                EndpointProvidedDescriptorSource.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => router.AttachAsync(
                request));
    }

    [Fact]
    public void Constructor_NullNativeService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "nativeNetworkService",
            () => new EndpointAttachmentServiceRouter(
                null!,
                new RecordingAttachmentService()));
    }

    [Fact]
    public void Constructor_NullCompactService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "compactSerialService",
            () => new EndpointAttachmentServiceRouter(
                new RecordingAttachmentService(),
                null!));
    }

    private static EndpointAttachmentRequest CreateNetworkRequest()
    {
        return new EndpointAttachmentRequest(
            NetworkEndpointConnectionDefinition.FromConfiguration(
                new TcpTransportOptions(
                    "192.0.2.1",
                    5000)),
            EndpointProvidedDescriptorSource.Instance);
    }

    private static EndpointAttachmentRequest CreateSerialRequest()
    {
        return new EndpointAttachmentRequest(
            SerialEndpointConnectionDefinition.FromConfiguration(
                new SerialTransportOptions(
                    "COM1",
                    115200)),
            HostRepositoryDescriptorSource.Instance);
    }

    private sealed class RecordingAttachmentService
        : IEndpointAttachmentService
    {
        public EndpointAttachmentRequest? LastRequest
        {
            get;
            private set;
        }

        public Task<IEndpointAttachmentSession> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest =
                request;

            return Task.FromException<IEndpointAttachmentSession>(
                new ExpectedRouteException());
        }
    }

    private sealed class UnsupportedConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public Hase.Core.Domain.Identity.EndpointId? ExpectedEndpointId =>
            null;
    }

    private sealed class ExpectedRouteException
        : Exception;
}

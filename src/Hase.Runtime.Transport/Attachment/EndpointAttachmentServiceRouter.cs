namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Routes heterogeneous endpoint attachment requests to transport-specific
/// services owned by one runtime host.
/// </summary>
/// <remarks>
/// The routed services are expected to share the same runtime context. This
/// router does not attach, discover, select, or replace endpoints by itself.
/// </remarks>
public sealed class EndpointAttachmentServiceRouter
    : IEndpointAttachmentService
{
    private readonly IEndpointAttachmentService
        _nativeNetworkService;

    private readonly IEndpointAttachmentService
        _compactSerialService;

    /// <summary>
    /// Initializes the transport-specific attachment routes.
    /// </summary>
    public EndpointAttachmentServiceRouter(
        IEndpointAttachmentService nativeNetworkService,
        IEndpointAttachmentService compactSerialService)
    {
        _nativeNetworkService =
            nativeNetworkService
            ?? throw new ArgumentNullException(
                nameof(nativeNetworkService));

        _compactSerialService =
            compactSerialService
            ?? throw new ArgumentNullException(
                nameof(compactSerialService));
    }

    /// <inheritdoc />
    public Task<IEndpointAttachmentSession> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        return request.ConnectionDefinition switch
        {
            NetworkEndpointConnectionDefinition =>
                _nativeNetworkService.AttachAsync(
                    request,
                    cancellationToken),

            SerialEndpointConnectionDefinition =>
                _compactSerialService.AttachAsync(
                    request,
                    cancellationToken),

            _ =>
                throw new NotSupportedException(
                    "No endpoint attachment service is registered for "
                    + "the requested connection definition.")
        };
    }
}

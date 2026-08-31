using Hase.Runtime.Transport.Attachment;

namespace Hase.DesktopHost.App.Hosting;

/// <summary>
/// Routes the additional-family attachment requests of this host between the
/// configured serial instrument services. The runtime attachment host offers
/// exactly one additional-service slot; this router lets more than one
/// instrument family share it without widening the transport API.
/// </summary>
public sealed class DesktopRuntimeHostInstrumentAttachmentRouter
    : IEndpointAttachmentService
{
    private readonly IEndpointAttachmentService? kel103Service;
    private readonly IEndpointAttachmentService? rfLabService;

    public DesktopRuntimeHostInstrumentAttachmentRouter(
        IEndpointAttachmentService? kel103Service,
        IEndpointAttachmentService? rfLabService)
    {
        if (kel103Service is null && rfLabService is null)
        {
            throw new ArgumentException(
                "The instrument attachment router requires at least one service.",
                nameof(kel103Service));
        }

        this.kel103Service = kel103Service;
        this.rfLabService = rfLabService;
    }

    public Task<IEndpointAttachmentSession> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return request.ConnectionDefinition switch
        {
            DesktopRuntimeHostKel103ConnectionDefinition
                when kel103Service is not null =>
                kel103Service.AttachAsync(request, cancellationToken),

            DesktopRuntimeHostRfLabConnectionDefinition
                when rfLabService is not null =>
                rfLabService.AttachAsync(request, cancellationToken),

            _ => throw new NotSupportedException(
                "No instrument attachment service is registered for "
                + "the requested connection definition.")
        };
    }
}

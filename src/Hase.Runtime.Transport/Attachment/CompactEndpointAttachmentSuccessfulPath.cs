using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Adapts compact endpoint bootstrap and operational resources to the shared
/// successful attachment lifecycle.
/// </summary>
internal sealed class CompactEndpointAttachmentSuccessfulPath
{
    private readonly EndpointAttachmentSuccessfulPath _successfulPath;

    /// <summary>
    /// Initializes the compact successful attachment adapter.
    /// </summary>
    internal CompactEndpointAttachmentSuccessfulPath(
        RuntimeContext runtimeContext)
    {
        _successfulPath =
            new EndpointAttachmentSuccessfulPath(
                runtimeContext
                ?? throw new ArgumentNullException(
                    nameof(runtimeContext)));
    }

    /// <summary>
    /// Completes attachment using one coherent compact operational resource
    /// graph.
    /// </summary>
    internal Task<EndpointAttachmentSession> CompleteAsync(
        EndpointAttachmentRequest request,
        CompactEndpointAttachmentBootstrapResult bootstrapResult,
        Func<
            RuntimeEndpoint,
            ICompactEndpointOperationalResources>
            createOperationalResources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            bootstrapResult);

        ArgumentNullException.ThrowIfNull(
            createOperationalResources);

        return _successfulPath.CompleteAsync(
            request,
            bootstrapResult.Descriptor,
            runtimeEndpoint =>
                createOperationalResources(
                    runtimeEndpoint),
            cancellationToken);
    }
}
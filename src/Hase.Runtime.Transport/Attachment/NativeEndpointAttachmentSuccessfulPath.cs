using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Adapts native endpoint bootstrap and operational resources to the shared
/// successful attachment lifecycle.
/// </summary>
internal sealed class NativeEndpointAttachmentSuccessfulPath
{
    private readonly EndpointAttachmentSuccessfulPath _successfulPath;

    /// <summary>
    /// Initializes the native successful attachment adapter.
    /// </summary>
    internal NativeEndpointAttachmentSuccessfulPath(
        RuntimeContext runtimeContext)
    {
        _successfulPath =
            new EndpointAttachmentSuccessfulPath(
                runtimeContext
                ?? throw new ArgumentNullException(
                    nameof(runtimeContext)));
    }

    /// <summary>
    /// Completes attachment using a supplied native supervision lifetime and
    /// ordered resources.
    /// </summary>
    internal async Task<EndpointAttachmentSession> CompleteAsync(
        EndpointAttachmentRequest request,
        NativeEndpointBootstrapResult bootstrapResult,
        Func<
            RuntimeEndpoint,
            EndpointConnectionSupervisionLifetime>
            createSupervisionLifetime,
        IEnumerable<IAsyncDisposable>
            resourcesAfterSupervision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            bootstrapResult);

        ArgumentNullException.ThrowIfNull(
            createSupervisionLifetime);

        ArgumentNullException.ThrowIfNull(
            resourcesAfterSupervision);

        IAsyncDisposable[] remainingResources =
            resourcesAfterSupervision.ToArray();

        if (remainingResources.Any(
                static resource =>
                    resource is null))
        {
            throw new ArgumentException(
                "The resource collection must not contain null.",
                nameof(resourcesAfterSupervision));
        }

        return await CompleteAsync(
            request,
            bootstrapResult,
            runtimeEndpoint =>
                new SuppliedOperationalResources(
                    createSupervisionLifetime(
                        runtimeEndpoint),
                    remainingResources),
            cancellationToken);
    }

    /// <summary>
    /// Completes attachment using one coherent native operational resource
    /// graph.
    /// </summary>
    internal Task<EndpointAttachmentSession> CompleteAsync(
        EndpointAttachmentRequest request,
        NativeEndpointBootstrapResult bootstrapResult,
        Func<
            RuntimeEndpoint,
            INativeEndpointOperationalResources>
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

    private sealed class SuppliedOperationalResources
        : INativeEndpointOperationalResources
    {
        internal SuppliedOperationalResources(
            EndpointConnectionSupervisionLifetime supervisionLifetime,
            IReadOnlyList<IAsyncDisposable> resourcesAfterSupervision)
        {
            SupervisionLifetime =
                supervisionLifetime
                ?? throw new InvalidOperationException(
                    "The supervision-lifetime factory returned null.");

            ResourcesAfterSupervision =
                resourcesAfterSupervision;
        }

        public EndpointConnectionSupervisionLifetime SupervisionLifetime
        {
            get;
        }

        public IReadOnlyList<IAsyncDisposable> ResourcesAfterSupervision
        {
            get;
        }
    }
}
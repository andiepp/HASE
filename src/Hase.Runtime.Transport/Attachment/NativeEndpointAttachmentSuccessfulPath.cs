using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Completes the successful post-bootstrap attachment path for one native
/// endpoint.
/// </summary>
internal sealed class NativeEndpointAttachmentSuccessfulPath
{
    private readonly RuntimeContext _runtimeContext;

    /// <summary>
    /// Initializes the successful attachment path.
    /// </summary>
    internal NativeEndpointAttachmentSuccessfulPath(
        RuntimeContext runtimeContext)
    {
        _runtimeContext =
            runtimeContext
            ?? throw new ArgumentNullException(
                nameof(runtimeContext));
    }

    /// <summary>
    /// Creates a staged endpoint, starts supervision, waits for initial
    /// readiness, publishes the endpoint, and returns its owning session.
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

        cancellationToken.ThrowIfCancellationRequested();

        RuntimeEndpoint runtimeEndpoint =
            _runtimeContext.CreateEndpoint(
                bootstrapResult.Descriptor);

        EndpointConnectionSupervisionLifetime supervisionLifetime =
            createSupervisionLifetime(
                runtimeEndpoint)
            ?? throw new InvalidOperationException(
                "The supervision-lifetime factory returned null.");

        Task supervisionTask =
            supervisionLifetime.RunAsync();

        await RuntimeEndpointInitialReadiness.WaitAsync(
            runtimeEndpoint,
            supervisionTask,
            cancellationToken);

        RuntimeEndpointPublication publication =
            RuntimeEndpointPublication.Publish(
                _runtimeContext,
                runtimeEndpoint);

        IAsyncDisposable[] ownedResources =
        [
            publication,
            supervisionLifetime,
            .. remainingResources
        ];

        return new EndpointAttachmentSession(
            request,
            runtimeEndpoint,
            ownedResources);
    }
}
using System.Runtime.ExceptionServices;
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

        EndpointConnectionSupervisionLifetime? supervisionLifetime =
            null;

        RuntimeEndpointPublication? publication =
            null;

        try
        {
            supervisionLifetime =
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

            publication =
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
        catch (Exception attachmentFailure)
        {
            List<Exception> cleanupFailures =
                await CleanupFailedAttachmentAsync(
                    publication,
                    supervisionLifetime,
                    remainingResources,
                    attachmentFailure);

            if (cleanupFailures.Count > 0)
            {
                throw new AggregateException(
                    "Endpoint attachment failed and one or more "
                    + "resources also failed during cleanup.",
                    [
                        attachmentFailure,
                        .. cleanupFailures
                    ]);
            }

            ExceptionDispatchInfo
                .Capture(
                    attachmentFailure)
                .Throw();

            throw;
        }
    }

    private static async Task<List<Exception>>
        CleanupFailedAttachmentAsync(
            RuntimeEndpointPublication? publication,
            EndpointConnectionSupervisionLifetime? supervisionLifetime,
            IReadOnlyList<IAsyncDisposable> remainingResources,
            Exception attachmentFailure)
    {
        var failures =
            new List<Exception>();

        if (publication is not null)
        {
            await DisposeFailedResourceAsync(
                publication,
                attachmentFailure,
                failures);
        }

        if (supervisionLifetime is not null)
        {
            await DisposeFailedResourceAsync(
                supervisionLifetime,
                attachmentFailure,
                failures);
        }

        foreach (
            IAsyncDisposable resource
            in remainingResources)
        {
            await DisposeFailedResourceAsync(
                resource,
                attachmentFailure,
                failures);
        }

        return failures;
    }

    private static async Task DisposeFailedResourceAsync(
        IAsyncDisposable resource,
        Exception attachmentFailure,
        ICollection<Exception> cleanupFailures)
    {
        try
        {
            await resource.DisposeAsync();
        }
        catch (Exception cleanupFailure)
            when (ReferenceEquals(
                cleanupFailure,
                attachmentFailure))
        {
        }
        catch (Exception cleanupFailure)
        {
            cleanupFailures.Add(
                cleanupFailure);
        }
    }
}
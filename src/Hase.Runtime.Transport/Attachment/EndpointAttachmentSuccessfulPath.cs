using System.Runtime.ExceptionServices;
using Hase.Core.Domain.Endpoints;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Completes the transport-independent successful attachment lifecycle after
/// authoritative bootstrap and descriptor resolution.
/// </summary>
internal sealed class EndpointAttachmentSuccessfulPath
{
    private readonly RuntimeContext _runtimeContext;

    /// <summary>
    /// Initializes the shared successful attachment path.
    /// </summary>
    internal EndpointAttachmentSuccessfulPath(
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
        EndpointDescriptor descriptor,
        Func<
            RuntimeEndpoint,
            IEndpointOperationalResources>
            createOperationalResources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            descriptor);

        ArgumentNullException.ThrowIfNull(
            createOperationalResources);

        cancellationToken.ThrowIfCancellationRequested();

        RuntimeEndpoint runtimeEndpoint =
            _runtimeContext.CreateEndpoint(
                descriptor);

        IEndpointOperationalResources? operationalResources =
            null;

        RuntimeEndpointPublication? publication =
            null;

        try
        {
            operationalResources =
                createOperationalResources(
                    runtimeEndpoint)
                ?? throw new InvalidOperationException(
                    "The operational-resource factory returned null.");

            Task supervisionTask =
                operationalResources
                    .SupervisionLifetime
                    .RunAsync();

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
                operationalResources.SupervisionLifetime,
                .. operationalResources.ResourcesAfterSupervision
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
                    operationalResources?.SupervisionLifetime,
                    operationalResources?.ResourcesAfterSupervision
                        ?? Array.Empty<IAsyncDisposable>(),
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
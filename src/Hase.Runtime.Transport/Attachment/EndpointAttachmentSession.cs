using System.Runtime.ExceptionServices;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Owns the ordered asynchronous resources of one attached runtime endpoint.
/// </summary>
public sealed class EndpointAttachmentSession
    : IEndpointAttachmentSession
{
    private readonly IReadOnlyList<IAsyncDisposable>
        _ownedResourcesInShutdownOrder;

    private readonly object _syncRoot =
        new();

    private Task? _shutdownTask;

    /// <summary>
    /// Initializes an endpoint attachment session.
    /// </summary>
    /// <param name="request">
    /// The explicit request that created the attachment.
    /// </param>
    /// <param name="runtimeEndpoint">
    /// The attached and initially synchronized runtime endpoint.
    /// </param>
    /// <param name="ownedResourcesInShutdownOrder">
    /// Resources owned by the session in the exact order in which they
    /// must be shut down.
    /// </param>
    public EndpointAttachmentSession(
        EndpointAttachmentRequest request,
        RuntimeEndpoint runtimeEndpoint,
        IEnumerable<IAsyncDisposable>
            ownedResourcesInShutdownOrder)
    {
        Request =
            request
            ?? throw new ArgumentNullException(
                nameof(request));

        RuntimeEndpoint =
            runtimeEndpoint
            ?? throw new ArgumentNullException(
                nameof(runtimeEndpoint));

        ArgumentNullException.ThrowIfNull(
            ownedResourcesInShutdownOrder);

        IAsyncDisposable[] ownedResources =
            ownedResourcesInShutdownOrder.ToArray();

        if (ownedResources.Any(
                static resource =>
                    resource is null))
        {
            throw new ArgumentException(
                "The owned resource collection must not contain null.",
                nameof(ownedResourcesInShutdownOrder));
        }

        _ownedResourcesInShutdownOrder =
            ownedResources;
    }

    /// <inheritdoc />
    public EndpointAttachmentRequest Request
    {
        get;
    }

    /// <inheritdoc />
    public RuntimeEndpoint RuntimeEndpoint
    {
        get;
    }

    /// <inheritdoc />
    public Task ShutdownAsync(
        CancellationToken cancellationToken = default)
    {
        Task shutdownTask =
            GetOrCreateShutdownTask();

        return shutdownTask.WaitAsync(
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await GetOrCreateShutdownTask();
    }

    private Task GetOrCreateShutdownTask()
    {
        lock (_syncRoot)
        {
            _shutdownTask ??=
                ShutdownCoreAsync();

            return _shutdownTask;
        }
    }

    private async Task ShutdownCoreAsync()
    {
        await Task.Yield();

        List<Exception>? failures =
            null;

        foreach (
            IAsyncDisposable resource
            in _ownedResourcesInShutdownOrder)
        {
            try
            {
                await resource.DisposeAsync();
            }
            catch (Exception exception)
            {
                failures ??=
                    [];

                failures.Add(
                    exception);
            }
        }

        if (failures is null)
        {
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo
                .Capture(
                    failures[0])
                .Throw();
        }

        throw new AggregateException(
            "Multiple endpoint attachment resources failed "
            + "during shutdown.",
            failures);
    }
}
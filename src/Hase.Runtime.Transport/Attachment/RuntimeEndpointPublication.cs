using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Owns publication of one staged runtime endpoint in its runtime context.
/// </summary>
internal sealed class RuntimeEndpointPublication
    : IAsyncDisposable
{
    private int _disposed;

    private RuntimeEndpointPublication(
        RuntimeContext context,
        RuntimeEndpoint endpoint)
    {
        Context =
            context;

        Endpoint =
            endpoint;
    }

    /// <summary>
    /// Gets the context containing the published endpoint.
    /// </summary>
    internal RuntimeContext Context
    {
        get;
    }

    /// <summary>
    /// Gets the endpoint whose publication is owned.
    /// </summary>
    internal RuntimeEndpoint Endpoint
    {
        get;
    }

    /// <summary>
    /// Publishes a staged endpoint and returns its publication owner.
    /// </summary>
    internal static RuntimeEndpointPublication Publish(
        RuntimeContext context,
        RuntimeEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(
            context);

        ArgumentNullException.ThrowIfNull(
            endpoint);

        RuntimeEndpoint publishedEndpoint =
            context.PublishEndpoint(
                endpoint);

        return new RuntimeEndpointPublication(
            context,
            publishedEndpoint);
    }

    /// <summary>
    /// Removes the endpoint from the runtime context.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1)
            != 0)
        {
            return ValueTask.CompletedTask;
        }

        Context.RemoveEndpoint(
            Endpoint);

        return ValueTask.CompletedTask;
    }
}
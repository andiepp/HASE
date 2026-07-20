namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Exposes one coherent operational resource graph for a native endpoint.
/// </summary>
internal interface INativeEndpointOperationalResources
{
    /// <summary>
    /// Gets the lifetime that starts and stops connection supervision.
    /// </summary>
    EndpointConnectionSupervisionLifetime SupervisionLifetime
    {
        get;
    }

    /// <summary>
    /// Gets resources in their required order after supervision has stopped.
    /// </summary>
    IReadOnlyList<IAsyncDisposable> ResourcesAfterSupervision
    {
        get;
    }
}
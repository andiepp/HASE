namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Exposes the transport-independent ownership shape of one endpoint's
/// operational connection resources.
/// </summary>
internal interface IEndpointOperationalResources
{
    /// <summary>
    /// Gets the lifetime that starts and stops endpoint connection
    /// supervision.
    /// </summary>
    EndpointConnectionSupervisionLifetime SupervisionLifetime
    {
        get;
    }

    /// <summary>
    /// Gets resources in their required disposal order after supervision has
    /// stopped.
    /// </summary>
    IReadOnlyList<IAsyncDisposable> ResourcesAfterSupervision
    {
        get;
    }
}
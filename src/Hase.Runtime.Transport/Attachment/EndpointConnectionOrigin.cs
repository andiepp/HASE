namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Identifies how an endpoint connection definition was obtained.
/// </summary>
public enum EndpointConnectionOrigin
{
    /// <summary>
    /// The connection target was obtained from a verified discovery result.
    /// </summary>
    Discovered = 0,

    /// <summary>
    /// The connection target was supplied through explicit configuration.
    /// </summary>
    Configured = 1
}
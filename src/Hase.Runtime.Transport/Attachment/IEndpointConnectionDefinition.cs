using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Describes a transport-specific endpoint connection definition
/// accepted by the runtime-host attachment lifecycle.
/// </summary>
/// <remarks>
/// Implementations contain transport-specific reachability information.
/// Connection information is not authoritative endpoint identity.
/// </remarks>
public interface IEndpointConnectionDefinition
{
    /// <summary>
    /// Gets how this connection definition was obtained.
    /// </summary>
    EndpointConnectionOrigin Origin
    {
        get;
    }

    /// <summary>
    /// Gets the endpoint identity expected during attachment verification,
    /// or <see langword="null"/> when the definition does not constrain it.
    /// </summary>
    EndpointId? ExpectedEndpointId
    {
        get;
    }
}
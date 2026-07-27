using Hase.Core.Domain.Identity;

namespace Hase.Client;

/// <summary>
/// Identifies one logical runtime host in the normalized client model.
/// </summary>
/// <remarks>
/// The identity is independent of network address, machine name, client
/// credential, endpoint identity, and attachment generation.
/// </remarks>
public sealed record RemoteRuntimeHostId
    : HaseId
{
    /// <summary>
    /// Initializes one stable runtime-host client identity.
    /// </summary>
    public RemoteRuntimeHostId(
        string value)
        : base(
            value)
    {
    }
}

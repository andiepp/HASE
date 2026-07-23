using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Identifies one logical HASE runtime-host installation.
/// </summary>
/// <remarks>
/// Runtime-host identity is independent of machine names, network addresses,
/// Tailscale node identity, northbound listening addresses, attached endpoints,
/// and endpoint attachment generations. It is identification, not an
/// authentication credential.
/// </remarks>
public sealed record RuntimeHostId
    : HaseId
{
    /// <summary>
    /// Initializes a stable runtime-host identity.
    /// </summary>
    public RuntimeHostId(
        string value)
        : base(
            value)
    {
    }
}
using Grpc.Core;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Provides one explicitly configured principal for the enforced trusted
/// loopback development profile.
/// </summary>
public sealed class TrustedLoopbackRuntimeHostClientPrincipalProvider
    : IRuntimeHostClientPrincipalProvider
{
    private readonly RuntimeHostClientPrincipal principal;

    /// <summary>
    /// Initializes the trusted-loopback principal provider.
    /// </summary>
    public TrustedLoopbackRuntimeHostClientPrincipalProvider(
        RuntimeHostClientPrincipal principal)
    {
        this.principal =
            principal
            ?? throw new ArgumentNullException(
                nameof(principal));
    }

    /// <inheritdoc />
    public RuntimeHostClientPrincipal GetPrincipal(
        ServerCallContext? context)
    {
        return principal;
    }
}

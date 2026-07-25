using Grpc.Core;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Provides the authenticated HASE client principal for one remote call.
/// </summary>
public interface IRuntimeHostClientPrincipalProvider
{
    /// <summary>
    /// Gets the authenticated client principal for the supplied call context.
    /// </summary>
    RuntimeHostClientPrincipal GetPrincipal(
        ServerCallContext? context);
}

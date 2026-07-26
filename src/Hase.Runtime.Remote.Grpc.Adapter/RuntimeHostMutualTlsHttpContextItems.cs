namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Defines the HTTP-context item keys used by runtime-host mutual-TLS
/// authentication middleware.
/// </summary>
public static class RuntimeHostMutualTlsHttpContextItems
{
    /// <summary>
    /// Gets the key under which the request authentication result is stored.
    /// </summary>
    public static readonly object AuthenticationResult =
        new();
}

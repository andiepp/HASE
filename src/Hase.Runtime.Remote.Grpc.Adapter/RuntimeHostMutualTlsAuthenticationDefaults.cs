namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Defines the authentication scheme used by the runtime-host mutual-TLS
/// identity projection.
/// </summary>
public static class RuntimeHostMutualTlsAuthenticationDefaults
{
    /// <summary>
    /// Gets the stable ASP.NET Core authentication scheme name.
    /// </summary>
    public const string AuthenticationScheme =
        "HASE.MutualTls";
}

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Contains the Kestrel transport policy derived from one enabled runtime-host
/// mutual-TLS configuration.
/// </summary>
public sealed class RuntimeHostMutualTlsKestrelConfiguration
{
    /// <summary>
    /// Initializes one Kestrel mutual-TLS transport policy.
    /// </summary>
    public RuntimeHostMutualTlsKestrelConfiguration(
        HttpProtocols protocols,
        HttpsConnectionAdapterOptions httpsOptions)
    {
        if (protocols != HttpProtocols.Http2)
        {
            throw new ArgumentException(
                "The remote runtime-host listener must use HTTP/2 only.",
                nameof(protocols));
        }

        Protocols = protocols;
        HttpsOptions =
            httpsOptions
            ?? throw new ArgumentNullException(
                nameof(httpsOptions));
    }

    /// <summary>
    /// Gets the only HTTP protocol accepted by the remote runtime-host
    /// listener.
    /// </summary>
    public HttpProtocols Protocols { get; }

    /// <summary>
    /// Gets the HTTPS options applied to the Kestrel listener.
    /// </summary>
    public HttpsConnectionAdapterOptions HttpsOptions { get; }
}

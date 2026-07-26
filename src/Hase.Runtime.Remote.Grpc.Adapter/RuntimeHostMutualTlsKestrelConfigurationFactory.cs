using System.Security.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps an enabled runtime-host mutual-TLS configuration to the corresponding
/// fail-closed Kestrel HTTPS and HTTP/2 policy.
/// </summary>
public static class RuntimeHostMutualTlsKestrelConfigurationFactory
{
    /// <summary>
    /// Creates the Kestrel policy for one enabled mutual-TLS listener.
    /// </summary>
    public static RuntimeHostMutualTlsKestrelConfiguration Create(
        RuntimeHostMutualTlsOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        if (!options.Enabled)
        {
            throw new InvalidOperationException(
                "A Kestrel mutual-TLS policy cannot be created from disabled "
                + "runtime-host options.");
        }

        if (!options.RequireClientCertificate)
        {
            throw new InvalidOperationException(
                "The runtime-host mutual-TLS configuration must require a "
                + "client certificate.");
        }

        HttpsConnectionAdapterOptions httpsOptions =
            new()
            {
                ServerCertificate =
                    options.ServerCertificate
                    ?? throw new InvalidOperationException(
                        "The enabled runtime-host mutual-TLS configuration "
                        + "does not contain a server certificate."),
                ClientCertificateMode =
                    ClientCertificateMode.RequireCertificate,
                SslProtocols =
                    SslProtocols.Tls12
                    | SslProtocols.Tls13
            };

        return new RuntimeHostMutualTlsKestrelConfiguration(
            HttpProtocols.Http2,
            httpsOptions);
    }
}

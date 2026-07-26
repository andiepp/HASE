using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Defines the mutual-TLS transport configuration for one remote runtime-host
/// listener.
/// </summary>
public sealed class RuntimeHostMutualTlsOptions
{
    /// <summary>
    /// Initializes one mutual-TLS runtime-host configuration.
    /// </summary>
    public RuntimeHostMutualTlsOptions(
        bool enabled,
        X509Certificate2? serverCertificate,
        bool requireClientCertificate)
    {
        if (enabled && serverCertificate is null)
        {
            throw new ArgumentException(
                "An enabled mutual-TLS listener requires a server certificate.",
                nameof(serverCertificate));
        }

        if (enabled && !requireClientCertificate)
        {
            throw new ArgumentException(
                "An enabled mutual-TLS listener must require a client certificate.",
                nameof(requireClientCertificate));
        }

        if (!enabled && serverCertificate is not null)
        {
            throw new ArgumentException(
                "A disabled mutual-TLS listener must not contain a server certificate.",
                nameof(serverCertificate));
        }

        if (!enabled && requireClientCertificate)
        {
            throw new ArgumentException(
                "A disabled mutual-TLS listener must not require a client certificate.",
                nameof(requireClientCertificate));
        }

        Enabled = enabled;
        ServerCertificate = serverCertificate;
        RequireClientCertificate = requireClientCertificate;
    }

    /// <summary>
    /// Gets a value indicating whether the mutual-TLS listener is enabled.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Gets the server certificate presented by the runtime host.
    /// </summary>
    public X509Certificate2? ServerCertificate { get; }

    /// <summary>
    /// Gets a value indicating whether the listener requires a client
    /// certificate during the TLS handshake.
    /// </summary>
    public bool RequireClientCertificate { get; }

    /// <summary>
    /// Creates the explicitly disabled mutual-TLS configuration.
    /// </summary>
    public static RuntimeHostMutualTlsOptions Disabled()
    {
        return new RuntimeHostMutualTlsOptions(
            false,
            null,
            false);
    }

    /// <summary>
    /// Creates an enabled mutual-TLS configuration that requires a client
    /// certificate.
    /// </summary>
    public static RuntimeHostMutualTlsOptions EnabledWith(
        X509Certificate2 serverCertificate)
    {
        ArgumentNullException.ThrowIfNull(
            serverCertificate);

        return new RuntimeHostMutualTlsOptions(
            true,
            serverCertificate,
            true);
    }
}

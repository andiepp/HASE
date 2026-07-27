using System.Net;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Defines the complete external configuration references required by one
/// private-network runtime-host client.
/// </summary>
public sealed record RuntimeHostPrivateNetworkClientOptions
{
    /// <summary>
    /// Initializes one private-network client configuration.
    /// </summary>
    public RuntimeHostPrivateNetworkClientOptions(
        Uri address,
        RuntimeHostCertificateStoreReference clientCertificate,
        RuntimeHostCertificateStoreReference trustedServerCertificate)
    {
        ArgumentNullException.ThrowIfNull(
            address);
        ArgumentNullException.ThrowIfNull(
            clientCertificate);
        ArgumentNullException.ThrowIfNull(
            trustedServerCertificate);

        if (!address.IsAbsoluteUri
            || !string.Equals(
                address.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The private-network runtime-host address must be an "
                + "absolute HTTPS URI.",
                nameof(address));
        }

        if (!IPAddress.TryParse(
                address.Host,
                out _))
        {
            throw new ArgumentException(
                "The private-network runtime-host address must use an "
                + "explicit IP address.",
                nameof(address));
        }

        if (!string.IsNullOrEmpty(
                address.UserInfo)
            || address.AbsolutePath != "/"
            || !string.IsNullOrEmpty(
                address.Query)
            || !string.IsNullOrEmpty(
                address.Fragment))
        {
            throw new ArgumentException(
                "The private-network runtime-host address must not contain "
                + "user information, a path, a query, or a fragment.",
                nameof(address));
        }

        Address = address;
        ClientCertificate = clientCertificate;
        TrustedServerCertificate = trustedServerCertificate;
    }

    /// <summary>
    /// Gets the explicit HTTPS runtime-host address.
    /// </summary>
    public Uri Address
    {
        get;
    }

    /// <summary>
    /// Gets the external store reference for the client certificate and
    /// private key.
    /// </summary>
    public RuntimeHostCertificateStoreReference ClientCertificate
    {
        get;
    }

    /// <summary>
    /// Gets the external store reference for the pinned server certificate.
    /// </summary>
    public RuntimeHostCertificateStoreReference TrustedServerCertificate
    {
        get;
    }
}

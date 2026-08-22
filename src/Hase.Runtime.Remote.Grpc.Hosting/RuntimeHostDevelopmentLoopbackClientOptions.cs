using System.Net;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Defines the explicitly labeled certificate-free loopback development
/// client configuration. The address must target one loopback IP over plain
/// HTTP; every non-loopback runtime host requires the secured private-network
/// client configuration instead.
/// </summary>
public sealed record RuntimeHostDevelopmentLoopbackClientOptions
{
    /// <summary>
    /// Initializes one development loopback client configuration.
    /// </summary>
    public RuntimeHostDevelopmentLoopbackClientOptions(
        Uri address)
    {
        ArgumentNullException.ThrowIfNull(
            address);

        if (!address.IsAbsoluteUri
            || !string.Equals(
                address.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The development loopback runtime-host address must be an "
                + "absolute HTTP URI.",
                nameof(address));
        }

        if (!IPAddress.TryParse(
                address.Host,
                out IPAddress? host))
        {
            throw new ArgumentException(
                "The development loopback runtime-host address must use an "
                + "explicit IP address.",
                nameof(address));
        }

        if (!IPAddress.IsLoopback(
                host))
        {
            throw new ArgumentException(
                "The development client configuration is loopback-only and "
                + "refuses every non-loopback address. A non-loopback runtime "
                + "host requires the secured private-network client "
                + "configuration.",
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
                "The development loopback runtime-host address must not "
                + "contain user information, a path, a query, or a fragment.",
                nameof(address));
        }

        Address = address;
    }

    /// <summary>
    /// Gets the explicit HTTP loopback runtime-host address.
    /// </summary>
    public Uri Address
    {
        get;
    }
}

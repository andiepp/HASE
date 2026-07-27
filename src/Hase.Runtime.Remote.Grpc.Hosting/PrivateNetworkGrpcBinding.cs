using System.Net;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Represents one explicitly configured private-network gRPC listener binding.
/// </summary>
public sealed record PrivateNetworkGrpcBinding
{
    /// <summary>
    /// Initializes one explicit private-network binding.
    /// </summary>
    public PrivateNetworkGrpcBinding(
        IPAddress address,
        int port)
    {
        ArgumentNullException.ThrowIfNull(
            address);

        if (IPAddress.IsLoopback(
                address))
        {
            throw new ArgumentException(
                "The private-network gRPC host address must not be a "
                + "loopback address.",
                nameof(address));
        }

        if (address.Equals(
                IPAddress.Any)
            || address.Equals(
                IPAddress.IPv6Any))
        {
            throw new ArgumentException(
                "The private-network gRPC host address must not be a "
                + "wildcard address.",
                nameof(address));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                port,
                "The private-network gRPC host port must be between 1 "
                + "and 65535.");
        }

        Address =
            address;

        Port =
            port;
    }

    /// <summary>
    /// Gets the explicitly configured IPv4 or IPv6 listener address.
    /// </summary>
    public IPAddress Address
    {
        get;
    }

    /// <summary>
    /// Gets the fixed listener port.
    /// </summary>
    public int Port
    {
        get;
    }
}

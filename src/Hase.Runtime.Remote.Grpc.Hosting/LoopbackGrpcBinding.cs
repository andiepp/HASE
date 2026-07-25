using System.Net;

namespace Hase.Runtime.Remote.Grpc.Hosting;

/// <summary>
/// Represents one validated loopback-only gRPC listener binding.
/// </summary>
public sealed record LoopbackGrpcBinding
{
    /// <summary>
    /// Initializes a loopback-only binding.
    /// </summary>
    public LoopbackGrpcBinding(
        IPAddress address,
        int port)
    {
        ArgumentNullException.ThrowIfNull(
            address);

        if (!IPAddress.IsLoopback(
                address))
        {
            throw new ArgumentException(
                "The gRPC host address must be a loopback address.",
                nameof(address));
        }

        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                port,
                "The gRPC host port must be between 0 and 65535.");
        }

        Address =
            address;

        Port =
            port;
    }

    /// <summary>
    /// Gets the validated IPv4 or IPv6 loopback address.
    /// </summary>
    public IPAddress Address
    {
        get;
    }

    /// <summary>
    /// Gets the listener port. Zero requests an automatically selected port.
    /// </summary>
    public int Port
    {
        get;
    }
}

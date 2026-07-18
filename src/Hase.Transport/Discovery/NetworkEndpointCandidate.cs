using System.Net;

namespace Hase.Transport.Discovery;

/// <summary>
/// Represents one network endpoint advertised as a possible HASE
/// Protocol Version 1 TCP endpoint.
/// </summary>
/// <remarks>
/// The service instance name is descriptive discovery information.
/// It is not an authoritative HASE endpoint identity.
///
/// Candidate identity is defined by the resolved address and port.
/// </remarks>
public sealed class NetworkEndpointCandidate
    : IEquatable<NetworkEndpointCandidate>
{
    /// <summary>
    /// Initializes a new network endpoint candidate.
    /// </summary>
    /// <param name="serviceInstanceName">
    /// The DNS-SD service instance name.
    /// </param>
    /// <param name="address">
    /// The resolved network address.
    /// </param>
    /// <param name="port">
    /// The advertised TCP port.
    /// </param>
    public NetworkEndpointCandidate(
        string serviceInstanceName,
        IPAddress address,
        int port)
    {
        if (string.IsNullOrWhiteSpace(
            serviceInstanceName))
        {
            throw new ArgumentException(
                "The service instance name must not be empty.",
                nameof(serviceInstanceName));
        }

        ArgumentNullException.ThrowIfNull(
            address);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                port,
                "The TCP port must be between 1 and 65535.");
        }

        ServiceInstanceName =
            serviceInstanceName;

        Address =
            address;

        Port =
            port;
    }

    /// <summary>
    /// Gets the DNS-SD service instance name.
    /// </summary>
    public string ServiceInstanceName
    {
        get;
    }

    /// <summary>
    /// Gets the resolved network address.
    /// </summary>
    public IPAddress Address
    {
        get;
    }

    /// <summary>
    /// Gets the advertised TCP port.
    /// </summary>
    public int Port
    {
        get;
    }

    /// <inheritdoc />
    public bool Equals(
        NetworkEndpointCandidate? other)
    {
        return other is not null
            && Address.Equals(
                other.Address)
            && Port == other.Port;
    }

    /// <inheritdoc />
    public override bool Equals(
        object? obj)
    {
        return Equals(
            obj as NetworkEndpointCandidate);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            Address,
            Port);
    }
}
namespace Hase.Transport.Tcp;

/// <summary>
/// Defines the remote endpoint used by a TCP transport connection.
/// </summary>
public sealed class TcpTransportOptions
{
    /// <summary>
    /// Initializes TCP transport options.
    /// </summary>
    /// <param name="host">
    /// IPv4 address, IPv6 address, or DNS host name of the remote endpoint.
    /// </param>
    /// <param name="port">
    /// TCP port of the remote endpoint.
    /// </param>
    public TcpTransportOptions(
        string host,
        int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            host);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                port,
                "The TCP port must be between 1 and 65535.");
        }

        Host =
            host;

        Port =
            port;
    }

    /// <summary>
    /// Gets the IPv4 address, IPv6 address, or DNS host name
    /// of the remote endpoint.
    /// </summary>
    public string Host
    {
        get;
    }

    /// <summary>
    /// Gets the TCP port of the remote endpoint.
    /// </summary>
    public int Port
    {
        get;
    }
}
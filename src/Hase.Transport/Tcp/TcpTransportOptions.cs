namespace Hase.Transport.Tcp;

/// <summary>
/// Defines the remote endpoint and connection-establishment behavior used by
/// a TCP transport connection.
/// </summary>
public sealed class TcpTransportOptions
{
    /// <summary>
    /// Gets the default TCP connection-attempt timeout.
    /// </summary>
    public static TimeSpan DefaultConnectionTimeout
    {
        get;
    } =
        TimeSpan.FromSeconds(
            5);

    /// <summary>
    /// Initializes TCP transport options using the default connection timeout.
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
        : this(
            host,
            port,
            DefaultConnectionTimeout)
    {
    }

    /// <summary>
    /// Initializes TCP transport options.
    /// </summary>
    /// <param name="host">
    /// IPv4 address, IPv6 address, or DNS host name of the remote endpoint.
    /// </param>
    /// <param name="port">
    /// TCP port of the remote endpoint.
    /// </param>
    /// <param name="connectionTimeout">
    /// Maximum duration of one TCP connection attempt.
    /// <see cref="Timeout.InfiniteTimeSpan"/> preserves the operating system's
    /// connection-attempt timeout behavior.
    /// </param>
    public TcpTransportOptions(
        string host,
        int port,
        TimeSpan connectionTimeout)
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

        if (connectionTimeout != Timeout.InfiniteTimeSpan
            && connectionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(connectionTimeout),
                connectionTimeout,
                "The TCP connection timeout must be positive or "
                + "Timeout.InfiniteTimeSpan.");
        }

        Host =
            host;

        Port =
            port;

        ConnectionTimeout =
            connectionTimeout;
    }

    /// <summary>
    /// Gets the IPv4 address, IPv6 address, or DNS host name of the remote
    /// endpoint.
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

    /// <summary>
    /// Gets the maximum duration of one TCP connection attempt.
    /// </summary>
    public TimeSpan ConnectionTimeout
    {
        get;
    }
}
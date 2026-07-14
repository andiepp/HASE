namespace Hase.Transport;

/// <summary>
/// Creates transport connections.
///
/// A transport factory encapsulates the configuration and
/// establishment of a transport-specific connection.
/// </summary>
public interface ITransportFactory
{
    /// <summary>
    /// Establishes a transport connection.
    /// </summary>
    Task<ITransportConnection> ConnectAsync(
        CancellationToken cancellationToken = default);
}
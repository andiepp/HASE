using System.Net.Sockets;

namespace Hase.Transport.Tcp;

/// <summary>
/// Establishes framed TCP transport connections.
/// </summary>
public sealed class TcpTransportFactory
    : ITransportFactory
{
    private readonly TcpTransportOptions _options;
    private readonly int _maximumPayloadLength;

    /// <summary>
    /// Initializes a TCP transport factory.
    /// </summary>
    /// <param name="options">
    /// Remote TCP endpoint configuration.
    /// </param>
    /// <param name="maximumPayloadLength">
    /// Maximum accepted response payload length in bytes.
    /// </param>
    public TcpTransportFactory(
        TcpTransportOptions options,
        int maximumPayloadLength)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        if (maximumPayloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadLength),
                maximumPayloadLength,
                "The maximum payload length must not be negative.");
        }

        _options =
            options;

        _maximumPayloadLength =
            maximumPayloadLength;
    }

    /// <inheritdoc />
    public async Task<ITransportConnection> ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client =
            new TcpClient();

        try
        {
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                cancellationToken);

            return new TcpTransportConnection(
                client,
                _maximumPayloadLength);
        }
        catch
        {
            client.Dispose();

            throw;
        }
    }
}
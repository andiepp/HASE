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
    private readonly ITcpClientConnector _connector;

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
        : this(
            options,
            maximumPayloadLength,
            new DefaultTcpClientConnector())
    {
    }

    internal TcpTransportFactory(
        TcpTransportOptions options,
        int maximumPayloadLength,
        ITcpClientConnector connector)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        ArgumentNullException.ThrowIfNull(
            connector);

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

        _connector =
            connector;
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
            await ConnectClientAsync(
                client,
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

    private async Task ConnectClientAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        if (_options.ConnectionTimeout
            == Timeout.InfiniteTimeSpan)
        {
            await _connector.ConnectAsync(
                client,
                _options.Host,
                _options.Port,
                cancellationToken);

            return;
        }

        using var timeoutCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCancellationTokenSource.CancelAfter(
            _options.ConnectionTimeout);

        try
        {
            await _connector.ConnectAsync(
                client,
                _options.Host,
                _options.Port,
                timeoutCancellationTokenSource.Token);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested
                  && timeoutCancellationTokenSource
                      .IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The TCP connection attempt to "
                + $"'{_options.Host}:{_options.Port}' did not complete "
                + $"within {_options.ConnectionTimeout}.",
                exception);
        }
    }

    private sealed class DefaultTcpClientConnector
        : ITcpClientConnector
    {
        public ValueTask ConnectAsync(
            TcpClient client,
            string host,
            int port,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                client);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                host);

            cancellationToken.ThrowIfCancellationRequested();

            return client.ConnectAsync(
                host,
                port,
                cancellationToken);
        }
    }
}
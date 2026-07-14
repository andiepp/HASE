using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

/// <summary>
/// Provides a single-connection framed TCP endpoint for transport tests.
/// </summary>
public sealed class FramedTcpTestServer
    : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly int _maximumPayloadLength;
    private bool _disposed;

    public FramedTcpTestServer(
        int maximumPayloadLength = 1024 * 1024)
    {
        if (maximumPayloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadLength),
                maximumPayloadLength,
                "The maximum payload length must not be negative.");
        }

        _maximumPayloadLength =
            maximumPayloadLength;

        _listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        _listener.Start();

        Port =
            ((IPEndPoint)_listener.LocalEndpoint)
            .Port;
    }

    /// <summary>
    /// Gets the automatically allocated loopback TCP port.
    /// </summary>
    public int Port
    {
        get;
    }

    /// <summary>
    /// Accepts one client, reads one frame, and sends one response frame.
    /// </summary>
    public async Task ServeSingleExchangeAsync(
        Func<
            byte[],
            CancellationToken,
            Task<byte[]>> exchangeHandler,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        ArgumentNullException.ThrowIfNull(
            exchangeHandler);

        using TcpClient client =
            await _listener.AcceptTcpClientAsync(
                cancellationToken);

        await using NetworkStream stream =
            client.GetStream();

        byte[] request =
            await TcpFrameReader.ReadAsync(
                stream,
                _maximumPayloadLength,
                cancellationToken);

        byte[] response =
            await exchangeHandler(
                request,
                cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException(
                "The framed TCP test-server handler returned a null response.");
        }

        byte[] responseFrame =
            TcpFrameCodec.Encode(
                response);

        await stream.WriteAsync(
            responseFrame.AsMemory(),
            cancellationToken);

        await stream.FlushAsync(
            cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed =
            true;

        _listener.Stop();

        return ValueTask.CompletedTask;
    }
}
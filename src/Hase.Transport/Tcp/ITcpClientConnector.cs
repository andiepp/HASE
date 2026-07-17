using System.Net.Sockets;

namespace Hase.Transport.Tcp;

/// <summary>
/// Establishes the socket connection owned by a TCP client.
/// </summary>
internal interface ITcpClientConnector
{
    ValueTask ConnectAsync(
        TcpClient client,
        string host,
        int port,
        CancellationToken cancellationToken);
}
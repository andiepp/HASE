using Hase.Transport;
using Hase.Transport.Tcp;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Bootstraps a native HASE endpoint through a temporary framed-TCP
/// connection.
/// </summary>
public sealed class TcpNativeEndpointBootstrapClient
{
    /// <summary>
    /// Gets the default maximum accepted protocol payload length.
    /// </summary>
    public const int DefaultMaximumPayloadLength =
        4096;

    private readonly INativeEndpointBootstrapper _bootstrapper;

    private readonly Func<
        NetworkEndpointConnectionDefinition,
        CancellationToken,
        Task<ITransportConnection>> _connectAsync;

    /// <summary>
    /// Initializes a temporary TCP bootstrap client.
    /// </summary>
    public TcpNativeEndpointBootstrapClient(
        INativeEndpointBootstrapper bootstrapper,
        int maximumPayloadLength =
            DefaultMaximumPayloadLength)
        : this(
            bootstrapper,
            CreateConnectionFactory(
                maximumPayloadLength))
    {
    }

    internal TcpNativeEndpointBootstrapClient(
        INativeEndpointBootstrapper bootstrapper,
        Func<
            NetworkEndpointConnectionDefinition,
            CancellationToken,
            Task<ITransportConnection>> connectAsync)
    {
        _bootstrapper =
            bootstrapper
            ?? throw new ArgumentNullException(
                nameof(bootstrapper));

        _connectAsync =
            connectAsync
            ?? throw new ArgumentNullException(
                nameof(connectAsync));
    }

    /// <summary>
    /// Creates a temporary TCP connection, bootstraps the endpoint, and
    /// closes the temporary connection.
    /// </summary>
    public async Task<NativeEndpointBootstrapResult> BootstrapAsync(
        NetworkEndpointConnectionDefinition connectionDefinition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            connectionDefinition);

        cancellationToken.ThrowIfCancellationRequested();

        ITransportConnection connection =
            await _connectAsync(
                connectionDefinition,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The TCP bootstrap connection factory returned null.");

        try
        {
            var protocolConnection =
                new LegacyRuntimeProtocolConnection(
                    connection);

            return await _bootstrapper.BootstrapAsync(
                protocolConnection,
                connectionDefinition.ExpectedEndpointId,
                cancellationToken);
        }
        finally
        {
            await DisposeConnectionAsync(
                connection);
        }
    }

    private static Func<
        NetworkEndpointConnectionDefinition,
        CancellationToken,
        Task<ITransportConnection>> CreateConnectionFactory(
            int maximumPayloadLength)
    {
        if (maximumPayloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadLength),
                maximumPayloadLength,
                "The maximum payload length must not be negative.");
        }

        return (
            connectionDefinition,
            cancellationToken) =>
        {
            ITransportFactory factory =
                new TcpTransportFactory(
                    connectionDefinition.TransportOptions,
                    maximumPayloadLength);

            return factory.ConnectAsync(
                cancellationToken);
        };
    }

    private static async ValueTask DisposeConnectionAsync(
        ITransportConnection connection)
    {
        if (connection
            is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();

            return;
        }

        if (connection
            is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
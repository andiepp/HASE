namespace Hase.Transport;

/// <summary>
/// Owns the current transport connection created by a transport factory.
/// </summary>
/// <remarks>
/// The manager provides explicit connection creation and replacement.
/// It does not perform automatic reconnect or retry.
/// </remarks>
public sealed class TransportConnectionManager
    : IAsyncDisposable
{
    private readonly ITransportFactory _factory;

    private readonly SemaphoreSlim _operationLock =
        new(
            initialCount: 1,
            maxCount: 1);

    private ITransportConnection? _currentConnection;

    private bool _disposed;

    /// <summary>
    /// Initializes a transport connection manager.
    /// </summary>
    /// <param name="factory">
    /// Factory used to create transport connections.
    /// </param>
    public TransportConnectionManager(
        ITransportFactory factory)
    {
        _factory =
            factory
            ?? throw new ArgumentNullException(
                nameof(factory));
    }

    /// <summary>
    /// Gets the currently owned transport connection,
    /// or <see langword="null"/> before a connection has been created.
    /// </summary>
    public ITransportConnection? CurrentConnection =>
        _currentConnection;

    /// <summary>
    /// Gets the current connection state,
    /// or <see langword="null"/> when no connection exists.
    /// </summary>
    public TransportConnectionState? CurrentState =>
        _currentConnection?.State;

    /// <summary>
    /// Creates and owns the initial transport connection.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A current connection already exists.
    /// </exception>
    public async Task<ITransportConnection> ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(
            cancellationToken);

        try
        {
            ThrowIfDisposed();

            if (_currentConnection is not null)
            {
                throw new InvalidOperationException(
                    "The transport connection manager already owns "
                    + "a connection.");
            }

            ITransportConnection connection =
                await _factory.ConnectAsync(
                    cancellationToken);

            _currentConnection =
                connection
                ?? throw new InvalidOperationException(
                    "The transport factory returned a null connection.");

            return connection;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Replaces the current faulted connection with a newly created
    /// connection.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No current connection exists, or the current connection is not faulted.
    /// </exception>
    public async Task<ITransportConnection> ReplaceFaultedAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(
            cancellationToken);

        try
        {
            ThrowIfDisposed();

            ITransportConnection previousConnection =
                _currentConnection
                ?? throw new InvalidOperationException(
                    "The transport connection manager does not own "
                    + "a connection.");

            if (previousConnection.State
                != TransportConnectionState.Faulted)
            {
                throw new InvalidOperationException(
                    "Only a faulted transport connection can be "
                    + "replaced.");
            }

            ITransportConnection replacementConnection =
                await _factory.ConnectAsync(
                    cancellationToken);

            if (replacementConnection is null)
            {
                throw new InvalidOperationException(
                    "The transport factory returned a null connection.");
            }

            _currentConnection =
                replacementConnection;

            await DisposeConnectionAsync(
                previousConnection);

            return replacementConnection;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Disposes the currently owned connection and the manager.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _operationLock.WaitAsync();

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed =
                true;

            ITransportConnection? connection =
                _currentConnection;

            _currentConnection =
                null;

            if (connection is not null)
            {
                await DisposeConnectionAsync(
                    connection);
            }
        }
        finally
        {
            _operationLock.Release();
            _operationLock.Dispose();
        }
    }

    private static async ValueTask DisposeConnectionAsync(
        ITransportConnection connection)
    {
        if (connection is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();

            return;
        }

        if (connection is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
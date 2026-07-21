using Hase.CompactProtocol;

namespace Hase.Runtime.Transport;

/// <summary>
/// Owns at most one active compact endpoint connection and coordinates
/// replacement, detachment, and shutdown.
/// </summary>
internal sealed class CompactEndpointConnectionOwner
    : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate =
        new(
            initialCount: 1,
            maxCount: 1);

    private CompactEndpointConnection? _current;
    private bool _disposed;

    public CompactEndpointConnection? Current =>
        _current;

    public async Task ReplaceAsync(
        CompactEndpointConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            connection);

        CompactEndpointConnection? previous;

        await _gate.WaitAsync(
            cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            if (ReferenceEquals(
                    _current,
                    connection))
            {
                return;
            }

            previous =
                _current;

            _current =
                connection;
        }
        finally
        {
            _gate.Release();
        }

        if (previous is not null)
        {
            await previous.DisposeAsync();
        }
    }

    public async Task DetachAsync(
        CancellationToken cancellationToken = default)
    {
        CompactEndpointConnection? connection;

        await _gate.WaitAsync(
            cancellationToken);

        try
        {
            if (_disposed)
            {
                return;
            }

            connection =
                _current;

            _current =
                null;
        }
        finally
        {
            _gate.Release();
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        CompactEndpointConnection? connection;

        await _gate.WaitAsync();

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed =
                true;

            connection =
                _current;

            _current =
                null;
        }
        finally
        {
            _gate.Release();
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
        }
    }
}
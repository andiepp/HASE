using Hase.Core.Domain.Endpoints;

namespace Hase.CompactProtocol;

/// <summary>
/// Owns one initialized compact endpoint descriptor together with the established
/// Compact Serial Protocol connection used to communicate with that endpoint.
/// </summary>
internal sealed class CompactEndpointConnection
    : IAsyncDisposable
{
    private readonly ICompactSerialProtocolConnection _connection;
    private bool _disposed;

    public CompactEndpointConnection(
        EndpointDescriptor descriptor,
        ICompactSerialProtocolConnection connection)
    {
        Descriptor =
            descriptor
            ?? throw new ArgumentNullException(
                nameof(descriptor));

        _connection =
            connection
            ?? throw new ArgumentNullException(
                nameof(connection));
    }

    /// <summary>
    /// Gets the materialized descriptor for the connected endpoint.
    /// </summary>
    public EndpointDescriptor Descriptor
    {
        get;
    }

    /// <summary>
    /// Gets the established Compact Serial Protocol connection.
    /// </summary>
    public ICompactSerialProtocolConnection Connection =>
        _connection;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        await _connection.DisposeAsync();
    }
}
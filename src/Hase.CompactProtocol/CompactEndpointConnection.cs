using Hase.Core.Domain.Endpoints;

namespace Hase.CompactProtocol;

/// <summary>
/// Owns one initialized compact endpoint descriptor together with the established
/// Compact Serial Protocol connection used to communicate with that endpoint.
/// </summary>
internal sealed class CompactEndpointConnection
    : IAsyncDisposable
{
    private ICompactSerialProtocolConnection _connection;
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

    public CompactEndpointConnection(
        ICompactSerialProtocolConnection connection,
        CompactEndpointInitializationResult initializationResult)
    {
        _connection =
            connection
            ?? throw new ArgumentNullException(
                nameof(connection));

        InitializationResult =
            initializationResult
            ?? throw new ArgumentNullException(
                nameof(initializationResult));

        Descriptor =
            initializationResult.Descriptor;
    }

    /// <summary>
    /// Gets the complete initialization result when this connection was
    /// created by the production compact endpoint initialization path.
    /// </summary>
    public CompactEndpointInitializationResult? InitializationResult
    {
        get;
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

    /// <summary>
    /// Replaces the owned protocol connection with one transparent decorator
    /// before this endpoint connection is published for operational use.
    /// </summary>
    internal void ApplyConnectionDecorator(
        Func<
            ICompactSerialProtocolConnection,
            ICompactSerialProtocolConnection> decorator)
    {
        ArgumentNullException.ThrowIfNull(
            decorator);

        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        _connection =
            decorator(
                _connection)
            ?? throw new InvalidOperationException(
                "The compact protocol connection decorator returned null.");
    }

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

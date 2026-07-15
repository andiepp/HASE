namespace Hase.Transport.Loopback;

/// <summary>
/// In-memory request/response transport.
///
/// The transport forwards raw request bytes to a supplied endpoint
/// handler and returns the raw response bytes produced by that handler.
/// It remains independent of the HASE protocol and runtime layers.
/// </summary>
public sealed class LoopbackTransportConnection
    : ITransportConnection
{
    private readonly Func<
        byte[],
        CancellationToken,
        Task<byte[]>> _exchangeHandler;

    public LoopbackTransportConnection(
        Func<
            byte[],
            CancellationToken,
            Task<byte[]>> exchangeHandler)
    {
        _exchangeHandler =
            exchangeHandler
            ?? throw new ArgumentNullException(
                nameof(exchangeHandler));
    }

    /// <inheritdoc />
    public TransportConnectionState State =>
        TransportConnectionState.Connected;

    /// <inheritdoc />
    public async Task<byte[]> ExchangeAsync(
        byte[] request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        byte[] response =
            await _exchangeHandler(
                request,
                cancellationToken);

        if (response is null)
        {
            throw new InvalidOperationException(
                "The loopback endpoint handler returned a null response.");
        }

        return response;
    }
}
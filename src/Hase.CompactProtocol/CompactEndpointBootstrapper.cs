using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol;

/// <summary>
/// Reads authoritative endpoint identity and an exact descriptor reference
/// through an established Compact Serial Protocol connection.
/// </summary>
internal sealed class CompactEndpointBootstrapper
{
    private static int _nextCorrelationId;

    private readonly ICompactSerialProtocolConnection _connection;
    private readonly Func<byte> _correlationIdFactory;

    public CompactEndpointBootstrapper(
        ICompactSerialProtocolConnection connection)
        : this(
            connection,
            CreateCorrelationId)
    {
    }

    internal CompactEndpointBootstrapper(
        ICompactSerialProtocolConnection connection,
        Func<byte> correlationIdFactory)
    {
        _connection =
            connection
            ?? throw new ArgumentNullException(
                nameof(connection));

        _correlationIdFactory =
            correlationIdFactory
            ?? throw new ArgumentNullException(
                nameof(correlationIdFactory));
    }

    /// <summary>
    /// Performs one compact bootstrap exchange and validates an optional
    /// expected endpoint identity.
    /// </summary>
    public async Task<CompactBootstrapResponse> BootstrapAsync(
        EndpointId? expectedEndpointId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte correlationId =
            _correlationIdFactory();

        if (correlationId == 0)
        {
            throw new InvalidOperationException(
                "The compact correlation-identifier factory returned "
                + "the reserved zero value.");
        }

        var request =
            new CompactBootstrapRequest(
                correlationId);

        CompactSerialFrame responseFrame =
            await _connection.ExchangeAsync(
                CompactBootstrapCodec.EncodeRequest(
                    request),
                cancellationToken);

        CompactBootstrapResponse response =
            CompactBootstrapCodec.DecodeResponse(
                responseFrame);

        if (expectedEndpointId is not null
            && response.EndpointId
                != expectedEndpointId)
        {
            throw new InvalidDataException(
                $"The compact endpoint identity "
                + $"'{response.EndpointId.Value}' does not match the "
                + $"expected endpoint identity "
                + $"'{expectedEndpointId.Value}'.");
        }

        return response;
    }

    private static byte CreateCorrelationId()
    {
        uint value =
            unchecked(
                (uint)Interlocked.Increment(
                    ref _nextCorrelationId));

        return checked(
            (byte)(((value - 1) % byte.MaxValue) + 1));
    }
}
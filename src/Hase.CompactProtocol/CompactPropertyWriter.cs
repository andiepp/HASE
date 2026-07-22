namespace Hase.CompactProtocol;

/// <summary>
/// Writes descriptor-encoded values to compact endpoint properties through an
/// established Compact Serial Protocol connection.
/// </summary>
internal sealed class CompactPropertyWriter
{
    private static int _nextCorrelationId;

    private readonly ICompactSerialProtocolConnection _connection;
    private readonly Func<byte> _correlationIdFactory;

    public CompactPropertyWriter(
        ICompactSerialProtocolConnection connection)
        : this(
            connection,
            CreateCorrelationId)
    {
    }

    internal CompactPropertyWriter(
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
    /// Writes one descriptor-encoded compact property value and returns the
    /// endpoint-reported write status.
    /// </summary>
    public async Task<CompactPropertyWriteStatus> WriteAsync(
        byte propertyId,
        ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (propertyId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(propertyId),
                propertyId,
                "A compact property identifier must be nonzero.");
        }

        if (value.IsEmpty)
        {
            throw new ArgumentException(
                "A compact property write must contain value bytes.",
                nameof(value));
        }

        byte correlationId =
            _correlationIdFactory();

        if (correlationId == 0)
        {
            throw new InvalidOperationException(
                "The compact correlation-identifier factory returned the "
                + "reserved zero value.");
        }

        var request =
            new CompactWritePropertyRequest(
                correlationId,
                propertyId,
                value);

        CompactSerialFrame responseFrame =
            await _connection.ExchangeAsync(
                CompactWritePropertyCodec.EncodeRequest(
                    request),
                cancellationToken);

        CompactWritePropertyResponse response =
            CompactWritePropertyCodec.DecodeResponse(
                responseFrame);

        if (response.CorrelationId != correlationId)
        {
            throw new InvalidDataException(
                $"Compact property-write response correlation identifier "
                + $"0x{response.CorrelationId:X2} does not match request "
                + $"correlation identifier 0x{correlationId:X2}.");
        }

        if (response.PropertyId != propertyId)
        {
            throw new InvalidDataException(
                $"Compact property-write response identifier "
                + $"0x{response.PropertyId:X2} does not match requested "
                + $"property identifier 0x{propertyId:X2}.");
        }

        return response.Status;
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
using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol;

/// <summary>
/// Reads and decodes compact endpoint properties through an established Compact
/// Serial Protocol connection.
/// </summary>
internal sealed class CompactPropertyReader
{
    private static int _nextCorrelationId;

    private readonly ICompactSerialProtocolConnection _connection;
    private readonly CompactPropertyMap _propertyMap;
    private readonly Func<byte> _correlationIdFactory;
    private readonly Func<DateTimeOffset> _utcNowFactory;

    public CompactPropertyReader(
        ICompactSerialProtocolConnection connection,
        CompactPropertyMap propertyMap)
        : this(
            connection,
            propertyMap,
            CreateCorrelationId,
            () => DateTimeOffset.UtcNow)
    {
    }

    internal CompactPropertyReader(
        ICompactSerialProtocolConnection connection,
        CompactPropertyMap propertyMap,
        Func<byte> correlationIdFactory,
        Func<DateTimeOffset> utcNowFactory)
    {
        _connection =
            connection
            ?? throw new ArgumentNullException(
                nameof(connection));

        _propertyMap =
            propertyMap
            ?? throw new ArgumentNullException(
                nameof(propertyMap));

        _correlationIdFactory =
            correlationIdFactory
            ?? throw new ArgumentNullException(
                nameof(correlationIdFactory));

        _utcNowFactory =
            utcNowFactory
            ?? throw new ArgumentNullException(
                nameof(utcNowFactory));
    }

    public async Task<CompactPropertyReadResult> ReadAsync(
        byte compactPropertyId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (compactPropertyId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compactPropertyId),
                compactPropertyId,
                "A compact property identifier must be nonzero.");
        }

        CompactPropertyMapping mapping =
            _propertyMap.Find(
                compactPropertyId)
            ?? throw new ArgumentException(
                $"Compact property identifier 0x{compactPropertyId:X2} "
                + "is not present in the selected host-side descriptor.",
                nameof(compactPropertyId));

        byte correlationId =
            _correlationIdFactory();

        if (correlationId == 0)
        {
            throw new InvalidOperationException(
                "The compact correlation-identifier factory returned the "
                + "reserved zero value.");
        }

        var request =
            new CompactReadPropertyRequest(
                correlationId,
                compactPropertyId);

        CompactSerialFrame responseFrame =
            await _connection.ExchangeAsync(
                CompactReadPropertyCodec.EncodeRequest(
                    request),
                cancellationToken);

        CompactReadPropertyResponse response =
            CompactReadPropertyCodec.DecodeResponse(
                responseFrame);

        if (response.CorrelationId != correlationId)
        {
            throw new InvalidDataException(
                $"Compact property response correlation identifier "
                + $"0x{response.CorrelationId:X2} does not match request "
                + $"correlation identifier 0x{correlationId:X2}.");
        }

        if (response.PropertyId != compactPropertyId)
        {
            throw new InvalidDataException(
                $"Compact property response identifier "
                + $"0x{response.PropertyId:X2} does not match requested "
                + $"property identifier 0x{compactPropertyId:X2}.");
        }

        if (response.Status != CompactPropertyReadStatus.Success)
        {
            return new CompactPropertyReadResult(
                mapping,
                response.Status,
                value: null);
        }

        object decodedValue =
            CompactPropertyValueDecoder.Decode(
                mapping.Encoding,
                response.Value.Span);

        var propertyValue =
            new PropertyValue(
                decodedValue,
                _utcNowFactory(),
                PropertyQuality.Good);

        return new CompactPropertyReadResult(
            mapping,
            response.Status,
            propertyValue);
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
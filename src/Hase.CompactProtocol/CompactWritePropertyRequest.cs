namespace Hase.CompactProtocol;

/// <summary>
/// Requests that one compact endpoint property be assigned a
/// descriptor-defined wire value.
/// </summary>
internal sealed record CompactWritePropertyRequest
{
    public CompactWritePropertyRequest(
        byte correlationId,
        byte propertyId,
        ReadOnlyMemory<byte> value)
    {
        if (correlationId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlationId),
                correlationId,
                "A compact write-property request must use a nonzero "
                + "correlation identifier.");
        }

        if (propertyId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(propertyId),
                propertyId,
                "A compact write-property request must use a nonzero "
                + "property identifier.");
        }

        if (value.IsEmpty)
        {
            throw new ArgumentException(
                "A compact write-property request must contain value bytes.",
                nameof(value));
        }

        CorrelationId =
            correlationId;

        PropertyId =
            propertyId;

        Value =
            value.ToArray();
    }

    public byte CorrelationId
    {
        get;
    }

    public byte PropertyId
    {
        get;
    }

    public ReadOnlyMemory<byte> Value
    {
        get;
    }
}
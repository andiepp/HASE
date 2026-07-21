namespace Hase.CompactProtocol;

/// <summary>
/// Reports the outcome and descriptor-defined wire value of one compact
/// endpoint property read.
/// </summary>
internal sealed record CompactReadPropertyResponse
{
    public CompactReadPropertyResponse(
        byte correlationId,
        byte propertyId,
        CompactPropertyReadStatus status,
        ReadOnlyMemory<byte> value)
    {
        if (correlationId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlationId),
                correlationId,
                "A compact read-property response must use a nonzero "
                + "correlation identifier.");
        }

        if (propertyId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(propertyId),
                propertyId,
                "A compact read-property response must use a nonzero "
                + "property identifier.");
        }

        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The compact property-read status is not defined.");
        }

        if (status == CompactPropertyReadStatus.Success
            && value.IsEmpty)
        {
            throw new ArgumentException(
                "A successful compact property read must contain value "
                + "bytes.",
                nameof(value));
        }

        if (status != CompactPropertyReadStatus.Success
            && !value.IsEmpty)
        {
            throw new ArgumentException(
                "An unsuccessful compact property read must not contain "
                + "value bytes.",
                nameof(value));
        }

        CorrelationId =
            correlationId;

        PropertyId =
            propertyId;

        Status =
            status;

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

    public CompactPropertyReadStatus Status
    {
        get;
    }

    public ReadOnlyMemory<byte> Value
    {
        get;
    }
}
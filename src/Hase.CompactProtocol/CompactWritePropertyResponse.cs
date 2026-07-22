namespace Hase.CompactProtocol;

/// <summary>
/// Reports the outcome of one compact endpoint property write.
/// </summary>
internal sealed record CompactWritePropertyResponse
{
    public CompactWritePropertyResponse(
        byte correlationId,
        byte propertyId,
        CompactPropertyWriteStatus status)
    {
        if (correlationId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlationId),
                correlationId,
                "A compact write-property response must use a nonzero "
                + "correlation identifier.");
        }

        if (propertyId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(propertyId),
                propertyId,
                "A compact write-property response must use a nonzero "
                + "property identifier.");
        }

        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The compact property-write status is not defined.");
        }

        CorrelationId =
            correlationId;

        PropertyId =
            propertyId;

        Status =
            status;
    }

    public byte CorrelationId
    {
        get;
    }

    public byte PropertyId
    {
        get;
    }

    public CompactPropertyWriteStatus Status
    {
        get;
    }
}
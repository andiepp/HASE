namespace Hase.CompactProtocol;

/// <summary>
/// Requests one compact endpoint property identified by its
/// resource-constrained wire identifier.
/// </summary>
internal sealed record CompactReadPropertyRequest
{
    public CompactReadPropertyRequest(
        byte correlationId,
        byte propertyId)
    {
        if (correlationId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlationId),
                correlationId,
                "A compact read-property request must use a nonzero "
                + "correlation identifier.");
        }

        if (propertyId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(propertyId),
                propertyId,
                "A compact read-property request must use a nonzero "
                + "property identifier.");
        }

        CorrelationId =
            correlationId;

        PropertyId =
            propertyId;
    }

    public byte CorrelationId
    {
        get;
    }

    public byte PropertyId
    {
        get;
    }
}
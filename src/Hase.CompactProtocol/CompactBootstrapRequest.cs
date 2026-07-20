namespace Hase.CompactProtocol;

/// <summary>
/// Requests authoritative endpoint identity and an exact descriptor reference.
/// </summary>
internal sealed record CompactBootstrapRequest
{
    public CompactBootstrapRequest(
        byte correlationId)
    {
        if (correlationId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(correlationId),
                correlationId,
                "A compact bootstrap request must use a nonzero "
                + "correlation identifier.");
        }

        CorrelationId =
            correlationId;
    }

    public byte CorrelationId
    {
        get;
    }
}
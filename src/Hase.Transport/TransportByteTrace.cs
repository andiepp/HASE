namespace Hase.Transport;

/// <summary>
/// Exposes one complete raw transport frame to synchronous observers.
/// </summary>
/// <remarks>
/// The byte memory is callback-scoped and is not copied by the source.
/// Observers that retain bytes must create their own bounded copy.
/// </remarks>
public readonly record struct TransportByteTrace
{
    public TransportByteTrace(
        TransportByteDirection direction,
        ReadOnlyMemory<byte> bytes,
        string? correlationId = null)
    {
        if (!Enum.IsDefined(
                direction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "Direction is not defined.");
        }

        Direction =
            direction;

        Bytes =
            bytes;

        CorrelationId =
            string.IsNullOrWhiteSpace(
                correlationId)
                ? null
                : correlationId.Trim();
    }

    public TransportByteDirection Direction
    {
        get;
    }

    public ReadOnlyMemory<byte> Bytes
    {
        get;
    }

    public string? CorrelationId
    {
        get;
    }
}

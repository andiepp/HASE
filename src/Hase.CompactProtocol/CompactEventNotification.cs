namespace Hase.CompactProtocol;

/// <summary>
/// Represents one unsolicited Compact Serial Protocol Version 1 event
/// notification identified by its descriptor-defined compact event identifier.
/// </summary>
internal sealed record CompactEventNotification
{
    public CompactEventNotification(
        byte eventId,
        ReadOnlyMemory<byte> value)
    {
        if (eventId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventId),
                eventId,
                "A compact event notification must use a nonzero event "
                + "identifier.");
        }

        EventId =
            eventId;

        Value =
            value.ToArray();
    }

    /// <summary>
    /// Gets the nonzero descriptor-defined compact event identifier.
    /// </summary>
    public byte EventId
    {
        get;
    }

    /// <summary>
    /// Gets the optional descriptor-defined compact event value bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Value
    {
        get;
    }
}
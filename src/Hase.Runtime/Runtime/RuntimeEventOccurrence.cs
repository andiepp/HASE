namespace Hase.Runtime.Runtime;

/// <summary>
/// Describes one occurrence of a runtime event.
/// </summary>
public sealed record RuntimeEventOccurrence
{
    /// <summary>
    /// Initializes a runtime event occurrence.
    /// </summary>
    public RuntimeEventOccurrence(
        RuntimeEvent runtimeEvent,
        DateTimeOffset timestampUtc,
        object? value)
    {
        Event =
            runtimeEvent
            ?? throw new ArgumentNullException(
                nameof(runtimeEvent));

        if (timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The event occurrence timestamp must be expressed in UTC.",
                nameof(timestampUtc));
        }

        TimestampUtc =
            timestampUtc;

        Value =
            value;
    }

    /// <summary>
    /// Gets the runtime event that occurred.
    /// </summary>
    public RuntimeEvent Event
    {
        get;
    }

    /// <summary>
    /// Gets the UTC occurrence timestamp. Protocols that carry an endpoint
    /// timestamp preserve it; protocols without one use the host observation
    /// time.
    /// </summary>
    public DateTimeOffset TimestampUtc
    {
        get;
    }

    /// <summary>
    /// Gets the optional event value.
    /// </summary>
    public object? Value
    {
        get;
    }
}
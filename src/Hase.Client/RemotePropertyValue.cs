namespace Hase.Client;

/// <summary>
/// Represents one immutable timestamped normalized remote Property value.
/// </summary>
public sealed record RemotePropertyValue
{
    /// <summary>
    /// Initializes one normalized remote Property value.
    /// </summary>
    public RemotePropertyValue(
        RemoteValue? value,
        DateTimeOffset timestampUtc,
        RemotePropertyQuality quality)
    {
        if (timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The remote Property timestamp must be expressed in UTC.",
                nameof(timestampUtc));
        }

        if (!Enum.IsDefined(
                quality)
            || quality == RemotePropertyQuality.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quality),
                quality,
                "A specified remote Property quality is required.");
        }

        Value =
            value;
        TimestampUtc =
            timestampUtc;
        Quality =
            quality;
    }

    /// <summary>
    /// Gets the normalized value when the runtime host supplied one.
    /// </summary>
    public RemoteValue? Value
    {
        get;
    }

    /// <summary>
    /// Gets the UTC timestamp associated with this Property value.
    /// </summary>
    public DateTimeOffset TimestampUtc
    {
        get;
    }

    /// <summary>
    /// Gets the normalized Property quality.
    /// </summary>
    public RemotePropertyQuality Quality
    {
        get;
    }
}

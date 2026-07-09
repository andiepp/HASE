namespace Hase.Core.Domain.Properties;

/// <summary>
/// Represents an immutable timestamped snapshot of a property value.
/// </summary>
public sealed record PropertyValue
{
    public PropertyValue(
        object? value,
        DateTimeOffset timestampUtc,
        PropertyQuality quality = PropertyQuality.Good)
    {
        if (timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must be expressed in UTC.",
                nameof(timestampUtc));
        }

        Value = value;
        TimestampUtc = timestampUtc;
        Quality = quality;
    }

    /// <summary>
    /// Current engineering value.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// UTC timestamp of this value.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; }

    /// <summary>
    /// Indicates the quality of the value.
    /// </summary>
    public PropertyQuality Quality { get; }
}
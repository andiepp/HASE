using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Describes one transient runtime Event occurrence.
/// </summary>
public sealed record RuntimeHostEventOccurredObservationPayload
    : RuntimeHostObservationPayload
{
    /// <summary>
    /// Initializes an Event-occurred observation payload.
    /// </summary>
    public RuntimeHostEventOccurredObservationPayload(
        InstrumentId instrumentId,
        DescriptorPath eventPath,
        DateTimeOffset occurredAtUtc,
        object? value)
    {
        InstrumentId =
            instrumentId
            ?? throw new ArgumentNullException(
                nameof(instrumentId));

        EventPath =
            eventPath
            ?? throw new ArgumentNullException(
                nameof(eventPath));

        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The Event occurrence time must be expressed in UTC.",
                nameof(occurredAtUtc));
        }

        OccurredAtUtc =
            occurredAtUtc;

        Value =
            value;
    }

    /// <inheritdoc />
    public override RuntimeHostObservationKind Kind =>
        RuntimeHostObservationKind.EventOccurred;

    /// <summary>
    /// Gets the instrument identity.
    /// </summary>
    public InstrumentId InstrumentId
    {
        get;
    }

    /// <summary>
    /// Gets the logical Event path.
    /// </summary>
    public DescriptorPath EventPath
    {
        get;
    }

    /// <summary>
    /// Gets the UTC Event occurrence time.
    /// </summary>
    public DateTimeOffset OccurredAtUtc
    {
        get;
    }

    /// <summary>
    /// Gets the optional Event value.
    /// </summary>
    public object? Value
    {
        get;
    }
}
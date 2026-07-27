using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client;

/// <summary>
/// Describes one transient remote runtime Event occurrence.
/// </summary>
public sealed record RemoteEventOccurredObservationPayload
    : RemoteObservationPayload
{
    /// <summary>
    /// Initializes one Event-occurred observation payload.
    /// </summary>
    public RemoteEventOccurredObservationPayload(
        InstrumentId instrumentId,
        DescriptorPath eventPath,
        DateTimeOffset occurredAtUtc,
        RemoteValue? value)
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
                "The remote Event occurrence time must be expressed in UTC.",
                nameof(occurredAtUtc));
        }

        OccurredAtUtc =
            occurredAtUtc;
        Value =
            value;
    }

    /// <inheritdoc />
    public override RemoteObservationKind Kind =>
        RemoteObservationKind.EventOccurred;

    /// <summary>
    /// Gets the instrument identity.
    /// </summary>
    public InstrumentId InstrumentId
    {
        get;
    }

    /// <summary>
    /// Gets the complete ordered logical Event path.
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
    /// Gets the optional normalized Event value.
    /// </summary>
    public RemoteValue? Value
    {
        get;
    }
}

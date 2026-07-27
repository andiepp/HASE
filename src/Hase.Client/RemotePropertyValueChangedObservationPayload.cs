using Hase.Core.Domain.Identity;

namespace Hase.Client;

/// <summary>
/// Describes one change already accepted by the authoritative remote runtime
/// Property cache.
/// </summary>
public sealed record RemotePropertyValueChangedObservationPayload
    : RemoteObservationPayload
{
    /// <summary>
    /// Initializes one Property-value-changed observation payload.
    /// </summary>
    public RemotePropertyValueChangedObservationPayload(
        InstrumentId instrumentId,
        PropertyId propertyId,
        RemotePropertyValue? previousValue,
        RemotePropertyValue currentValue)
    {
        InstrumentId =
            instrumentId
            ?? throw new ArgumentNullException(
                nameof(instrumentId));

        PropertyId =
            propertyId
            ?? throw new ArgumentNullException(
                nameof(propertyId));

        PreviousValue =
            previousValue;

        CurrentValue =
            currentValue
            ?? throw new ArgumentNullException(
                nameof(currentValue));
    }

    /// <inheritdoc />
    public override RemoteObservationKind Kind =>
        RemoteObservationKind.PropertyValueChanged;

    /// <summary>
    /// Gets the instrument identity.
    /// </summary>
    public InstrumentId InstrumentId
    {
        get;
    }

    /// <summary>
    /// Gets the Property identity.
    /// </summary>
    public PropertyId PropertyId
    {
        get;
    }

    /// <summary>
    /// Gets the previous known Property value when available.
    /// </summary>
    public RemotePropertyValue? PreviousValue
    {
        get;
    }

    /// <summary>
    /// Gets the current authoritative runtime-cache Property value.
    /// </summary>
    public RemotePropertyValue CurrentValue
    {
        get;
    }
}

using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Describes a change already accepted by the authoritative runtime Property
/// cache.
/// </summary>
public sealed record RuntimeHostPropertyValueChangedObservationPayload
    : RuntimeHostObservationPayload
{
    /// <summary>
    /// Initializes a Property-value-changed observation payload.
    /// </summary>
    public RuntimeHostPropertyValueChangedObservationPayload(
        InstrumentId instrumentId,
        PropertyId propertyId,
        PropertyValue? previousValue,
        PropertyValue currentValue)
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
    public override RuntimeHostObservationKind Kind =>
        RuntimeHostObservationKind.PropertyValueChanged;

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
    /// Gets the previous known Property value, when available.
    /// </summary>
    public PropertyValue? PreviousValue
    {
        get;
    }

    /// <summary>
    /// Gets the current authoritative runtime-cache Property value.
    /// </summary>
    public PropertyValue CurrentValue
    {
        get;
    }
}
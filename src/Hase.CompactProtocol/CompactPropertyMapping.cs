using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol;

/// <summary>
/// Maps one resource-constrained wire property identifier to one property in a
/// predefined host-side endpoint descriptor.
/// </summary>
public sealed record CompactPropertyMapping
{
    /// <summary>
    /// Initializes one compact property mapping.
    /// </summary>
    public CompactPropertyMapping(
        byte compactPropertyId,
        InstrumentId instrumentId,
        PropertyId propertyId,
        CompactPropertyValueEncoding encoding)
    {
        if (compactPropertyId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compactPropertyId),
                compactPropertyId,
                "A compact property identifier must be nonzero.");
        }

        if (!Enum.IsDefined(
                encoding))
        {
            throw new ArgumentOutOfRangeException(
                nameof(encoding),
                encoding,
                "The compact property-value encoding is not defined.");
        }

        CompactPropertyId =
            compactPropertyId;

        InstrumentId =
            instrumentId
            ?? throw new ArgumentNullException(
                nameof(instrumentId));

        PropertyId =
            propertyId
            ?? throw new ArgumentNullException(
                nameof(propertyId));

        Encoding =
            encoding;
    }

    /// <summary>
    /// Gets the nonzero compact wire-property identifier.
    /// </summary>
    public byte CompactPropertyId
    {
        get;
    }

    /// <summary>
    /// Gets the target runtime instrument identity.
    /// </summary>
    public InstrumentId InstrumentId
    {
        get;
    }

    /// <summary>
    /// Gets the target runtime property identity.
    /// </summary>
    public PropertyId PropertyId
    {
        get;
    }

    /// <summary>
    /// Gets the compact wire-value encoding.
    /// </summary>
    public CompactPropertyValueEncoding Encoding
    {
        get;
    }
}
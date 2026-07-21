using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol;

/// <summary>
/// Maps one resource-constrained wire property identifier to one property in a
/// predefined host-side endpoint descriptor.
/// </summary>
internal sealed record CompactPropertyMapping
{
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

    public byte CompactPropertyId
    {
        get;
    }

    public InstrumentId InstrumentId
    {
        get;
    }

    public PropertyId PropertyId
    {
        get;
    }

    public CompactPropertyValueEncoding Encoding
    {
        get;
    }
}
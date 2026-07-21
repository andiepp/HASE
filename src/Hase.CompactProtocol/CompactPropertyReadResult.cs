using Hase.Core.Domain.Properties;

namespace Hase.CompactProtocol;

/// <summary>
/// Contains the endpoint-reported status and optional decoded value of one
/// compact property read.
/// </summary>
internal sealed record CompactPropertyReadResult
{
    public CompactPropertyReadResult(
        CompactPropertyMapping mapping,
        CompactPropertyReadStatus status,
        PropertyValue? value)
    {
        Mapping =
            mapping
            ?? throw new ArgumentNullException(
                nameof(mapping));

        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The compact property-read status is not defined.");
        }

        if (status == CompactPropertyReadStatus.Success
            && value is null)
        {
            throw new ArgumentException(
                "A successful compact property read must contain a decoded "
                + "property value.",
                nameof(value));
        }

        if (status != CompactPropertyReadStatus.Success
            && value is not null)
        {
            throw new ArgumentException(
                "An unsuccessful compact property read must not contain a "
                + "decoded property value.",
                nameof(value));
        }

        Status =
            status;

        Value =
            value;
    }

    public CompactPropertyMapping Mapping
    {
        get;
    }

    public CompactPropertyReadStatus Status
    {
        get;
    }

    public PropertyValue? Value
    {
        get;
    }
}
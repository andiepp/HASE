namespace Hase.CompactProtocol;

/// <summary>
/// Contains the resolved descriptor mapping and endpoint-reported status of one
/// compact property write.
/// </summary>
internal sealed record CompactPropertyWriteResult
{
    public CompactPropertyWriteResult(
        CompactPropertyMapping mapping,
        CompactPropertyWriteStatus status)
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
                "The compact property-write status is not defined.");
        }

        Status =
            status;
    }

    public CompactPropertyMapping Mapping
    {
        get;
    }

    public CompactPropertyWriteStatus Status
    {
        get;
    }
}
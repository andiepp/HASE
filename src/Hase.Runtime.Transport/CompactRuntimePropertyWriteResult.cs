using Hase.CompactProtocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Reports the write, confirmation-read, and runtime-cache outcome of one
/// compact property write.
/// </summary>
internal sealed record CompactRuntimePropertyWriteResult
{
    public CompactRuntimePropertyWriteResult(
        CompactPropertyMapping mapping,
        RuntimeProperty runtimeProperty,
        CompactPropertyWriteStatus writeStatus,
        CompactPropertyReadStatus? confirmationReadStatus)
    {
        Mapping =
            mapping
            ?? throw new ArgumentNullException(
                nameof(mapping));

        RuntimeProperty =
            runtimeProperty
            ?? throw new ArgumentNullException(
                nameof(runtimeProperty));

        if (!Enum.IsDefined(
                writeStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(writeStatus),
                writeStatus,
                "The compact property-write status is not defined.");
        }

        if (confirmationReadStatus.HasValue
            && !Enum.IsDefined(
                confirmationReadStatus.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmationReadStatus),
                confirmationReadStatus,
                "The compact confirmation-read status is not defined.");
        }

        if (writeStatus != CompactPropertyWriteStatus.Success
            && confirmationReadStatus.HasValue)
        {
            throw new ArgumentException(
                "An unsuccessful compact property write must not contain a "
                + "confirmation-read status.",
                nameof(confirmationReadStatus));
        }

        if (writeStatus == CompactPropertyWriteStatus.Success
            && !confirmationReadStatus.HasValue)
        {
            throw new ArgumentException(
                "A successful compact property write must contain a "
                + "confirmation-read status.",
                nameof(confirmationReadStatus));
        }

        WriteStatus =
            writeStatus;

        ConfirmationReadStatus =
            confirmationReadStatus;
    }

    public CompactPropertyMapping Mapping
    {
        get;
    }

    public RuntimeProperty RuntimeProperty
    {
        get;
    }

    public CompactPropertyWriteStatus WriteStatus
    {
        get;
    }

    public CompactPropertyReadStatus? ConfirmationReadStatus
    {
        get;
    }

    public bool CacheUpdated =>
        WriteStatus == CompactPropertyWriteStatus.Success
        && ConfirmationReadStatus
            == CompactPropertyReadStatus.Success;
}
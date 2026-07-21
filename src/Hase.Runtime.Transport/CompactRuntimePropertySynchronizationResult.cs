using Hase.CompactProtocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Reports the runtime-cache outcome of one compact property read.
/// </summary>
internal sealed record CompactRuntimePropertySynchronizationResult
{
    public CompactRuntimePropertySynchronizationResult(
        CompactPropertyMapping mapping,
        RuntimeProperty runtimeProperty,
        CompactPropertyReadStatus status)
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
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The compact property-read status is not defined.");
        }

        Status =
            status;
    }

    public CompactPropertyMapping Mapping
    {
        get;
    }

    public RuntimeProperty RuntimeProperty
    {
        get;
    }

    public CompactPropertyReadStatus Status
    {
        get;
    }

    public bool CacheUpdated =>
        Status == CompactPropertyReadStatus.Success;
}

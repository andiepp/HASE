namespace Hase.DesktopHost;

public enum DesktopRuntimePropertyWriteState
{
    Ready = 0,
    Executing = 1,
    Succeeded = 2,
    Rejected = 3,
    Failed = 4,
    Cancelled = 5
}

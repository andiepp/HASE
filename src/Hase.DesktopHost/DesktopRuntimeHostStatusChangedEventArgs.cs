namespace Hase.DesktopHost;

public sealed class DesktopRuntimeHostStatusChangedEventArgs : EventArgs
{
    public DesktopRuntimeHostStatusChangedEventArgs(
        DesktopRuntimeHostStatus previousStatus,
        DesktopRuntimeHostStatus currentStatus)
    {
        PreviousStatus = previousStatus;
        CurrentStatus = currentStatus;
    }

    public DesktopRuntimeHostStatus PreviousStatus { get; }

    public DesktopRuntimeHostStatus CurrentStatus { get; }
}

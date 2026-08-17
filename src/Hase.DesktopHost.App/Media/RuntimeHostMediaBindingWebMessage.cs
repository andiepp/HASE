namespace Hase.DesktopHost.App.Media;

public enum RuntimeHostMediaBindingWebMessageKind
{
    Ready,
    DiscoveryRequested,
    SelectionConfirmed,
    Cancelled,
    Faulted
}

public sealed record RuntimeHostMediaBindingWebMessage(
    RuntimeHostMediaBindingWebMessageKind Kind,
    IReadOnlyList<RuntimeHostMediaBindingSelection>? Selections = null,
    string? FailureCode = null);

public sealed record RuntimeHostMediaBindingSelection(
    string VideoDeviceId,
    string? AudioDeviceId);

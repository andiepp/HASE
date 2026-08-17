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
    string? VideoDeviceId = null,
    string? AudioDeviceId = null,
    string? FailureCode = null);

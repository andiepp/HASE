namespace Hase.DesktopHost.App.Media;

public enum RuntimeHostMediaWebMessageKind
{
    Ready,
    CaptureStarted,
    CaptureStopped,
    CaptureFaulted
}

public sealed record RuntimeHostMediaWebMessage(
    RuntimeHostMediaWebMessageKind Kind,
    string? FailureCode);

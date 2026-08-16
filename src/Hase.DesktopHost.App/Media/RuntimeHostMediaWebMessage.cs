using Hase.Runtime.Media;

namespace Hase.DesktopHost.App.Media;

public enum RuntimeHostMediaWebMessageKind
{
    Ready,
    CaptureStarted,
    CaptureStopped,
    CaptureFaulted,
    Negotiation,
    PeerConnected,
    PeerFaulted
}

public sealed record RuntimeHostMediaWebMessage(
    RuntimeHostMediaWebMessageKind Kind,
    string? FailureCode,
    RuntimeHostMediaNegotiationMessage? NegotiationMessage = null);

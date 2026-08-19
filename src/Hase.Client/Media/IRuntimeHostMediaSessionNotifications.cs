namespace Hase.Client.Media;

public sealed class RemoteMediaSessionChangedEventArgs : EventArgs
{
    public RemoteMediaSessionChangedEventArgs(
        RemoteMediaSessionSnapshot? session,
        string statusText,
        RemoteMediaTerminalReason terminalReason =
            RemoteMediaTerminalReason.None)
    {
        Session = session;
        StatusText = string.IsNullOrWhiteSpace(statusText)
            ? throw new ArgumentException("A sanitized status is required.", nameof(statusText))
            : statusText;
        TerminalReason = terminalReason;
    }

    public RemoteMediaSessionSnapshot? Session { get; }
    public string StatusText { get; }
    public RemoteMediaTerminalReason TerminalReason { get; }
}

public interface IRuntimeHostMediaSessionNotifications
{
    event EventHandler<RemoteMediaSessionChangedEventArgs>? SessionChanged;
}

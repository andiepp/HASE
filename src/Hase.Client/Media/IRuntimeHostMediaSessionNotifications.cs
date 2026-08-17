namespace Hase.Client.Media;

public sealed class RemoteMediaSessionChangedEventArgs : EventArgs
{
    public RemoteMediaSessionChangedEventArgs(
        RemoteMediaSessionSnapshot? session,
        string statusText)
    {
        Session = session;
        StatusText = string.IsNullOrWhiteSpace(statusText)
            ? throw new ArgumentException("A sanitized status is required.", nameof(statusText))
            : statusText;
    }

    public RemoteMediaSessionSnapshot? Session { get; }
    public string StatusText { get; }
}

public interface IRuntimeHostMediaSessionNotifications
{
    event EventHandler<RemoteMediaSessionChangedEventArgs>? SessionChanged;
}

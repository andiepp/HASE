namespace Hase.Client;

/// <summary>
/// Publishes validated transient Event occurrences without persistence or
/// replay.
/// </summary>
public interface IRuntimeHostEventSource
{
    event EventHandler<RemoteEventOccurredEventArgs>? EventOccurred;
}

namespace Hase.DesktopHost;

public interface IDesktopRuntimeHostEventSource
{
    IAsyncEnumerable<DesktopRuntimeEventOccurrence> ObserveEventsAsync(
        CancellationToken cancellationToken = default);
}

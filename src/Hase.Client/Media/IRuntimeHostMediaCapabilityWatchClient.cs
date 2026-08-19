namespace Hase.Client.Media;

public interface IRuntimeHostMediaCapabilityWatchClient
{
    IAsyncEnumerable<RemoteMediaCapabilitySnapshot> WatchCapabilitiesAsync(
        ulong afterRevision = 0,
        CancellationToken cancellationToken = default);
}

namespace Hase.Client.Media;

/// <summary>
/// Client-side control seam for one selected Runtime Host. Implementations
/// authenticate through the existing profile; callers never provide device
/// identities or network destinations.
/// </summary>
public interface IRuntimeHostMediaControlClient
{
    Task<IReadOnlyList<RemoteMediaSourceCapability>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default);

    Task<RemoteMediaStartResult> StartAsync(
        RemoteMediaSourceTarget target,
        bool includeAudio,
        CancellationToken cancellationToken = default);

    Task<RemoteMediaStopResult> StopAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}

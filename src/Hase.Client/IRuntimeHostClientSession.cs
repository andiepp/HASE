namespace Hase.Client;

/// <summary>
/// Owns one normalized client session with a remote runtime host.
/// </summary>
public interface IRuntimeHostClientSession
    : IAsyncDisposable
{
    /// <summary>
    /// Occurs after the normalized session status changes.
    /// </summary>
    event EventHandler<RuntimeHostClientSessionStatusChangedEventArgs>?
        StatusChanged;

    /// <summary>
    /// Gets the current normalized session status.
    /// </summary>
    RuntimeHostClientSessionStatus Status
    {
        get;
    }

    /// <summary>
    /// Gets the latest authoritative or retained normalized state.
    /// </summary>
    RemoteObservationState? CurrentState
    {
        get;
    }

    /// <summary>
    /// Connects through the observation boundary and yields normalized states,
    /// recovering only according to the configured bounded policy.
    /// </summary>
    IAsyncEnumerable<RemoteObservationState> ReadStatesAsync(
        CancellationToken cancellationToken = default);
}

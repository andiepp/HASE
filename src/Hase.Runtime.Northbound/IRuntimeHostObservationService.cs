namespace Hase.Runtime.Northbound;

/// <summary>
/// Provides race-free initial state and normalized live observations for
/// applications using one runtime host.
/// </summary>
public interface IRuntimeHostObservationService
{
    /// <summary>
    /// Opens one independently buffered runtime-host observation subscription.
    /// </summary>
    /// <remarks>
    /// The returned subscription already owns an active observation boundary.
    /// Its stream contains only observations whose sequence is later than its
    /// snapshot sequence.
    /// </remarks>
    Task<RuntimeHostObservationSubscription> OpenSubscriptionAsync(
        RuntimeHostObservationSubscriptionOptions options,
        CancellationToken cancellationToken = default);
}
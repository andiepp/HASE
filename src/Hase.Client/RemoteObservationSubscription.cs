using System.Runtime.CompilerServices;

namespace Hase.Client;

/// <summary>
/// Consumes one transport-neutral observation stream and publishes immutable
/// normalized client states.
/// </summary>
public sealed class RemoteObservationSubscription
{
    private readonly RemoteObservationReducer _reducer;

    /// <summary>
    /// Initializes a subscription lifecycle using the standard reducer.
    /// </summary>
    public RemoteObservationSubscription()
        : this(
            new RemoteObservationReducer())
    {
    }

    internal RemoteObservationSubscription(
        RemoteObservationReducer reducer)
    {
        _reducer =
            reducer
            ?? throw new ArgumentNullException(
                nameof(reducer));
    }

    /// <summary>
    /// Reads the mandatory initial snapshot and then reduces every later
    /// observation into a new immutable state.
    /// </summary>
    /// <remarks>
    /// The initial state is yielded first. Normal transport completion ends
    /// the returned stream. Cancellation, transport failures, and invalid
    /// observation sequences are propagated to the caller.
    /// </remarks>
    public async IAsyncEnumerable<RemoteObservationState> ReadStatesAsync(
        IRemoteObservationStream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        RemoteObservationInitialSnapshot initialSnapshot =
            await stream.ReadInitialSnapshotAsync(
                    cancellationToken)
                .ConfigureAwait(
                    false);

        RemoteObservationState state =
            _reducer.Initialize(
                RemoteObservationState.Empty,
                initialSnapshot);

        yield return state;

        await foreach (RemoteRuntimeHostObservation observation
            in stream.ReadObservationsAsync(
                    cancellationToken)
                .WithCancellation(
                    cancellationToken)
                .ConfigureAwait(
                    false))
        {
            state =
                _reducer.Apply(
                    state,
                    observation);

            yield return state;
        }
    }
}

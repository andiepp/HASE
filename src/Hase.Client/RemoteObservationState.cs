using System.Collections.ObjectModel;

namespace Hase.Client;

/// <summary>
/// Represents one immutable normalized state of a remote observation stream.
/// </summary>
public sealed class RemoteObservationState
{
    private static readonly IReadOnlyDictionary<
        RemotePropertyTarget,
        RemotePropertyValue> EmptyPropertyValues =
        new ReadOnlyDictionary<
            RemotePropertyTarget,
            RemotePropertyValue>(
            new Dictionary<
                RemotePropertyTarget,
                RemotePropertyValue>());

    private RemoteObservationState()
    {
        Snapshot =
            null;
        LastSequence =
            null;
        PropertyValues =
            EmptyPropertyValues;
    }

    internal RemoteObservationState(
        RemoteRuntimeHostSnapshot snapshot,
        RemoteObservationSequence lastSequence,
        IDictionary<RemotePropertyTarget, RemotePropertyValue> propertyValues)
    {
        Snapshot =
            snapshot
            ?? throw new ArgumentNullException(
                nameof(snapshot));

        LastSequence =
            lastSequence
            ?? throw new ArgumentNullException(
                nameof(lastSequence));

        ArgumentNullException.ThrowIfNull(
            propertyValues);

        PropertyValues =
            new ReadOnlyDictionary<
                RemotePropertyTarget,
                RemotePropertyValue>(
                new Dictionary<
                    RemotePropertyTarget,
                    RemotePropertyValue>(
                    propertyValues));
    }

    /// <summary>
    /// Gets the empty state before the mandatory initial snapshot.
    /// </summary>
    public static RemoteObservationState Empty
    {
        get;
    } =
        new();

    /// <summary>
    /// Gets whether the mandatory initial snapshot has been applied.
    /// </summary>
    public bool IsInitialized =>
        Snapshot is not null;

    /// <summary>
    /// Gets the current immutable remote runtime-host snapshot after
    /// initialization.
    /// </summary>
    public RemoteRuntimeHostSnapshot? Snapshot
    {
        get;
    }

    /// <summary>
    /// Gets the last applied subscription-local sequence after initialization.
    /// </summary>
    public RemoteObservationSequence? LastSequence
    {
        get;
    }

    /// <summary>
    /// Gets the latest Property values learned from this observation stream.
    /// </summary>
    /// <remarks>
    /// API version 1 initial snapshots contain descriptors and connection
    /// state but no cached Property values. This collection is therefore empty
    /// at initialization and is populated by later Property observations.
    /// </remarks>
    public IReadOnlyDictionary<
        RemotePropertyTarget,
        RemotePropertyValue> PropertyValues
    {
        get;
    }
}

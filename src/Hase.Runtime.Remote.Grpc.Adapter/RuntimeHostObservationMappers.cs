namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Holds the fully composed version 1 runtime-host observation mapper roots.
/// </summary>
public sealed class RuntimeHostObservationMappers
{
    /// <summary>
    /// Initializes the observation mapper roots.
    /// </summary>
    public RuntimeHostObservationMappers(
        IObservationInitialSnapshotMapper initialSnapshotMapper,
        IRuntimeHostObservationMapper observationMapper)
    {
        InitialSnapshotMapper =
            initialSnapshotMapper
            ?? throw new ArgumentNullException(
                nameof(initialSnapshotMapper));
        ObservationMapper =
            observationMapper
            ?? throw new ArgumentNullException(
                nameof(observationMapper));
    }

    /// <summary>
    /// Gets the mandatory first-message mapper.
    /// </summary>
    public IObservationInitialSnapshotMapper InitialSnapshotMapper
    {
        get;
    }

    /// <summary>
    /// Gets the generation-scoped live-observation mapper.
    /// </summary>
    public IRuntimeHostObservationMapper ObservationMapper
    {
        get;
    }
}

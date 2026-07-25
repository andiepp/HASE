using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one authoritative observation-subscription boundary to the mandatory
/// first version 1 stream message.
/// </summary>
public sealed class ObservationInitialSnapshotMapper
    : IObservationInitialSnapshotMapper
{
    private readonly RuntimeHostSnapshotMapper snapshotMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public ObservationInitialSnapshotMapper(
        RuntimeHostSnapshotMapper snapshotMapper)
    {
        this.snapshotMapper =
            snapshotMapper
            ?? throw new ArgumentNullException(
                nameof(snapshotMapper));
    }

    /// <inheritdoc />
    public GrpcV1.ObserveResponse Map(
        Northbound.PublishedRuntimeHostSnapshot snapshot,
        Northbound.RuntimeHostObservationSequence snapshotSequence)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);
        ArgumentNullException.ThrowIfNull(
            snapshotSequence);

        return new GrpcV1.ObserveResponse
        {
            InitialSnapshot =
                new GrpcV1.ObservationInitialSnapshot
                {
                    Snapshot =
                        snapshotMapper.Map(
                            snapshot),
                    SnapshotSequence =
                        checked(
                            (ulong)snapshotSequence.Value)
                }
        };
    }
}

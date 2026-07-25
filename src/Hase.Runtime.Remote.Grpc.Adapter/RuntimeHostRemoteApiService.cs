using Grpc.Core;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Adapts the version 1 unary remote snapshot operation to the authoritative
/// northbound runtime-host snapshot provider.
/// </summary>
public sealed class RuntimeHostRemoteApiService
    : GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiBase
{
    private readonly Northbound.IRuntimeHostSnapshotProvider snapshotProvider;
    private readonly RuntimeHostSnapshotMapper snapshotMapper;

    /// <summary>
    /// Initializes the service adapter.
    /// </summary>
    public RuntimeHostRemoteApiService(
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        RuntimeHostSnapshotMapper snapshotMapper)
    {
        this.snapshotProvider =
            snapshotProvider
            ?? throw new ArgumentNullException(
                nameof(snapshotProvider));

        this.snapshotMapper =
            snapshotMapper
            ?? throw new ArgumentNullException(
                nameof(snapshotMapper));
    }

    /// <inheritdoc />
    public override Task<GrpcV1.GetSnapshotResponse> GetSnapshot(
        GrpcV1.GetSnapshotRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        Northbound.PublishedRuntimeHostSnapshot snapshot =
            snapshotProvider.Capture()
            ?? throw new InvalidOperationException(
                "The runtime-host snapshot provider returned null.");

        GrpcV1.GetSnapshotResponse response =
            snapshotMapper.Map(
                snapshot);

        return Task.FromResult(
            response);
    }
}

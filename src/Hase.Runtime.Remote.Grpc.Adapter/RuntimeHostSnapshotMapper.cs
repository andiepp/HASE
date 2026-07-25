using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps transport-independent runtime-host snapshots to the version 1 remote
/// contract.
/// </summary>
public sealed class RuntimeHostSnapshotMapper
{
    private readonly IRuntimeEndpointSnapshotMapper endpointSnapshotMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeHostSnapshotMapper(
        IRuntimeEndpointSnapshotMapper endpointSnapshotMapper)
    {
        this.endpointSnapshotMapper =
            endpointSnapshotMapper
            ?? throw new ArgumentNullException(
                nameof(endpointSnapshotMapper));
    }

    /// <summary>
    /// Maps one authoritative runtime-host snapshot.
    /// </summary>
    public GrpcV1.GetSnapshotResponse Map(
        Northbound.PublishedRuntimeHostSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        var response =
            new GrpcV1.GetSnapshotResponse
            {
                RuntimeHostId =
                    snapshot.RuntimeHostId.Value,
                ApiVersion =
                    new GrpcV1.RuntimeHostApiVersion
                    {
                        Major =
                            snapshot.ApiVersion.Major,
                        Minor =
                            snapshot.ApiVersion.Minor
                    }
            };

        foreach (Northbound.PublishedRuntimeEndpointSnapshot endpoint in
                 snapshot.Endpoints)
        {
            GrpcV1.PublishedRuntimeEndpointSnapshot mappedEndpoint =
                endpointSnapshotMapper.Map(
                    endpoint)
                ?? throw new InvalidOperationException(
                    "The endpoint snapshot mapper returned null.");

            response.Endpoints.Add(
                mappedEndpoint);
        }

        return response;
    }
}

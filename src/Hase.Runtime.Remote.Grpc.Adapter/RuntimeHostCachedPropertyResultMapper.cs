using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps cached northbound Property results to the version 1 remote contract.
/// </summary>
public sealed class RuntimeHostCachedPropertyResultMapper
    : IRuntimeHostCachedPropertyResultMapper
{
    private readonly IRuntimeHostPropertyOperationStatusMapper statusMapper;
    private readonly IPublishedRuntimePropertySnapshotMapper snapshotMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public RuntimeHostCachedPropertyResultMapper(
        IRuntimeHostPropertyOperationStatusMapper statusMapper,
        IPublishedRuntimePropertySnapshotMapper snapshotMapper)
    {
        this.statusMapper =
            statusMapper
            ?? throw new ArgumentNullException(
                nameof(statusMapper));

        this.snapshotMapper =
            snapshotMapper
            ?? throw new ArgumentNullException(
                nameof(snapshotMapper));
    }

    /// <inheritdoc />
    public GrpcV1.CachedPropertyResult Map(
        Northbound.RuntimeHostCachedPropertyResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        var mappedResult =
            new GrpcV1.CachedPropertyResult
            {
                Status =
                    statusMapper.Map(
                        result.Status)
            };

        if (result.Snapshot is not null)
        {
            mappedResult.Snapshot =
                snapshotMapper.Map(
                    result.Snapshot)
                ?? throw new InvalidOperationException(
                    "The published Property snapshot mapper returned null.");
        }

        if (result.Diagnostic is not null)
        {
            mappedResult.Diagnostic =
                result.Diagnostic;
        }

        return mappedResult;
    }
}

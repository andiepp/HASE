using Grpc.Core;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Adapts version 1 unary remote operations to the authoritative northbound
/// runtime-host services.
/// </summary>
public sealed class RuntimeHostRemoteApiService
    : GrpcV1.RuntimeHostRemoteApi.RuntimeHostRemoteApiBase
{
    private readonly Northbound.IRuntimeHostSnapshotProvider snapshotProvider;
    private readonly RuntimeHostSnapshotMapper snapshotMapper;
    private readonly Northbound.IRuntimeHostPropertyService? propertyService;
    private readonly IRuntimeHostPropertyTargetMapper? propertyTargetMapper;
    private readonly IRuntimeHostCachedPropertyResultMapper? cachedResultMapper;
    private readonly IRuntimeHostPropertyOperationResultMapper? operationResultMapper;

    /// <summary>
    /// Initializes the service adapter.
    /// </summary>
    public RuntimeHostRemoteApiService(
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        RuntimeHostSnapshotMapper snapshotMapper,
        Northbound.IRuntimeHostPropertyService? propertyService = null,
        IRuntimeHostPropertyTargetMapper? propertyTargetMapper = null,
        IRuntimeHostCachedPropertyResultMapper? cachedResultMapper = null,
        IRuntimeHostPropertyOperationResultMapper? operationResultMapper = null)
    {
        this.snapshotProvider =
            snapshotProvider
            ?? throw new ArgumentNullException(
                nameof(snapshotProvider));

        this.snapshotMapper =
            snapshotMapper
            ?? throw new ArgumentNullException(
                nameof(snapshotMapper));

        bool propertyAccessConfigured =
            propertyService is not null
            || propertyTargetMapper is not null
            || cachedResultMapper is not null
            || operationResultMapper is not null;

        if (!propertyAccessConfigured)
        {
            return;
        }

        this.propertyService =
            propertyService
            ?? throw new ArgumentNullException(
                nameof(propertyService));

        this.propertyTargetMapper =
            propertyTargetMapper
            ?? throw new ArgumentNullException(
                nameof(propertyTargetMapper));

        this.cachedResultMapper =
            cachedResultMapper
            ?? throw new ArgumentNullException(
                nameof(cachedResultMapper));

        this.operationResultMapper =
            operationResultMapper
            ?? throw new ArgumentNullException(
                nameof(operationResultMapper));
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

    /// <inheritdoc />
    public override Task<GrpcV1.CachedPropertyResult> ReadCachedProperty(
        GrpcV1.ReadCachedPropertyRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        Northbound.IRuntimeHostPropertyService propertyService =
            this.propertyService
            ?? throw new InvalidOperationException(
                "Cached Property access is not configured.");
        IRuntimeHostPropertyTargetMapper propertyTargetMapper =
            this.propertyTargetMapper
            ?? throw new InvalidOperationException(
                "Cached Property access is not configured.");
        IRuntimeHostCachedPropertyResultMapper cachedResultMapper =
            this.cachedResultMapper
            ?? throw new InvalidOperationException(
                "Cached Property access is not configured.");

        Northbound.RuntimeHostPropertyTarget target =
            propertyTargetMapper.Map(
                request.Target)
            ?? throw new InvalidOperationException(
                "The Property target mapper returned null.");

        Northbound.RuntimeHostCachedPropertyResult result =
            propertyService.GetCached(
                target)
            ?? throw new InvalidOperationException(
                "The runtime-host Property service returned null.");

        GrpcV1.CachedPropertyResult response =
            cachedResultMapper.Map(
                result)
            ?? throw new InvalidOperationException(
                "The cached Property result mapper returned null.");

        return Task.FromResult(
            response);
    }

    /// <inheritdoc />
    public override async Task<GrpcV1.PropertyOperationResult>
        ReadAuthoritativeProperty(
            GrpcV1.ReadAuthoritativePropertyRequest request,
            ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        Northbound.IRuntimeHostPropertyService propertyService =
            this.propertyService
            ?? throw new InvalidOperationException(
                "Authoritative Property access is not configured.");
        IRuntimeHostPropertyTargetMapper propertyTargetMapper =
            this.propertyTargetMapper
            ?? throw new InvalidOperationException(
                "Authoritative Property access is not configured.");
        IRuntimeHostPropertyOperationResultMapper operationResultMapper =
            this.operationResultMapper
            ?? throw new InvalidOperationException(
                "Authoritative Property access is not configured.");

        Northbound.RuntimeHostPropertyTarget target =
            propertyTargetMapper.Map(
                request.Target)
            ?? throw new InvalidOperationException(
                "The Property target mapper returned null.");

        Northbound.RuntimeHostPropertyOperationResult result =
            await propertyService.ReadAsync(
                target,
                context?.CancellationToken
                    ?? CancellationToken.None)
            ?? throw new InvalidOperationException(
                "The runtime-host Property service returned null.");

        return operationResultMapper.Map(
                result)
            ?? throw new InvalidOperationException(
                "The Property operation result mapper returned null.");
    }
}

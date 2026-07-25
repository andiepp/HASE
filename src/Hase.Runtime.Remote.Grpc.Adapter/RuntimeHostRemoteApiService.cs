using Grpc.Core;
using Microsoft.Extensions.Hosting;
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
    private readonly IRemoteValueMapper? remoteValueMapper;
    private readonly Northbound.IRuntimeHostCommandService? commandService;
    private readonly IRuntimeHostCommandTargetMapper? commandTargetMapper;
    private readonly IRuntimeHostCommandOperationResultMapper? commandResultMapper;
    private readonly Northbound.IRuntimeHostObservationService?
        observationService;
    private readonly IObservationInitialSnapshotMapper? initialSnapshotMapper;
    private readonly IRuntimeHostObservationMapper? observationMapper;
    private readonly IHostApplicationLifetime? applicationLifetime;
    private readonly IRuntimeHostClientPrincipalProvider? principalProvider;
    private readonly IRuntimeHostRemoteAuthorizationGate? authorizationGate;

    /// <summary>
    /// Initializes the service adapter.
    /// </summary>
    public RuntimeHostRemoteApiService(
        Northbound.IRuntimeHostSnapshotProvider snapshotProvider,
        RuntimeHostSnapshotMapper snapshotMapper,
        Northbound.IRuntimeHostPropertyService? propertyService = null,
        IRuntimeHostPropertyTargetMapper? propertyTargetMapper = null,
        IRuntimeHostCachedPropertyResultMapper? cachedResultMapper = null,
        IRuntimeHostPropertyOperationResultMapper? operationResultMapper = null,
        IRemoteValueMapper? remoteValueMapper = null,
        Northbound.IRuntimeHostCommandService? commandService = null,
        IRuntimeHostCommandTargetMapper? commandTargetMapper = null,
        IRuntimeHostCommandOperationResultMapper? commandResultMapper = null,
        Northbound.IRuntimeHostObservationService? observationService = null,
        IObservationInitialSnapshotMapper? initialSnapshotMapper = null,
        IRuntimeHostObservationMapper? observationMapper = null,
        IHostApplicationLifetime? applicationLifetime = null,
        IRuntimeHostClientPrincipalProvider? principalProvider = null,
        IRuntimeHostRemoteAuthorizationGate? authorizationGate = null)
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

        bool commandAccessConfigured =
            commandService is not null
            || commandTargetMapper is not null
            || commandResultMapper is not null;
        bool observationConfigured =
            observationService is not null
            || initialSnapshotMapper is not null
            || observationMapper is not null;
        bool authorizationConfigured =
            principalProvider is not null
            || authorizationGate is not null;

        if (propertyAccessConfigured)
        {
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

        if (commandAccessConfigured)
        {
            this.commandService =
                commandService
                ?? throw new ArgumentNullException(
                    nameof(commandService));

            this.commandTargetMapper =
                commandTargetMapper
                ?? throw new ArgumentNullException(
                    nameof(commandTargetMapper));

            this.commandResultMapper =
                commandResultMapper
                ?? throw new ArgumentNullException(
                    nameof(commandResultMapper));
        }

        if (propertyAccessConfigured
            || commandAccessConfigured)
        {
            this.remoteValueMapper =
                remoteValueMapper
                ?? throw new ArgumentNullException(
                    nameof(remoteValueMapper));
        }

        if (observationConfigured)
        {
            this.observationService =
                observationService
                ?? throw new ArgumentNullException(
                    nameof(observationService));
            this.initialSnapshotMapper =
                initialSnapshotMapper
                ?? throw new ArgumentNullException(
                    nameof(initialSnapshotMapper));
            this.observationMapper =
                observationMapper
                ?? throw new ArgumentNullException(
                    nameof(observationMapper));
        }

        if (authorizationConfigured)
        {
            this.principalProvider =
                principalProvider
                ?? throw new ArgumentNullException(
                    nameof(principalProvider));
            this.authorizationGate =
                authorizationGate
                ?? throw new ArgumentNullException(
                    nameof(authorizationGate));
        }

        this.applicationLifetime =
            applicationLifetime;
    }

    /// <inheritdoc />
    public override Task<GrpcV1.GetSnapshotResponse> GetSnapshot(
        GrpcV1.GetSnapshotRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        AuthorizeOperation(
            context,
            RuntimeHostRemoteOperation.GetSnapshot);

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

        AuthorizeOperation(
            context,
            RuntimeHostRemoteOperation.ReadCachedProperty);

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

        AuthorizeOperation(
            context,
            RuntimeHostRemoteOperation.ReadAuthoritativeProperty);

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

    /// <inheritdoc />
    public override async Task<GrpcV1.PropertyOperationResult> WriteProperty(
        GrpcV1.WritePropertyRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        AuthorizeOperation(
            context,
            RuntimeHostRemoteOperation.WriteProperty);

        Northbound.IRuntimeHostPropertyService propertyService =
            this.propertyService
            ?? throw new InvalidOperationException(
                "Property write access is not configured.");
        IRuntimeHostPropertyTargetMapper propertyTargetMapper =
            this.propertyTargetMapper
            ?? throw new InvalidOperationException(
                "Property write access is not configured.");
        IRuntimeHostPropertyOperationResultMapper operationResultMapper =
            this.operationResultMapper
            ?? throw new InvalidOperationException(
                "Property write access is not configured.");
        IRemoteValueMapper remoteValueMapper =
            this.remoteValueMapper
            ?? throw new InvalidOperationException(
                "Property write access is not configured.");

        Northbound.RuntimeHostPropertyTarget target =
            propertyTargetMapper.Map(
                request.Target)
            ?? throw new InvalidOperationException(
                "The Property target mapper returned null.");

        object? requestedValue =
            request.RequestedValue is null
                ? null
                : remoteValueMapper.MapToClr(
                    request.RequestedValue);

        Northbound.RuntimeHostPropertyOperationResult result =
            await propertyService.WriteAsync(
                target,
                requestedValue,
                context?.CancellationToken
                    ?? CancellationToken.None)
            ?? throw new InvalidOperationException(
                "The runtime-host Property service returned null.");

        return operationResultMapper.Map(
                result)
            ?? throw new InvalidOperationException(
                "The Property operation result mapper returned null.");
    }

    /// <inheritdoc />
    public override async Task<GrpcV1.CommandOperationResult> ExecuteCommand(
        GrpcV1.ExecuteCommandRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        AuthorizeOperation(
            context,
            RuntimeHostRemoteOperation.ExecuteCommand);

        Northbound.IRuntimeHostCommandService commandService =
            this.commandService
            ?? throw new InvalidOperationException(
                "Command execution is not configured.");
        IRuntimeHostCommandTargetMapper commandTargetMapper =
            this.commandTargetMapper
            ?? throw new InvalidOperationException(
                "Command execution is not configured.");
        IRuntimeHostCommandOperationResultMapper commandResultMapper =
            this.commandResultMapper
            ?? throw new InvalidOperationException(
                "Command execution is not configured.");
        IRemoteValueMapper remoteValueMapper =
            this.remoteValueMapper
            ?? throw new InvalidOperationException(
                "Command execution is not configured.");

        Northbound.RuntimeHostCommandTarget target =
            commandTargetMapper.Map(
                request.Target)
            ?? throw new InvalidOperationException(
                "The Command target mapper returned null.");

        object? argument =
            request.Argument is null
                ? null
                : remoteValueMapper.MapToClr(
                    request.Argument);

        Northbound.RuntimeHostCommandOperationResult result =
            await commandService.ExecuteAsync(
                target,
                argument,
                context?.CancellationToken
                    ?? CancellationToken.None)
            ?? throw new InvalidOperationException(
                "The runtime-host Command service returned null.");

        return commandResultMapper.Map(
                result)
            ?? throw new InvalidOperationException(
                "The Command operation result mapper returned null.");
    }

    /// <inheritdoc />
    public override async Task Observe(
        GrpcV1.ObserveRequest request,
        IServerStreamWriter<GrpcV1.ObserveResponse> responseStream,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(
            request);
        ArgumentNullException.ThrowIfNull(
            responseStream);

        AuthorizeOperation(
            context,
            RuntimeHostRemoteOperation.Observe);

        Northbound.IRuntimeHostObservationService observationService =
            this.observationService
            ?? throw new InvalidOperationException(
                "Runtime-host observation is not configured.");
        IObservationInitialSnapshotMapper initialSnapshotMapper =
            this.initialSnapshotMapper
            ?? throw new InvalidOperationException(
                "Runtime-host observation is not configured.");
        IRuntimeHostObservationMapper observationMapper =
            this.observationMapper
            ?? throw new InvalidOperationException(
                "Runtime-host observation is not configured.");
        CancellationToken requestCancellationToken =
            context?.CancellationToken
            ?? CancellationToken.None;
        CancellationTokenSource? linkedCancellationSource =
            applicationLifetime is null
                ? null
                : CancellationTokenSource.CreateLinkedTokenSource(
                    requestCancellationToken,
                    applicationLifetime.ApplicationStopping);
        using (linkedCancellationSource)
        {
            CancellationToken cancellationToken =
                linkedCancellationSource?.Token
                ?? requestCancellationToken;

            Northbound.RuntimeHostObservationSubscription subscription =
                await observationService.OpenSubscriptionAsync(
                    new Northbound.RuntimeHostObservationSubscriptionOptions(),
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "The runtime-host observation service returned null.");

            await using (subscription)
            {
                GrpcV1.ObserveResponse initialResponse =
                    initialSnapshotMapper.Map(
                        subscription.InitialSnapshot,
                        subscription.SnapshotSequence)
                    ?? throw new InvalidOperationException(
                        "The initial observation snapshot mapper returned null.");

                await responseStream.WriteAsync(
                    initialResponse);

                try
                {
                    await foreach (
                        Northbound.RuntimeHostObservation observation
                        in subscription.ReadAllAsync(
                            cancellationToken))
                    {
                        GrpcV1.ObserveResponse response =
                            observationMapper.Map(
                                observation)
                            ?? throw new InvalidOperationException(
                                "The runtime-host observation mapper returned null.");

                        await responseStream.WriteAsync(
                            response);
                    }
                }
                catch (Northbound.RuntimeHostObservationGapException)
                {
                    throw new RpcException(
                        new Status(
                            StatusCode.DataLoss,
                            "The observation stream has a gap. "
                            + "Open a new subscription."));
                }
            }
        }
    }

    private void AuthorizeOperation(
        ServerCallContext? context,
        RuntimeHostRemoteOperation operation)
    {
        if (principalProvider is null
            || authorizationGate is null)
        {
            return;
        }

        RuntimeHostClientPrincipal principal =
            principalProvider.GetPrincipal(
                context)
            ?? throw new InvalidOperationException(
                "The client-principal provider returned null.");

        RuntimeHostAuthorizationDecision decision =
            authorizationGate.Authorize(
                principal,
                operation)
            ?? throw new InvalidOperationException(
                "The remote authorization gate returned null.");

        if (!decision.IsAllowed)
        {
            throw new RpcException(
                new Status(
                    StatusCode.PermissionDenied,
                    "The authenticated client is not authorized "
                    + "to perform this operation."));
        }
    }
}

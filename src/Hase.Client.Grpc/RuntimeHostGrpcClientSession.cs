using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Hase.Runtime.Remote.Grpc.Hosting;
using Hase.Client.Media;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc;

/// <summary>
/// Owns one connected private-network runtime-host client session.
/// </summary>
/// <remarks>
/// One session instance is single-use. It owns the ADR-0032 deployment,
/// observation call, connected-session cancellation boundary, and latest
/// immutable normalized state.
/// </remarks>
public sealed class RuntimeHostGrpcClientSession
    : IRuntimeHostGrpcRecoverableSession,
      IRuntimeHostPropertyReader,
      IRuntimeHostPropertyWriter,
      IRuntimeHostCommandExecutor,
      IRuntimeHostEventSource,
      IRuntimeHostDiagnosticSource,
      IRuntimeHostMediaControlClient
{
    public event EventHandler<RemoteEventOccurredEventArgs>? EventOccurred;
    public event EventHandler<RemoteRuntimeDiagnosticObservedEventArgs>?
        DiagnosticObserved;
    public event EventHandler<RemoteRuntimeDiagnosticStreamFaultedEventArgs>?
        DiagnosticStreamFaulted;

    private readonly object gate =
        new();
    private readonly Func<
        CancellationToken,
        ValueTask<IRuntimeHostGrpcSessionResources>> resourcesFactory;
    private readonly RemoteObservationReducer reducer =
        new();
    private readonly Channel<RemoteObservationState> stateChanges =
        Channel.CreateUnbounded<RemoteObservationState>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations =
                    false,
                SingleReader =
                    false,
                SingleWriter =
                    true
            });
    private readonly CancellationTokenSource sessionCancellation =
        new();
    private RuntimeHostClientSessionStatus status =
        new(
            RuntimeHostClientSessionState.Disconnected);
    private RemoteObservationState? currentState;
    private IRuntimeHostGrpcSessionResources? resources;
    private Task? observationTask;
    private Task? diagnosticTask;
    private bool started;
    private bool disposed;

    /// <summary>
    /// Initializes one session from externally provisioned ADR-0032 client
    /// options.
    /// </summary>
    public RuntimeHostGrpcClientSession(
        RuntimeHostPrivateNetworkClientOptions options)
        : this(
            CreateResourcesFactory(
                options))
    {
    }

    /// <summary>
    /// Creates one unconnected session for the explicitly labeled
    /// certificate-free loopback development profile.
    /// </summary>
    public RuntimeHostGrpcClientSession(
        RuntimeHostDevelopmentLoopbackClientOptions options)
        : this(
            CreateResourcesFactory(
                options))
    {
    }

    internal RuntimeHostGrpcClientSession(
        Func<
            CancellationToken,
            ValueTask<IRuntimeHostGrpcSessionResources>> resourcesFactory)
    {
        this.resourcesFactory =
            resourcesFactory
            ?? throw new ArgumentNullException(
                nameof(resourcesFactory));
    }

    /// <summary>
    /// Gets the current normalized session status.
    /// </summary>
    public RuntimeHostClientSessionStatus Status
    {
        get
        {
            lock (gate)
            {
                return status;
            }
        }
    }

    /// <summary>
    /// Gets the latest authoritative normalized observation state, when the
    /// session has established one.
    /// </summary>
    public RemoteObservationState? CurrentState
    {
        get
        {
            lock (gate)
            {
                return currentState;
            }
        }
    }

    /// <summary>
    /// Gets the task representing later observation consumption.
    /// </summary>
    public Task Completion
    {
        get
        {
            lock (gate)
            {
                return observationTask
                    ?? Task.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Establishes the session and waits for its authoritative initial
    /// snapshot.
    /// </summary>
    public async Task ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            if (started)
            {
                throw new InvalidOperationException(
                    "A runtime-host gRPC client session can be connected only "
                    + "once.");
            }

            started =
                true;
            status =
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Connecting);
        }

        IRuntimeHostGrpcSessionResources? createdResources =
            null;

        try
        {
            using var connectionCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    sessionCancellation.Token);

            createdResources =
                await resourcesFactory(
                        connectionCancellation.Token)
                    .ConfigureAwait(
                        false);

            if (createdResources is null)
            {
                throw new InvalidOperationException(
                    "The runtime-host gRPC session resources factory returned "
                    + "null.");
            }

            RemoteObservationInitialSnapshot initialSnapshot =
                await createdResources.ObservationStream
                    .ReadInitialSnapshotAsync(
                        connectionCancellation.Token)
                    .ConfigureAwait(
                        false);
            RemoteObservationState initialState =
                reducer.Initialize(
                    RemoteObservationState.Empty,
                    initialSnapshot);
            RemoteRuntimeHostSnapshot snapshot =
                initialState.Snapshot
                ?? throw new InvalidDataException(
                    "The initialized observation state has no runtime-host "
                    + "snapshot.");

            lock (gate)
            {
                resources =
                    createdResources;
                currentState =
                    initialState;
                status =
                    new RuntimeHostClientSessionStatus(
                        RuntimeHostClientSessionState.Connected,
                        snapshot.RuntimeHostId,
                        snapshot.ApiVersion);
                stateChanges.Writer.TryWrite(
                    initialState);
                observationTask =
                    ConsumeObservationsAsync(
                        createdResources,
                        initialState,
                        sessionCancellation.Token);
                diagnosticTask =
                    ConsumeDiagnosticsAsync(
                        createdResources,
                        sessionCancellation.Token);
            }
        }
        catch (Exception exception)
        {
            if (createdResources is not null)
            {
                await createdResources.DisposeAsync()
                    .ConfigureAwait(
                        false);
            }

            lock (gate)
            {
                resources =
                    null;
                status =
                    new RuntimeHostClientSessionStatus(
                        RuntimeHostClientSessionState.Faulted);
            }

            stateChanges.Writer.TryComplete(
                exception);

            throw;
        }
    }

    /// <summary>
    /// Reads every normalized state published by the connected session,
    /// beginning with the authoritative initial state.
    /// </summary>
    public async IAsyncEnumerable<RemoteObservationState>
        ReadStateChangesAsync(
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
    {
        await foreach (RemoteObservationState state
            in stateChanges.Reader.ReadAllAsync(
                    cancellationToken)
                .ConfigureAwait(
                    false))
        {
            yield return state;
        }
    }

    public async Task<RemotePropertyOperationResult> ReadPropertyAsync(
        RemotePropertyTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        IRuntimeHostGrpcSessionResources activeResources;

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            if (status.State
                != RuntimeHostClientSessionState.Connected)
            {
                throw new InvalidOperationException(
                    "An authoritative Property read requires a connected "
                    + "runtime-host session.");
            }

            activeResources =
                resources
                ?? throw new InvalidOperationException(
                    "The connected runtime-host session has no active "
                    + "resources.");
        }

        var mapper =
            new RuntimeHostGrpcPropertyMapper();
        using var operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                sessionCancellation.Token);

        GrpcV1.PropertyOperationResult result =
            await activeResources.PropertyClient.ReadPropertyAsync(
                    mapper.MapRequest(
                        target),
                    operationCancellation.Token)
                .ConfigureAwait(
                    false);

        return mapper.MapResult(
            result);
    }

    public async Task<RemotePropertyOperationResult> WritePropertyAsync(
        RemotePropertyTarget target,
        RemoteValue requestedValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            target);
        ArgumentNullException.ThrowIfNull(
            requestedValue);

        IRuntimeHostGrpcSessionResources activeResources =
            GetConnectedResources(
                "write");
        var mapper =
            new RuntimeHostGrpcPropertyMapper();
        using var operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                sessionCancellation.Token);

        GrpcV1.PropertyOperationResult result =
            await activeResources.PropertyClient.WritePropertyAsync(
                    mapper.MapWriteRequest(
                        target,
                        requestedValue),
                    operationCancellation.Token)
                .ConfigureAwait(
                    false);

        return mapper.MapResult(
            result);
    }

    public async Task<RemoteCommandOperationResult> ExecuteCommandAsync(
        RemoteCommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        IRuntimeHostGrpcSessionResources activeResources =
            GetConnectedResources(
                "Command execution");
        var mapper =
            new RuntimeHostGrpcCommandMapper();
        using var operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                sessionCancellation.Token);

        GrpcV1.CommandOperationResult result =
            await activeResources.CommandClient.ExecuteAsync(
                    mapper.MapRequest(
                        request),
                    operationCancellation.Token)
                .ConfigureAwait(
                    false);

        return mapper.MapResult(
            result);
    }

    public Task<IReadOnlyList<RemoteMediaSourceCapability>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteMediaAsync(
            (client, token) => client.GetCapabilitiesAsync(token),
            cancellationToken);

    public Task<RemoteMediaStartResult> StartAsync(
        RemoteMediaSourceTarget target,
        bool includeAudio,
        CancellationToken cancellationToken = default) =>
        ExecuteMediaAsync(
            (client, token) => client.StartAsync(target, includeAudio, token),
            cancellationToken);

    public Task<RemoteMediaExchangeResult> ExchangeAsync(
        string sessionId,
        uint acknowledgedDeliverySequence,
        RemoteMediaNegotiationMessage? submittedMessage,
        CancellationToken cancellationToken = default) =>
        ExecuteMediaAsync(
            (client, token) => client.ExchangeAsync(
                sessionId, acknowledgedDeliverySequence, submittedMessage, token),
            cancellationToken);

    public Task<RemoteMediaStatusResult> GetStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        ExecuteMediaAsync(
            (client, token) => client.GetStatusAsync(sessionId, token),
            cancellationToken);

    public Task<RemoteMediaStopResult> StopAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        ExecuteMediaAsync(
            (client, token) => client.StopAsync(sessionId, token),
            cancellationToken);

    private async Task<TResult> ExecuteMediaAsync<TResult>(
        Func<IRuntimeHostMediaControlClient, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        IRuntimeHostGrpcSessionResources activeResources =
            GetConnectedResources("media operation");
        using var operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, sessionCancellation.Token);
        return await operation(activeResources.MediaClient,
            operationCancellation.Token).ConfigureAwait(false);
    }

    private IRuntimeHostGrpcSessionResources GetConnectedResources(
        string operationName)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            if (status.State
                != RuntimeHostClientSessionState.Connected)
            {
                throw new InvalidOperationException(
                    $"An authoritative Property {operationName} requires a "
                    + "connected runtime-host session.");
            }

            return resources
                ?? throw new InvalidOperationException(
                    "The connected runtime-host session has no active "
                    + "resources.");
        }
    }

    /// <summary>
    /// Disconnects and disposes the connected resources in ownership order.
    /// </summary>
    public async Task DisconnectAsync()
    {
        Task? task;
        Task? diagnostics;

        lock (gate)
        {
            if (!started
                || status.State
                    == RuntimeHostClientSessionState.Disconnected)
            {
                return;
            }

            if (status.State
                != RuntimeHostClientSessionState.Faulted)
            {
                RemoteRuntimeHostSnapshot? snapshot =
                    currentState?.Snapshot;
                status =
                    new RuntimeHostClientSessionStatus(
                        RuntimeHostClientSessionState.Disconnecting,
                        snapshot?.RuntimeHostId,
                        snapshot?.ApiVersion);
            }

            task =
                observationTask;
            diagnostics =
                diagnosticTask;
        }

        sessionCancellation.Cancel();

        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(
                    false);
            }
            catch (OperationCanceledException)
                when (sessionCancellation.IsCancellationRequested)
            {
            }
            catch
            {
                // Completion retains the original observation failure. An
                // explicit disconnect still performs orderly cleanup.
            }
        }
        else
        {
            await DisposeResourcesAsync()
                .ConfigureAwait(
                    false);
        }

        if (diagnostics is not null)
        {
            try
            {
                await diagnostics.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (sessionCancellation.IsCancellationRequested)
            {
            }
            catch
            {
                // The failure was already projected through
                // DiagnosticStreamFaulted.
            }
        }

        lock (gate)
        {
            currentState =
                null;
            status =
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Disconnected);
        }

        stateChanges.Writer.TryComplete();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed =
                true;
        }

        await DisconnectAsync()
            .ConfigureAwait(
                false);
        sessionCancellation.Dispose();
    }

    private async Task ConsumeObservationsAsync(
        IRuntimeHostGrpcSessionResources ownedResources,
        RemoteObservationState initialState,
        CancellationToken cancellationToken)
    {
        RemoteObservationState state =
            initialState;

        try
        {
            await foreach (RemoteRuntimeHostObservation observation
                in ownedResources.ObservationStream
                    .ReadObservationsAsync(
                        cancellationToken)
                    .WithCancellation(
                        cancellationToken)
                    .ConfigureAwait(
                        false))
            {
                state =
                    reducer.Apply(
                        state,
                        observation);

                lock (gate)
                {
                    currentState =
                        state;
                }

                if (observation.Payload
                    is RemoteEventOccurredObservationPayload)
                {
                    EventOccurred?.Invoke(
                        this,
                        new RemoteEventOccurredEventArgs(
                            observation));
                }

                await stateChanges.Writer.WriteAsync(
                        state,
                        cancellationToken)
                    .ConfigureAwait(
                        false);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                SetFaultedStatus();
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetFaultedStatus();
            stateChanges.Writer.TryComplete(
                exception);
            throw;
        }
        finally
        {
            await DisposeResourcesAsync()
                .ConfigureAwait(
                    false);

            if (!cancellationToken.IsCancellationRequested)
            {
                stateChanges.Writer.TryComplete();
            }
        }
    }

    private async Task ConsumeDiagnosticsAsync(
        IRuntimeHostGrpcSessionResources ownedResources,
        CancellationToken cancellationToken)
    {
        TimeSpan[] recoverySchedule =
        [
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10)
        ];
        int recoveryAttempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using IRemoteRuntimeDiagnosticStream stream =
                    ownedResources.CreateDiagnosticStream();
                await foreach (RemoteRuntimeDiagnosticObservation observation
                    in stream.ReadAsync(cancellationToken)
                        .WithCancellation(cancellationToken)
                        .ConfigureAwait(false))
                {
                    DiagnosticObserved?.Invoke(
                        this,
                        new RemoteRuntimeDiagnosticObservedEventArgs(observation));
                }
                if (!cancellationToken.IsCancellationRequested)
                {
                    throw new IOException(
                        "The projected diagnostic stream ended.");
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
                when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                DiagnosticStreamFaulted?.Invoke(
                    this,
                    new RemoteRuntimeDiagnosticStreamFaultedEventArgs(
                        ClassifyDiagnosticStreamFailure(exception),
                        exception));
                if (!ShouldRecoverDiagnosticStream(exception)
                    || recoveryAttempt >= recoverySchedule.Length)
                {
                    return;
                }

                TimeSpan delay = recoverySchedule[recoveryAttempt++];
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private static bool ShouldRecoverDiagnosticStream(Exception exception) =>
        exception is not global::Grpc.Core.RpcException
        {
            StatusCode: global::Grpc.Core.StatusCode.PermissionDenied
                or global::Grpc.Core.StatusCode.Unauthenticated
                or global::Grpc.Core.StatusCode.InvalidArgument
                or global::Grpc.Core.StatusCode.Unimplemented
        };

    private static RemoteRuntimeDiagnosticStreamFailureKind
        ClassifyDiagnosticStreamFailure(Exception exception) =>
        exception switch
        {
            global::Grpc.Core.RpcException
            {
                StatusCode: global::Grpc.Core.StatusCode.PermissionDenied
            } => RemoteRuntimeDiagnosticStreamFailureKind.AuthorizationDenied,
            global::Grpc.Core.RpcException
            {
                StatusCode: global::Grpc.Core.StatusCode.Unauthenticated
            } => RemoteRuntimeDiagnosticStreamFailureKind.AuthenticationFailed,
            global::Grpc.Core.RpcException
            {
                StatusCode: global::Grpc.Core.StatusCode.Unavailable
                    or global::Grpc.Core.StatusCode.DeadlineExceeded
            } => RemoteRuntimeDiagnosticStreamFailureKind.TransportUnavailable,
            InvalidDataException exceptionWithGap
                when exceptionWithGap.Message.Contains(
                    "gap",
                    StringComparison.OrdinalIgnoreCase) =>
                RemoteRuntimeDiagnosticStreamFailureKind.Gap,
            InvalidDataException =>
                RemoteRuntimeDiagnosticStreamFailureKind.InvalidRemoteContract,
            _ => RemoteRuntimeDiagnosticStreamFailureKind.Unknown
        };

    private async ValueTask DisposeResourcesAsync()
    {
        IRuntimeHostGrpcSessionResources? resourcesToDispose;

        lock (gate)
        {
            resourcesToDispose =
                resources;
            resources =
                null;
        }

        if (resourcesToDispose is not null)
        {
            await resourcesToDispose.DisposeAsync()
                .ConfigureAwait(
                    false);
        }
    }

    private void SetFaultedStatus()
    {
        lock (gate)
        {
            RemoteRuntimeHostSnapshot? snapshot =
                currentState?.Snapshot;
            status =
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Faulted,
                    snapshot?.RuntimeHostId,
                    snapshot?.ApiVersion);
        }
    }

    private static Func<
        CancellationToken,
        ValueTask<IRuntimeHostGrpcSessionResources>> CreateResourcesFactory(
        RuntimeHostPrivateNetworkClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        return _ =>
            ValueTask.FromResult<IRuntimeHostGrpcSessionResources>(
                RuntimeHostPrivateNetworkSessionResources.Create(
                    options));
    }

    private static Func<
        CancellationToken,
        ValueTask<IRuntimeHostGrpcSessionResources>> CreateResourcesFactory(
        RuntimeHostDevelopmentLoopbackClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        return _ =>
            ValueTask.FromResult<IRuntimeHostGrpcSessionResources>(
                RuntimeHostDevelopmentLoopbackSessionResources.Create(
                    options));
    }
}

internal interface IRuntimeHostGrpcSessionResources
    : IAsyncDisposable
{
    IRemoteObservationStream ObservationStream
    {
        get;
    }

    IRuntimeHostGrpcPropertyClient PropertyClient
    {
        get;
    }

    IRuntimeHostGrpcCommandClient CommandClient
    {
        get;
    }

    IRuntimeHostMediaControlClient MediaClient =>
        throw new NotSupportedException(
            "The session resources do not provide media control.");

    IRemoteRuntimeDiagnosticStream CreateDiagnosticStream();
}

internal interface IRuntimeHostGrpcRecoverableSession
    : IAsyncDisposable
{
    RuntimeHostClientSessionStatus Status
    {
        get;
    }

    RemoteObservationState? CurrentState
    {
        get;
    }

    Task Completion
    {
        get;
    }

    Task ConnectAsync(
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<RemoteObservationState> ReadStateChangesAsync(
        CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}

internal sealed class RuntimeHostPrivateNetworkSessionResources
    : IRuntimeHostGrpcSessionResources
{
    private readonly RuntimeHostPrivateNetworkClientDeployment deployment;
    private readonly RuntimeHostGrpcObservationStream observationStream;
    private readonly RuntimeHostGrpcPropertyClient propertyClient;
    private readonly RuntimeHostGrpcCommandClient commandClient;
    private readonly RuntimeHostGrpcMediaControlClient mediaClient;
    private bool disposed;

    private RuntimeHostPrivateNetworkSessionResources(
        RuntimeHostPrivateNetworkClientDeployment deployment,
        RuntimeHostGrpcObservationStream observationStream,
        RuntimeHostGrpcPropertyClient propertyClient,
        RuntimeHostGrpcCommandClient commandClient,
        RuntimeHostGrpcMediaControlClient mediaClient)
    {
        this.deployment =
            deployment;
        this.observationStream =
            observationStream;
        this.propertyClient =
            propertyClient;
        this.commandClient =
            commandClient;
        this.mediaClient = mediaClient;
    }

    public IRemoteObservationStream ObservationStream =>
        observationStream;

    public IRuntimeHostGrpcPropertyClient PropertyClient =>
        propertyClient;

    public IRuntimeHostGrpcCommandClient CommandClient =>
        commandClient;

    public IRuntimeHostMediaControlClient MediaClient => mediaClient;

    public IRemoteRuntimeDiagnosticStream CreateDiagnosticStream() =>
        new RuntimeHostGrpcDiagnosticStream(deployment.Client.Client);

    public static RuntimeHostPrivateNetworkSessionResources Create(
        RuntimeHostPrivateNetworkClientOptions options)
    {
        RuntimeHostPrivateNetworkClientDeployment deployment =
            RuntimeHostPrivateNetworkClientDeployment.Create(
                options);

        try
        {
            return new RuntimeHostPrivateNetworkSessionResources(
                deployment,
                new RuntimeHostGrpcObservationStream(
                    deployment.Client.Client),
                new RuntimeHostGrpcPropertyClient(
                    deployment.Client.Client),
                new RuntimeHostGrpcCommandClient(
                    deployment.Client.Client),
                new RuntimeHostGrpcMediaControlClient(
                    deployment.Client.MediaClient));
        }
        catch
        {
            deployment.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        try
        {
            await observationStream.DisposeAsync()
                .ConfigureAwait(false);
        }
        finally
        {
            deployment.Dispose();
        }
    }
}

internal sealed class RuntimeHostDevelopmentLoopbackSessionResources
    : IRuntimeHostGrpcSessionResources
{
    private readonly RuntimeHostDevelopmentLoopbackGrpcClient client;
    private readonly RuntimeHostGrpcObservationStream observationStream;
    private readonly RuntimeHostGrpcPropertyClient propertyClient;
    private readonly RuntimeHostGrpcCommandClient commandClient;
    private readonly RuntimeHostGrpcMediaControlClient mediaClient;
    private bool disposed;

    private RuntimeHostDevelopmentLoopbackSessionResources(
        RuntimeHostDevelopmentLoopbackGrpcClient client,
        RuntimeHostGrpcObservationStream observationStream,
        RuntimeHostGrpcPropertyClient propertyClient,
        RuntimeHostGrpcCommandClient commandClient,
        RuntimeHostGrpcMediaControlClient mediaClient)
    {
        this.client =
            client;
        this.observationStream =
            observationStream;
        this.propertyClient =
            propertyClient;
        this.commandClient =
            commandClient;
        this.mediaClient =
            mediaClient;
    }

    public IRemoteObservationStream ObservationStream =>
        observationStream;

    public IRuntimeHostGrpcPropertyClient PropertyClient =>
        propertyClient;

    public IRuntimeHostGrpcCommandClient CommandClient =>
        commandClient;

    public IRuntimeHostMediaControlClient MediaClient =>
        mediaClient;

    public IRemoteRuntimeDiagnosticStream CreateDiagnosticStream() =>
        new RuntimeHostGrpcDiagnosticStream(client.Client);

    public static RuntimeHostDevelopmentLoopbackSessionResources Create(
        RuntimeHostDevelopmentLoopbackClientOptions options)
    {
        RuntimeHostDevelopmentLoopbackGrpcClient client =
            RuntimeHostDevelopmentLoopbackGrpcClient.Create(
                options);

        try
        {
            return new RuntimeHostDevelopmentLoopbackSessionResources(
                client,
                new RuntimeHostGrpcObservationStream(
                    client.Client),
                new RuntimeHostGrpcPropertyClient(
                    client.Client),
                new RuntimeHostGrpcCommandClient(
                    client.Client),
                new RuntimeHostGrpcMediaControlClient(
                    client.MediaClient));
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed =
            true;

        try
        {
            await observationStream.DisposeAsync()
                .ConfigureAwait(false);
        }
        finally
        {
            client.Dispose();
        }
    }
}

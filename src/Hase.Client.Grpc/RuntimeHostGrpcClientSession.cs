using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Hase.Runtime.Remote.Grpc.Hosting;

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
    : IRuntimeHostGrpcRecoverableSession
{
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

    /// <summary>
    /// Disconnects and disposes the connected resources in ownership order.
    /// </summary>
    public async Task DisconnectAsync()
    {
        Task? task;

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
}

internal interface IRuntimeHostGrpcSessionResources
    : IAsyncDisposable
{
    IRemoteObservationStream ObservationStream
    {
        get;
    }
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
    private bool disposed;

    private RuntimeHostPrivateNetworkSessionResources(
        RuntimeHostPrivateNetworkClientDeployment deployment,
        RuntimeHostGrpcObservationStream observationStream)
    {
        this.deployment =
            deployment;
        this.observationStream =
            observationStream;
    }

    public IRemoteObservationStream ObservationStream =>
        observationStream;

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
                    deployment.Client.Client));
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
                .ConfigureAwait(
                    false);
        }
        finally
        {
            deployment.Dispose();
        }
    }
}

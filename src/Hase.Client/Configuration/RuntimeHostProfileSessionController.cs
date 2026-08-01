namespace Hase.Client.Configuration;

/// <summary>Owns one independent runtime-host profile session.</summary>
public sealed class RuntimeHostProfileSessionController : IRuntimeHostProfileSessionController
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly RuntimeHostProfile profile;
    private readonly IRuntimeHostProfileClientSessionFactory factory;
    private IRuntimeHostClientSession? session;
    private CancellationTokenSource? cancellation;
    private Task? sessionTask;
    private bool disposed;
    private RuntimeHostProfileSessionSnapshot snapshot;

    public RuntimeHostProfileSessionController(
        RuntimeHostProfile profile,
        IRuntimeHostProfileClientSessionFactory factory)
    {
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        snapshot = CreateSnapshot(new(RuntimeHostClientSessionState.Disconnected));
    }

    public event EventHandler? SnapshotChanged;
    public RuntimeHostProfileSessionSnapshot Snapshot => Volatile.Read(ref snapshot);

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (session is not null)
                throw new InvalidOperationException("A runtime-host profile session is already active.");

            Publish(CreateSnapshot(new(RuntimeHostClientSessionState.Connecting)));
            IRuntimeHostClientSession created = await factory.CreateAsync(profile.ProfileId, cancellationToken).ConfigureAwait(false);
            var createdCancellation = new CancellationTokenSource();
            created.StatusChanged += SessionStatusChanged;
            session = created;
            cancellation = createdCancellation;
            sessionTask = RunAsync(created, createdCancellation.Token);
        }
        catch (RuntimeHostClientException exception)
        {
            PublishFault(exception);
            throw;
        }
        finally { gate.Release(); }
    }

    public async Task DisconnectAsync()
    {
        IRuntimeHostClientSession? active;
        CancellationTokenSource? activeCancellation;
        Task? activeTask;
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            active = session; activeCancellation = cancellation; activeTask = sessionTask;
            session = null; cancellation = null; sessionTask = null;
        }
        finally { gate.Release(); }
        if (active is null) return;
        activeCancellation!.Cancel();
        try { if (activeTask is not null) await activeTask.ConfigureAwait(false); }
        finally
        {
            active.StatusChanged -= SessionStatusChanged;
            await active.DisposeAsync().ConfigureAwait(false);
            activeCancellation.Dispose();
            Publish(CreateSnapshot(new(RuntimeHostClientSessionState.Disconnected)));
        }
    }

    public Task<RemotePropertyOperationResult> ReadPropertyAsync(RemotePropertyTarget target, CancellationToken cancellationToken = default) =>
        UseSessionAsync<IRuntimeHostPropertyReader, RemotePropertyTarget, RemotePropertyOperationResult>(
            target, (reader, value) => reader.ReadPropertyAsync(value, cancellationToken), cancellationToken);

    public Task<RemotePropertyOperationResult> WritePropertyAsync(RemotePropertyTarget target, RemoteValue requestedValue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedValue);
        return UseSessionAsync<IRuntimeHostPropertyWriter, RemotePropertyTarget, RemotePropertyOperationResult>(
            target, (writer, value) => writer.WritePropertyAsync(value, requestedValue, cancellationToken), cancellationToken);
    }

    public Task<RemoteCommandOperationResult> ExecuteCommandAsync(RemoteCommandExecutionRequest request, CancellationToken cancellationToken = default) =>
        UseSessionAsync<IRuntimeHostCommandExecutor, RemoteCommandExecutionRequest, RemoteCommandOperationResult>(
            request, (executor, value) => executor.ExecuteCommandAsync(value, cancellationToken), cancellationToken);

    private async Task<TResult> UseSessionAsync<TCapability, TTarget, TResult>(
        TTarget target,
        Func<TCapability, TTarget, Task<TResult>> operation,
        CancellationToken cancellationToken)
        where TCapability : class
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(target);
        IRuntimeHostClientSession active;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            active = session ?? throw new InvalidOperationException("A runtime-host profile session is not active.");
            if (Snapshot.Status.State != RuntimeHostClientSessionState.Connected)
                throw new InvalidOperationException("The runtime-host profile session is not connected.");
        }
        finally { gate.Release(); }

        if (active is not TCapability capability)
            throw new NotSupportedException($"The active runtime-host session does not support {typeof(TCapability).Name}.");
        return await operation(capability, target).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            disposed = true;
        }
        finally { gate.Release(); }
        await DisconnectAsync().ConfigureAwait(false);
    }

    private async Task RunAsync(IRuntimeHostClientSession active, CancellationToken token)
    {
        try
        {
            await foreach (RemoteObservationState state in active.ReadStatesAsync(token))
            {
                if (state.Snapshot!.RuntimeHostId != profile.ExpectedRuntimeHostId)
                    throw new RuntimeHostClientException(RuntimeHostClientFailureCategory.InvalidRemoteContract,
                        "The authoritative runtime-host identity does not match the configured profile.");
                Publish(CreateSnapshot(active.Status, state));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (RuntimeHostClientException exception) when (token.IsCancellationRequested && exception.Category == RuntimeHostClientFailureCategory.Cancelled) { }
        catch (RuntimeHostClientException exception) { PublishFault(exception); }
        catch (Exception exception) { PublishFault(new(RuntimeHostClientFailureCategory.Unknown, exception.Message, exception)); }
    }

    private void SessionStatusChanged(object? sender, RuntimeHostClientSessionStatusChangedEventArgs args)
    {
        RemoteObservationState? state = Snapshot.CurrentState;

        // The normalized session announces Connected immediately before its
        // observation enumerator yields the authoritative initial state.
        if (args.Current.State == RuntimeHostClientSessionState.Connected
            && (state is null || !state.IsInitialized))
        {
            return;
        }

        if (args.Current.State == RuntimeHostClientSessionState.Faulted)
        {
            PublishFault(new RuntimeHostClientException(
                RuntimeHostClientFailureCategory.Unknown,
                "The runtime-host client session faulted."));
            return;
        }

        Publish(CreateSnapshot(args.Current, state));
    }

    private RuntimeHostProfileSessionSnapshot CreateSnapshot(RuntimeHostClientSessionStatus status, RemoteObservationState? state = null) =>
        new(profile, status, DateTimeOffset.UtcNow, state);

    private void PublishFault(RuntimeHostClientException exception) => Publish(
        new(profile, new(RuntimeHostClientSessionState.Faulted), DateTimeOffset.UtcNow,
            Snapshot.CurrentState, new(exception.Category, exception.Message)));

    private void Publish(RuntimeHostProfileSessionSnapshot value)
    { Volatile.Write(ref snapshot, value); SnapshotChanged?.Invoke(this, EventArgs.Empty); }
}

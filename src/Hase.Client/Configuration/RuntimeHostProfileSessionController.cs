namespace Hase.Client.Configuration;
using Hase.Client.Diagnostics;
using Hase.Client.Media;

/// <summary>Owns one independent runtime-host profile session.</summary>
public sealed class RuntimeHostProfileSessionController :
    IRuntimeHostProfileSessionController,
    IRuntimeHostMediaControlClient
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly RuntimeHostProfile profile;
    private readonly IRuntimeHostProfileClientSessionFactory factory;
    private IRuntimeHostClientSession? session;
    private CancellationTokenSource? cancellation;
    private Task? sessionTask;
    private bool disposed;
    private RuntimeHostProfileSessionSnapshot snapshot;
    private readonly ClientDiagnosticPublisher? diagnostics;

    public RuntimeHostProfileSessionController(
        RuntimeHostProfile profile,
        IRuntimeHostProfileClientSessionFactory factory,
        ClientDiagnosticPublisher? diagnostics = null)
    {
        this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.diagnostics = diagnostics;
        snapshot = CreateSnapshot(new(RuntimeHostClientSessionState.Disconnected));
    }

    public event EventHandler? SnapshotChanged;
    public event EventHandler<RuntimeHostProfileEventOccurredEventArgs>? EventOccurred;
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
            if (created is IRuntimeHostEventSource eventSource)
                eventSource.EventOccurred += SessionEventOccurred;
            if (created is IRuntimeHostDiagnosticSource diagnosticSource)
            {
                diagnosticSource.DiagnosticObserved += SessionDiagnosticObserved;
                diagnosticSource.DiagnosticStreamFaulted += SessionDiagnosticStreamFaulted;
            }
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
            if (active is not null)
            {
                RuntimeHostProfileSessionSnapshot current = Snapshot;
                Publish(CreateSnapshot(
                    new RuntimeHostClientSessionStatus(
                        RuntimeHostClientSessionState.Disconnecting,
                        current.Status.RuntimeHostId,
                        current.Status.ApiVersion),
                    current.CurrentState));
            }
            session = null; cancellation = null; sessionTask = null;
        }
        finally { gate.Release(); }
        if (active is null) return;
        activeCancellation!.Cancel();
        try { if (activeTask is not null) await activeTask.ConfigureAwait(false); }
        finally
        {
            active.StatusChanged -= SessionStatusChanged;
            if (active is IRuntimeHostEventSource eventSource)
                eventSource.EventOccurred -= SessionEventOccurred;
            if (active is IRuntimeHostDiagnosticSource diagnosticSource)
            {
                diagnosticSource.DiagnosticObserved -= SessionDiagnosticObserved;
                diagnosticSource.DiagnosticStreamFaulted -= SessionDiagnosticStreamFaulted;
            }
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

    public Task<IReadOnlyList<RemoteMediaSourceCapability>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default) =>
        UseMediaSessionAsync(
            (client, token) => client.GetCapabilitiesAsync(token), cancellationToken);

    public Task<RemoteMediaStartResult> StartAsync(
        RemoteMediaSourceTarget target, bool includeAudio,
        CancellationToken cancellationToken = default) =>
        UseMediaSessionAsync(
            (client, token) => client.StartAsync(target, includeAudio, token),
            cancellationToken);

    public Task<RemoteMediaExchangeResult> ExchangeAsync(
        string sessionId, uint acknowledgedDeliverySequence,
        RemoteMediaNegotiationMessage? submittedMessage,
        CancellationToken cancellationToken = default) =>
        UseMediaSessionAsync(
            (client, token) => client.ExchangeAsync(sessionId,
                acknowledgedDeliverySequence, submittedMessage, token),
            cancellationToken);

    public Task<RemoteMediaStatusResult> GetStatusAsync(
        string sessionId, CancellationToken cancellationToken = default) =>
        UseMediaSessionAsync(
            (client, token) => client.GetStatusAsync(sessionId, token),
            cancellationToken);

    public Task<RemoteMediaStopResult> StopAsync(
        string sessionId, CancellationToken cancellationToken = default) =>
        UseMediaSessionAsync(
            (client, token) => client.StopAsync(sessionId, token),
            cancellationToken);

    private async Task<TResult> UseMediaSessionAsync<TResult>(
        Func<IRuntimeHostMediaControlClient, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        IRuntimeHostClientSession active;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            active = session ?? throw new InvalidOperationException(
                "A runtime-host profile session is not active.");
            if (Snapshot.Status.State != RuntimeHostClientSessionState.Connected)
            {
                throw new InvalidOperationException(
                    "The runtime-host profile session is not connected.");
            }
        }
        finally
        {
            gate.Release();
        }

        if (active is not IRuntimeHostMediaControlClient client)
        {
            throw new NotSupportedException(
                "The active runtime-host session does not support media control.");
        }
        return await operation(client, cancellationToken).ConfigureAwait(false);
    }

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

    private void SessionEventOccurred(object? sender, RemoteEventOccurredEventArgs args)
    {
        RuntimeHostProfileSessionSnapshot current = Snapshot;
        if (current.Status.State != RuntimeHostClientSessionState.Connected
            || current.Status.RuntimeHostId is null
            || current.CurrentState?.Snapshot?.Attachments.Any(
                attachment => attachment.Key == args.Observation.Attachment) != true)
            return;
        EventOccurred?.Invoke(
            this,
            new RuntimeHostProfileEventOccurredEventArgs(
                profile.ProfileId,
                current.Status.RuntimeHostId,
                args.Observation));
    }

    private void SessionDiagnosticObserved(
        object? sender,
        RemoteRuntimeDiagnosticObservedEventArgs args)
    {
        if (diagnostics is null)
        {
            return;
        }

        RuntimeHostProfileSessionSnapshot current = Snapshot;
        if (current.Status.State is RuntimeHostClientSessionState.Disconnected
            or RuntimeHostClientSessionState.Faulted)
        {
            return;
        }

        try
        {
            RemoteRuntimeDiagnosticRecord record = args.Observation.Record;
            if (!string.Equals(
                    record.RuntimeHostId,
                    profile.ExpectedRuntimeHostId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The projected diagnostic identity does not match the profile.");
            }
            RemoteRuntimeHostId runtimeHostId =
                current.Status.RuntimeHostId
                ?? new RemoteRuntimeHostId(record.RuntimeHostId);
            diagnostics.Publish(
                record.TimestampUtc,
                RemoteRuntimeDiagnosticClientEventMapper.Map(
                    record,
                    profile,
                    runtimeHostId));
        }
        catch
        {
            PublishDiagnosticStreamState(
                "RemoteDiagnosticRecordRejected",
                ClientDiagnosticSeverity.Warning);
        }
    }

    private void SessionDiagnosticStreamFaulted(
        object? sender,
        RemoteRuntimeDiagnosticStreamFaultedEventArgs args)
    {
        PublishDiagnosticStreamState(
            args.Kind switch
            {
                RemoteRuntimeDiagnosticStreamFailureKind.AuthorizationDenied =>
                    "RemoteDiagnosticAuthorizationDenied",
                RemoteRuntimeDiagnosticStreamFailureKind.AuthenticationFailed =>
                    "RemoteDiagnosticAuthenticationFailed",
                RemoteRuntimeDiagnosticStreamFailureKind.TransportUnavailable =>
                    "RemoteDiagnosticTransportUnavailable",
                RemoteRuntimeDiagnosticStreamFailureKind.Gap =>
                    "RemoteDiagnosticGapDetected",
                RemoteRuntimeDiagnosticStreamFailureKind.InvalidRemoteContract =>
                    "RemoteDiagnosticContractRejected",
                _ => "RemoteDiagnosticSubscriptionFaulted"
            },
            ClientDiagnosticSeverity.Warning);
    }

    private void PublishDiagnosticStreamState(
        string eventName,
        ClientDiagnosticSeverity severity)
    {
        RuntimeHostProfileSessionSnapshot current = Snapshot;
        diagnostics?.Publish(
            ClientDiagnosticLevel.Operational,
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientObservation,
                eventName,
                severity: severity,
                outcome: ClientDiagnosticOutcome.Failed,
                sessionContext: new ClientDiagnosticSessionContext(
                    profile.ProfileId,
                    profile.DisplayName,
                    profile.ExpectedRuntimeHostId,
                    current.Status.RuntimeHostId)));
    }

    private RuntimeHostProfileSessionSnapshot CreateSnapshot(RuntimeHostClientSessionStatus status, RemoteObservationState? state = null) =>
        new(profile, status, DateTimeOffset.UtcNow, state);

    private void PublishFault(RuntimeHostClientException exception) => Publish(
        new(profile, new(RuntimeHostClientSessionState.Faulted), DateTimeOffset.UtcNow,
            Snapshot.CurrentState, new(exception.Category, exception.Message)));

    private void Publish(RuntimeHostProfileSessionSnapshot value)
    {
        Volatile.Write(ref snapshot, value);
        diagnostics?.Publish(
            ClientDiagnosticLevel.Operational,
            () => new ClientDiagnosticEvent(
                ClientDiagnosticLevel.Operational,
                ClientDiagnosticCategory.ClientConnection,
                "RuntimeHostProfileSessionStateChanged",
                severity: value.Status.State == RuntimeHostClientSessionState.Faulted
                    ? ClientDiagnosticSeverity.Error
                    : ClientDiagnosticSeverity.Information,
                sessionContext: new ClientDiagnosticSessionContext(
                    profile.ProfileId,
                    profile.DisplayName,
                    profile.ExpectedRuntimeHostId,
                    value.Status.RuntimeHostId)));
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }
}

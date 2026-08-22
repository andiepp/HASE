using System.Runtime.CompilerServices;
using Hase.Runtime.Remote.Grpc.Hosting;
using Hase.Client.Media;

namespace Hase.Client.Grpc;

/// <summary>
/// Runs a finite sequence of fresh connected sessions after recoverable
/// observation failures.
/// </summary>
public sealed class RuntimeHostGrpcRecoveringClientSession
    : IRuntimeHostClientSession,
      IRuntimeHostPropertyReader,
      IRuntimeHostPropertyWriter,
      IRuntimeHostCommandExecutor,
      IRuntimeHostEventSource,
      IRuntimeHostDiagnosticSource,
      IRuntimeHostMediaControlClient
{
    private readonly object gate =
        new();
    private readonly Func<IRuntimeHostGrpcRecoverableSession> sessionFactory;
    private readonly RuntimeHostClientRecoveryPolicy recoveryPolicy;
    private readonly RuntimeHostGrpcFailureMapper failureMapper;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly CancellationTokenSource disposalCancellation =
        new();
    private RuntimeHostClientSessionStatus status =
        new(
            RuntimeHostClientSessionState.Disconnected);
    private RemoteObservationState? currentState;
    private IRuntimeHostGrpcRecoverableSession? activeSession;
    private bool started;
    private bool disposed;

    public event EventHandler<
        RuntimeHostClientSessionStatusChangedEventArgs>? StatusChanged;

    public event EventHandler<RemoteEventOccurredEventArgs>? EventOccurred;
    public event EventHandler<RemoteRuntimeDiagnosticObservedEventArgs>?
        DiagnosticObserved;
    public event EventHandler<RemoteRuntimeDiagnosticStreamFaultedEventArgs>?
        DiagnosticStreamFaulted;

    public RuntimeHostGrpcRecoveringClientSession(
        RuntimeHostPrivateNetworkClientOptions options,
        RuntimeHostClientRecoveryPolicy? recoveryPolicy = null)
        : this(
            CreateSessionFactory(
                options),
            recoveryPolicy
                ?? RuntimeHostClientRecoveryPolicy.Conservative,
            new RuntimeHostGrpcFailureMapper(),
            Task.Delay)
    {
    }

    /// <summary>
    /// Creates one recovering session for the explicitly labeled
    /// certificate-free loopback development profile.
    /// </summary>
    public RuntimeHostGrpcRecoveringClientSession(
        RuntimeHostDevelopmentLoopbackClientOptions options,
        RuntimeHostClientRecoveryPolicy? recoveryPolicy = null)
        : this(
            CreateSessionFactory(
                options),
            recoveryPolicy
                ?? RuntimeHostClientRecoveryPolicy.Conservative,
            new RuntimeHostGrpcFailureMapper(),
            Task.Delay)
    {
    }

    internal RuntimeHostGrpcRecoveringClientSession(
        Func<IRuntimeHostGrpcRecoverableSession> sessionFactory,
        RuntimeHostClientRecoveryPolicy recoveryPolicy,
        RuntimeHostGrpcFailureMapper failureMapper,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        this.sessionFactory =
            sessionFactory
            ?? throw new ArgumentNullException(
                nameof(sessionFactory));
        this.recoveryPolicy =
            recoveryPolicy
            ?? throw new ArgumentNullException(
                nameof(recoveryPolicy));
        this.failureMapper =
            failureMapper
            ?? throw new ArgumentNullException(
                nameof(failureMapper));
        this.delayAsync =
            delayAsync
            ?? throw new ArgumentNullException(
                nameof(delayAsync));
    }

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

    public async IAsyncEnumerable<RemoteObservationState> ReadStatesAsync(
        [EnumeratorCancellation]
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
                    "A recovering runtime-host client session can be "
                    + "consumed only once.");
            }

            started =
                true;
        }

        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                disposalCancellation.Token);
        CancellationToken sessionToken =
            linkedCancellation.Token;
        int recoveryAttempt =
            0;

        while (true)
        {
            sessionToken.ThrowIfCancellationRequested();
            SetStatus(
                currentState is null
                    ? RuntimeHostClientSessionState.Connecting
                    : RuntimeHostClientSessionState.Reconnecting);

            IRuntimeHostGrpcRecoverableSession? session =
                null;
            RuntimeHostClientException? failure =
                null;

            try
            {
                try
                {
                    session =
                        sessionFactory()
                        ?? throw new InvalidOperationException(
                            "The recovering session factory returned null.");
                    if (session is IRuntimeHostEventSource eventSource)
                    {
                        eventSource.EventOccurred +=
                            SessionEventOccurred;
                    }
                    if (session is IRuntimeHostDiagnosticSource diagnosticSource)
                    {
                        diagnosticSource.DiagnosticObserved +=
                            SessionDiagnosticObserved;
                        diagnosticSource.DiagnosticStreamFaulted +=
                            SessionDiagnosticStreamFaulted;
                    }
                    await session.ConnectAsync(
                            sessionToken)
                        .ConfigureAwait(
                            false);
                }
                catch (Exception exception)
                {
                    failure =
                        failureMapper.Map(
                            exception);
                }

                if (failure is null
                    && session is not null)
                {
                    RemoteObservationState initialState =
                        session.CurrentState
                        ?? throw new InvalidDataException(
                            "The connected runtime-host session has no "
                            + "authoritative initial state.");

                    lock (gate)
                    {
                        currentState =
                            initialState;
                        activeSession =
                            session;
                    }

                    SetStatus(
                        RuntimeHostClientSessionState.Connected);

                    await using IAsyncEnumerator<RemoteObservationState> states =
                        session.ReadStateChangesAsync(
                                sessionToken)
                            .GetAsyncEnumerator(
                                sessionToken);

                    while (true)
                    {
                        MoveNextResult moveResult =
                            await MoveNextAsync(
                                    states)
                                .ConfigureAwait(
                                    false);

                        if (moveResult.Exception is not null)
                        {
                            failure =
                                failureMapper.Map(
                                    moveResult.Exception);
                            break;
                        }

                        if (!moveResult.HasValue)
                        {
                            failure =
                                new RuntimeHostClientException(
                                    RuntimeHostClientFailureCategory
                                        .TransportUnavailable,
                                    "The runtime-host observation stream "
                                    + "ended.");
                            break;
                        }

                        RemoteObservationState state =
                            states.Current;

                        lock (gate)
                        {
                            currentState =
                                state;
                        }

                        yield return state;
                    }
                }

                if (session is not null)
                {
                    UnsubscribeEvents(
                        session);
                    ClearActiveSession(
                        session);
                    await session.DisposeAsync()
                        .ConfigureAwait(
                            false);
                    session =
                        null;
                }

                if (failure is null)
                {
                    throw new InvalidOperationException(
                        "The recovering runtime-host session ended without a "
                        + "failure classification.");
                }

                if (sessionToken.IsCancellationRequested
                    || failure.Category
                        == RuntimeHostClientFailureCategory.Cancelled)
                {
                    SetDisconnected();
                    throw failure;
                }

                if (!recoveryPolicy.TryGetDelay(
                        failure.Category,
                        recoveryAttempt,
                        out TimeSpan delay))
                {
                    SetStatus(
                        RuntimeHostClientSessionState.Faulted);
                    throw failure;
                }

                recoveryAttempt++;
                SetStatus(
                    RuntimeHostClientSessionState.Reconnecting);
                try
                {
                    await delayAsync(
                            delay,
                            sessionToken)
                        .ConfigureAwait(
                            false);
                }
                catch (OperationCanceledException exception)
                    when (sessionToken.IsCancellationRequested)
                {
                    SetDisconnected();
                    throw failureMapper.Map(
                        exception);
                }
            }
            finally
            {
                if (session is not null)
                {
                    UnsubscribeEvents(
                        session);
                    ClearActiveSession(
                        session);
                    await session.DisposeAsync()
                        .ConfigureAwait(
                            false);
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposed)
            {
                return ValueTask.CompletedTask;
            }

            disposed =
                true;
        }

        disposalCancellation.Cancel();
        disposalCancellation.Dispose();
        SetDisconnected();

        return ValueTask.CompletedTask;
    }

    public async Task<RemotePropertyOperationResult> ReadPropertyAsync(
        RemotePropertyTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            target);

        IRuntimeHostGrpcRecoverableSession session;

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            session =
                activeSession
                ?? throw new RuntimeHostClientException(
                    RuntimeHostClientFailureCategory.TransportUnavailable,
                    "The runtime-host session is not connected.");
        }

        if (session is not IRuntimeHostPropertyReader reader)
        {
            throw new NotSupportedException(
                "The connected runtime-host session does not support "
                + "authoritative Property reads.");
        }

        try
        {
            return await reader.ReadPropertyAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(
                    false);
        }
        catch (Exception exception)
        {
            throw failureMapper.Map(
                exception);
        }
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

        IRuntimeHostGrpcRecoverableSession session;

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            session =
                activeSession
                ?? throw new RuntimeHostClientException(
                    RuntimeHostClientFailureCategory.TransportUnavailable,
                    "The runtime-host session is not connected.");
        }

        if (session is not IRuntimeHostPropertyWriter writer)
        {
            throw new NotSupportedException(
                "The connected runtime-host session does not support "
                + "authoritative Property writes.");
        }

        try
        {
            return await writer.WritePropertyAsync(
                    target,
                    requestedValue,
                    cancellationToken)
                .ConfigureAwait(
                    false);
        }
        catch (Exception exception)
        {
            throw failureMapper.Map(
                exception);
        }
    }

    public async Task<RemoteCommandOperationResult> ExecuteCommandAsync(
        RemoteCommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        IRuntimeHostGrpcRecoverableSession session;

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);
            session =
                activeSession
                ?? throw new RuntimeHostClientException(
                    RuntimeHostClientFailureCategory.TransportUnavailable,
                    "The runtime-host session is not connected.");
        }

        if (session is not IRuntimeHostCommandExecutor executor)
        {
            throw new NotSupportedException(
                "The connected runtime-host session does not support "
                + "Command execution.");
        }

        try
        {
            return await executor.ExecuteCommandAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(
                    false);
        }
        catch (Exception exception)
        {
            throw failureMapper.Map(
                exception);
        }
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
        IRuntimeHostGrpcRecoverableSession session;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            session = activeSession ?? throw new RuntimeHostClientException(
                RuntimeHostClientFailureCategory.TransportUnavailable,
                "The runtime-host session is not connected.");
        }

        if (session is not IRuntimeHostMediaControlClient mediaClient)
        {
            throw new NotSupportedException(
                "The connected runtime-host session does not support media control.");
        }

        try
        {
            return await operation(mediaClient, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw failureMapper.Map(exception);
        }
    }

    private void ClearActiveSession(
        IRuntimeHostGrpcRecoverableSession session)
    {
        lock (gate)
        {
            if (ReferenceEquals(
                    activeSession,
                    session))
            {
                activeSession =
                    null;
            }
        }
    }

    private void SessionEventOccurred(
        object? sender,
        RemoteEventOccurredEventArgs eventArgs)
    {
        EventOccurred?.Invoke(
            this,
            eventArgs);
    }

    private void SessionDiagnosticObserved(
        object? sender,
        RemoteRuntimeDiagnosticObservedEventArgs eventArgs)
    {
        DiagnosticObserved?.Invoke(this, eventArgs);
    }

    private void SessionDiagnosticStreamFaulted(
        object? sender,
        RemoteRuntimeDiagnosticStreamFaultedEventArgs eventArgs)
    {
        DiagnosticStreamFaulted?.Invoke(this, eventArgs);
    }

    private void UnsubscribeEvents(
        IRuntimeHostGrpcRecoverableSession session)
    {
        if (session is IRuntimeHostEventSource eventSource)
        {
            eventSource.EventOccurred -=
                SessionEventOccurred;
        }
        if (session is IRuntimeHostDiagnosticSource diagnosticSource)
        {
            diagnosticSource.DiagnosticObserved -=
                SessionDiagnosticObserved;
            diagnosticSource.DiagnosticStreamFaulted -=
                SessionDiagnosticStreamFaulted;
        }
    }

    private void SetStatus(
        RuntimeHostClientSessionState state)
    {
        RuntimeHostClientSessionStatus previous;
        RuntimeHostClientSessionStatus current;

        lock (gate)
        {
            previous =
                status;
            RemoteRuntimeHostSnapshot? snapshot =
                currentState?.Snapshot;
            current =
                new RuntimeHostClientSessionStatus(
                    state,
                    state is RuntimeHostClientSessionState.Disconnected
                        or RuntimeHostClientSessionState.Connecting
                        ? null
                        : snapshot?.RuntimeHostId,
                    state is RuntimeHostClientSessionState.Disconnected
                        or RuntimeHostClientSessionState.Connecting
                        ? null
                        : snapshot?.ApiVersion);
            status =
                current;
        }

        PublishStatusChanged(
            previous,
            current);
    }

    private void SetDisconnected()
    {
        RuntimeHostClientSessionStatus previous;
        RuntimeHostClientSessionStatus current =
            new(
                RuntimeHostClientSessionState.Disconnected);

        lock (gate)
        {
            previous =
                status;
            status =
                current;
        }

        PublishStatusChanged(
            previous,
            current);
    }

    private void PublishStatusChanged(
        RuntimeHostClientSessionStatus previous,
        RuntimeHostClientSessionStatus current)
    {
        if (previous == current)
        {
            return;
        }

        StatusChanged?.Invoke(
            this,
            new RuntimeHostClientSessionStatusChangedEventArgs(
                previous,
                current));
    }

    private static async Task<MoveNextResult> MoveNextAsync(
        IAsyncEnumerator<RemoteObservationState> enumerator)
    {
        try
        {
            return new MoveNextResult(
                await enumerator.MoveNextAsync()
                    .ConfigureAwait(
                        false),
                null);
        }
        catch (Exception exception)
        {
            return new MoveNextResult(
                false,
                exception);
        }
    }

    private static Func<IRuntimeHostGrpcRecoverableSession>
        CreateSessionFactory(
            RuntimeHostPrivateNetworkClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        return () =>
            new RuntimeHostGrpcClientSession(
                options);
    }

    private static Func<IRuntimeHostGrpcRecoverableSession>
        CreateSessionFactory(
            RuntimeHostDevelopmentLoopbackClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        return () =>
            new RuntimeHostGrpcClientSession(
                options);
    }

    private readonly record struct MoveNextResult(
        bool HasValue,
        Exception? Exception);
}

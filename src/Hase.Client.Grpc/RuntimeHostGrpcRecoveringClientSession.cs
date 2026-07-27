using System.Runtime.CompilerServices;
using Hase.Runtime.Remote.Grpc.Hosting;

namespace Hase.Client.Grpc;

/// <summary>
/// Runs a finite sequence of fresh connected sessions after recoverable
/// observation failures.
/// </summary>
public sealed class RuntimeHostGrpcRecoveringClientSession
    : IRuntimeHostClientSession
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
    private bool started;
    private bool disposed;

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

    private void SetStatus(
        RuntimeHostClientSessionState state)
    {
        lock (gate)
        {
            RemoteRuntimeHostSnapshot? snapshot =
                currentState?.Snapshot;
            status =
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
        }
    }

    private void SetDisconnected()
    {
        lock (gate)
        {
            status =
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Disconnected);
        }
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

    private readonly record struct MoveNextResult(
        bool HasValue,
        Exception? Exception);
}

using System.Runtime.CompilerServices;
using Grpc.Core;
using Hase.Client;
using Hase.Client.Grpc;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcRecoveringClientSessionTests
{
    [Fact]
    public async Task ReadStatesAsync_UnavailableConnect_ShouldUseFreshSession()
    {
        var first =
            ScriptedSession.FailingConnect(
                Rpc(
                    StatusCode.Unavailable));
        var second =
            ScriptedSession.WithStates(
                [CreateState(
                    "runtime-02")]);
        var sessions =
            new Queue<ScriptedSession>(
                [first, second]);
        var delays =
            new List<TimeSpan>();
        await using var recovering =
            CreateRecoveringSession(
                sessions,
                [TimeSpan.Zero],
                (delay, _) =>
                {
                    delays.Add(
                        delay);

                    return Task.CompletedTask;
                });
        await using IAsyncEnumerator<RemoteObservationState> states =
            recovering.ReadStatesAsync()
                .GetAsyncEnumerator();

        Assert.True(
            await states.MoveNextAsync());

        Assert.Equal(
            "runtime-02",
            states.Current.Snapshot!.RuntimeHostId.Value);
        Assert.Equal(
            new TimeSpan[]
            {
                TimeSpan.Zero
            },
            delays);
        Assert.Equal(
            1,
            first.DisposeCount);
        Assert.Equal(
            RuntimeHostClientSessionState.Connected,
            recovering.Status.State);
    }

    [Fact]
    public async Task ReadStatesAsync_ObservationGap_ShouldReplaceBaseline()
    {
        var first =
            ScriptedSession.WithStates(
                [CreateState(
                    "runtime-01")],
                Rpc(
                    StatusCode.DataLoss));
        var second =
            ScriptedSession.WithStates(
                [CreateState(
                    "runtime-02")]);
        var sessions =
            new Queue<ScriptedSession>(
                [first, second]);
        await using var recovering =
            CreateRecoveringSession(
                sessions,
                [TimeSpan.Zero]);
        await using IAsyncEnumerator<RemoteObservationState> states =
            recovering.ReadStatesAsync()
                .GetAsyncEnumerator();

        Assert.True(
            await states.MoveNextAsync());
        Assert.Equal(
            "runtime-01",
            states.Current.Snapshot!.RuntimeHostId.Value);

        Assert.True(
            await states.MoveNextAsync());
        Assert.Equal(
            "runtime-02",
            states.Current.Snapshot!.RuntimeHostId.Value);
        Assert.Equal(
            "runtime-02",
            recovering.CurrentState!.Snapshot!.RuntimeHostId.Value);
        Assert.Equal(
            1,
            first.DisposeCount);
    }

    [Fact]
    public async Task ReadStatesAsync_AuthenticationFailure_ShouldNotRecover()
    {
        var first =
            ScriptedSession.FailingConnect(
                Rpc(
                    StatusCode.Unauthenticated));
        var sessions =
            new Queue<ScriptedSession>(
                [first]);
        await using var recovering =
            CreateRecoveringSession(
                sessions,
                [TimeSpan.Zero]);

        RuntimeHostClientException failure =
            await Assert.ThrowsAsync<RuntimeHostClientException>(
                async () => await ReadFirstAsync(
                    recovering.ReadStatesAsync()));

        Assert.Equal(
            RuntimeHostClientFailureCategory.Authentication,
            failure.Category);
        Assert.Empty(
            sessions);
        Assert.Equal(
            RuntimeHostClientSessionState.Faulted,
            recovering.Status.State);
    }

    [Fact]
    public async Task ReadStatesAsync_ExhaustedSchedule_ShouldStop()
    {
        var first =
            ScriptedSession.FailingConnect(
                Rpc(
                    StatusCode.Unavailable));
        var second =
            ScriptedSession.FailingConnect(
                Rpc(
                    StatusCode.Unavailable));
        var sessions =
            new Queue<ScriptedSession>(
                [first, second]);
        await using var recovering =
            CreateRecoveringSession(
                sessions,
                [TimeSpan.Zero]);

        RuntimeHostClientException failure =
            await Assert.ThrowsAsync<RuntimeHostClientException>(
                async () => await ReadFirstAsync(
                    recovering.ReadStatesAsync()));

        Assert.Equal(
            RuntimeHostClientFailureCategory.TransportUnavailable,
            failure.Category);
        Assert.Empty(
            sessions);
        Assert.Equal(
            RuntimeHostClientSessionState.Faulted,
            recovering.Status.State);
    }

    [Fact]
    public async Task ReadStatesAsync_CancelledRecoveryDelay_ShouldStop()
    {
        var first =
            ScriptedSession.FailingConnect(
                Rpc(
                    StatusCode.Unavailable));
        var sessions =
            new Queue<ScriptedSession>(
                [first]);
        using var cancellation =
            new CancellationTokenSource();
        await using var recovering =
            CreateRecoveringSession(
                sessions,
                [TimeSpan.FromSeconds(
                    1)],
                async (_, token) =>
                {
                    cancellation.Cancel();
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        token);
                });

        RuntimeHostClientException failure =
            await Assert.ThrowsAsync<RuntimeHostClientException>(
                async () => await ReadFirstAsync(
                    recovering.ReadStatesAsync(
                        cancellation.Token)));

        Assert.Equal(
            RuntimeHostClientFailureCategory.Cancelled,
            failure.Category);
        Assert.Equal(
            RuntimeHostClientSessionState.Disconnected,
            recovering.Status.State);
    }

    [Fact]
    public async Task ReadStatesAsync_SecondEnumeration_ShouldThrow()
    {
        var session =
            ScriptedSession.WithStates(
                [CreateState(
                    "runtime-01")]);
        await using var recovering =
            CreateRecoveringSession(
                new Queue<ScriptedSession>(
                    [session]),
                []);
        await using IAsyncEnumerator<RemoteObservationState> first =
            recovering.ReadStatesAsync()
                .GetAsyncEnumerator();
        Assert.True(
            await first.MoveNextAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ReadFirstAsync(
                recovering.ReadStatesAsync()));
    }

    [Fact]
    public async Task EnumeratorDisposal_ShouldDisposeActiveSession()
    {
        var session =
            ScriptedSession.WithStates(
                [CreateState(
                    "runtime-01")]);
        await using var recovering =
            CreateRecoveringSession(
                new Queue<ScriptedSession>(
                    [session]),
                []);
        IAsyncEnumerator<RemoteObservationState> states =
            recovering.ReadStatesAsync()
                .GetAsyncEnumerator();
        Assert.True(
            await states.MoveNextAsync());

        await states.DisposeAsync();

        Assert.Equal(
            1,
            session.DisposeCount);
    }

    private static RuntimeHostGrpcRecoveringClientSession
        CreateRecoveringSession(
            Queue<ScriptedSession> sessions,
            IReadOnlyList<TimeSpan> delays,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        return new RuntimeHostGrpcRecoveringClientSession(
            () => sessions.Dequeue(),
            new RuntimeHostClientRecoveryPolicy(
                delays),
            new RuntimeHostGrpcFailureMapper(),
            delayAsync
                ?? ((_, _) => Task.CompletedTask));
    }

    private static async Task<RemoteObservationState> ReadFirstAsync(
        IAsyncEnumerable<RemoteObservationState> states)
    {
        await foreach (RemoteObservationState state
            in states)
        {
            return state;
        }

        throw new InvalidOperationException(
            "The state stream completed.");
    }

    private static RemoteObservationState CreateState(
        string runtimeHostId)
    {
        return new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    new RemoteRuntimeHostId(
                        runtimeHostId),
                    RuntimeHostClientApiVersion.Current,
                    []),
                new RemoteObservationSequence(
                    0)));
    }

    private static RpcException Rpc(
        StatusCode statusCode)
    {
        return new RpcException(
            new Status(
                statusCode,
                "detail"));
    }

    private sealed class ScriptedSession
        : IRuntimeHostGrpcRecoverableSession
    {
        private readonly Exception? connectFailure;
        private readonly IReadOnlyList<RemoteObservationState> states;
        private readonly Exception? streamFailure;

        private ScriptedSession(
            Exception? connectFailure,
            IReadOnlyList<RemoteObservationState> states,
            Exception? streamFailure)
        {
            this.connectFailure =
                connectFailure;
            this.states =
                states;
            this.streamFailure =
                streamFailure;
        }

        public RuntimeHostClientSessionStatus Status
        {
            get;
            private set;
        } =
            new(
                RuntimeHostClientSessionState.Disconnected);

        public RemoteObservationState? CurrentState
        {
            get;
            private set;
        }

        public Task Completion =>
            Task.CompletedTask;

        public int DisposeCount
        {
            get;
            private set;
        }

        public static ScriptedSession FailingConnect(
            Exception exception)
        {
            return new ScriptedSession(
                exception,
                [],
                null);
        }

        public static ScriptedSession WithStates(
            IReadOnlyList<RemoteObservationState> states,
            Exception? streamFailure = null)
        {
            return new ScriptedSession(
                null,
                states,
                streamFailure);
        }

        public Task ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (connectFailure is not null)
            {
                return Task.FromException(
                    connectFailure);
            }

            RemoteRuntimeHostSnapshot snapshot =
                states[0].Snapshot!;
            CurrentState =
                states[0];
            Status =
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Connected,
                    snapshot.RuntimeHostId,
                    snapshot.ApiVersion);

            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<RemoteObservationState>
            ReadStateChangesAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            foreach (RemoteObservationState state
                in states)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CurrentState =
                    state;
                yield return state;
            }

            if (streamFailure is not null)
            {
                throw streamFailure;
            }

            await Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            Status =
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Disconnected);

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }
}

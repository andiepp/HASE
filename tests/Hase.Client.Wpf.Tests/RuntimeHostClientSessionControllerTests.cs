using System.Runtime.CompilerServices;
using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class RuntimeHostClientSessionControllerTests
{
    [Fact]
    public async Task ConnectAsync_ShouldMarshalStatusAndState()
    {
        var session =
            new StubSession();
        var dispatcher =
            new RecordingDispatcher();
        var viewModel =
            new MainWindowViewModel();
        await using var controller =
            new RuntimeHostClientSessionController(
                new StubFactory(
                    session),
                dispatcher,
                viewModel);

        await controller.ConnectAsync(
            @"C:\HASE\client.json");
        await session.StateDelivered.Task;

        Assert.Equal(
            RuntimeHostClientSessionState.Connected,
            viewModel.SessionStatus.State);
        Assert.Same(
            RemoteObservationState.Empty,
            viewModel.CurrentState);
        Assert.True(
            dispatcher.PostCount >= 3);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldCancelAndDisposeSession()
    {
        var session =
            new StubSession();
        var viewModel =
            new MainWindowViewModel();
        await using var controller =
            new RuntimeHostClientSessionController(
                new StubFactory(
                    session),
                new RecordingDispatcher(),
                viewModel);
        await controller.ConnectAsync(
            @"C:\HASE\client.json");
        await session.StateDelivered.Task;

        await controller.DisconnectAsync();

        Assert.True(
            session.CancellationObserved);
        Assert.Equal(
            1,
            session.DisposeCount);
        Assert.Equal(
            RuntimeHostClientSessionState.Disconnected,
            viewModel.SessionStatus.State);
    }

    [Fact]
    public async Task ConnectAsync_WhileActive_ShouldThrow()
    {
        var session =
            new StubSession();
        await using var controller =
            new RuntimeHostClientSessionController(
                new StubFactory(
                    session),
                new RecordingDispatcher(),
                new MainWindowViewModel());
        await controller.ConnectAsync(
            @"C:\HASE\client.json");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                controller.ConnectAsync(
                    @"C:\HASE\client.json"));
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeIdempotent()
    {
        var session =
            new StubSession();
        var controller =
            new RuntimeHostClientSessionController(
                new StubFactory(
                    session),
                new RecordingDispatcher(),
                new MainWindowViewModel());
        await controller.ConnectAsync(
            @"C:\HASE\client.json");

        await controller.DisposeAsync();
        await controller.DisposeAsync();

        Assert.Equal(
            1,
            session.DisposeCount);
    }

    [Fact]
    public async Task BackgroundFailure_ShouldBeObservedAndReleaseSession()
    {
        var failedSession =
            new StubSession
            {
                StreamFailure =
                    new RuntimeHostClientException(
                        RuntimeHostClientFailureCategory.Authentication,
                        "Sensitive diagnostic")
            };
        var replacementSession =
            new StubSession();
        var factory =
            new QueueFactory(
                [failedSession, replacementSession]);
        var viewModel =
            new MainWindowViewModel();
        await using var controller =
            new RuntimeHostClientSessionController(
                factory,
                new RecordingDispatcher(),
                viewModel);

        await controller.ConnectAsync(
            @"C:\HASE\client.json");
        await failedSession.Disposed.Task;

        Assert.Equal(
            RuntimeHostClientFailureCategory.Authentication,
            viewModel.LastFailureCategory);
        Assert.Equal(
            "Runtime-host authentication failed.",
            viewModel.FailureMessage);
        Assert.Equal(
            1,
            failedSession.DisposeCount);

        await controller.ConnectAsync(
            @"C:\HASE\client.json");
        await replacementSession.StateDelivered.Task;
    }

    private sealed class StubFactory
        : IRuntimeHostClientSessionFactory
    {
        private readonly IRuntimeHostClientSession session;

        public StubFactory(
            IRuntimeHostClientSession session)
        {
            this.session =
                session;
        }

        public Task<IRuntimeHostClientSession> CreateAsync(
            string configurationFilePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                session);
    }

    private sealed class QueueFactory
        : IRuntimeHostClientSessionFactory
    {
        private readonly Queue<IRuntimeHostClientSession> sessions;

        public QueueFactory(
            IEnumerable<IRuntimeHostClientSession> sessions)
        {
            this.sessions =
                new Queue<IRuntimeHostClientSession>(
                    sessions);
        }

        public Task<IRuntimeHostClientSession> CreateAsync(
            string configurationFilePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                sessions.Dequeue());
    }

    private sealed class RecordingDispatcher
        : IClientUiDispatcher
    {
        public int PostCount
        {
            get;
            private set;
        }

        public void Post(
            Action action)
        {
            PostCount++;
            action();
        }
    }

    private sealed class StubSession
        : IRuntimeHostClientSession
    {
        public event EventHandler<
            RuntimeHostClientSessionStatusChangedEventArgs>? StatusChanged;

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

        public TaskCompletionSource StateDelivered
        {
            get;
        } =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CancellationObserved
        {
            get;
            private set;
        }

        public int DisposeCount
        {
            get;
            private set;
        }

        public Exception? StreamFailure
        {
            get;
            init;
        }

        public TaskCompletionSource Disposed
        {
            get;
        } =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<RemoteObservationState> ReadStatesAsync(
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            SetStatus(
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Connecting));
            SetStatus(
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Connected,
                    new RemoteRuntimeHostId(
                        "runtime-01"),
                    RuntimeHostClientApiVersion.Current));
            CurrentState =
                RemoteObservationState.Empty;
            yield return CurrentState;
            StateDelivered.SetResult();

            if (StreamFailure is not null)
            {
                throw StreamFailure;
            }

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            finally
            {
                CancellationObserved =
                    cancellationToken.IsCancellationRequested;
            }
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            Disposed.TrySetResult();
            return ValueTask.CompletedTask;
        }

        private void SetStatus(
            RuntimeHostClientSessionStatus value)
        {
            RuntimeHostClientSessionStatus previous =
                Status;
            Status =
                value;
            StatusChanged?.Invoke(
                this,
                new RuntimeHostClientSessionStatusChangedEventArgs(
                    previous,
                    value));
        }
    }
}

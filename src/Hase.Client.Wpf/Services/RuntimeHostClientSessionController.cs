using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Services;

/// <summary>
/// Owns one presentation session and marshals its normalized status and state
/// onto the WPF UI thread.
/// </summary>
public sealed class RuntimeHostClientSessionController
    : IRuntimeHostClientSessionController
{
    private readonly SemaphoreSlim gate =
        new(
            1,
            1);
    private readonly IRuntimeHostClientSessionFactory sessionFactory;
    private readonly IClientUiDispatcher dispatcher;
    private readonly MainWindowViewModel viewModel;
    private IRuntimeHostClientSession? session;
    private CancellationTokenSource? sessionCancellation;
    private Task? sessionTask;
    private bool disposed;

    public RuntimeHostClientSessionController(
        IRuntimeHostClientSessionFactory sessionFactory,
        IClientUiDispatcher dispatcher,
        MainWindowViewModel viewModel)
    {
        this.sessionFactory =
            sessionFactory
            ?? throw new ArgumentNullException(
                nameof(sessionFactory));
        this.dispatcher =
            dispatcher
            ?? throw new ArgumentNullException(
                nameof(dispatcher));
        this.viewModel =
            viewModel
            ?? throw new ArgumentNullException(
                nameof(viewModel));
    }

    public async Task ConnectAsync(
        string configurationFilePath,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(
                false);

        try
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            if (session is not null)
            {
                throw new InvalidOperationException(
                    "A runtime-host client session is already active.");
            }

            IRuntimeHostClientSession createdSession =
                await sessionFactory.CreateAsync(
                        configurationFilePath,
                        cancellationToken)
                    .ConfigureAwait(
                        false);
            var createdCancellation =
                new CancellationTokenSource();

            createdSession.StatusChanged +=
                SessionStatusChanged;
            session =
                createdSession;
            sessionCancellation =
                createdCancellation;
            sessionTask =
                RunSessionAsync(
                    createdSession,
                    createdCancellation.Token);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        IRuntimeHostClientSession? activeSession;
        CancellationTokenSource? activeCancellation;
        Task? activeTask;

        await gate.WaitAsync()
            .ConfigureAwait(
                false);

        try
        {
            activeSession =
                session;
            activeCancellation =
                sessionCancellation;
            activeTask =
                sessionTask;
            session =
                null;
            sessionCancellation =
                null;
            sessionTask =
                null;
        }
        finally
        {
            gate.Release();
        }

        if (activeSession is null)
        {
            return;
        }

        activeCancellation!.Cancel();

        try
        {
            if (activeTask is not null)
            {
                await activeTask.ConfigureAwait(
                    false);
            }
        }
        finally
        {
            activeSession.StatusChanged -=
                SessionStatusChanged;
            await activeSession.DisposeAsync()
                .ConfigureAwait(
                    false);
            activeCancellation.Dispose();
            dispatcher.Post(
                () =>
                    viewModel.ApplySessionStatus(
                        new RuntimeHostClientSessionStatus(
                            RuntimeHostClientSessionState.Disconnected)));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync()
            .ConfigureAwait(
                false);

        try
        {
            if (disposed)
            {
                return;
            }

            disposed =
                true;
        }
        finally
        {
            gate.Release();
        }

        await DisconnectAsync()
            .ConfigureAwait(
                false);
    }

    private async Task RunSessionAsync(
        IRuntimeHostClientSession activeSession,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (RemoteObservationState state
                in activeSession.ReadStatesAsync(
                    cancellationToken))
            {
                dispatcher.Post(
                    () =>
                        viewModel.ApplyObservationState(
                            state));
            }
        }
        catch (RuntimeHostClientException exception)
            when (cancellationToken.IsCancellationRequested
                && exception.Category
                    == RuntimeHostClientFailureCategory.Cancelled)
        {
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void SessionStatusChanged(
        object? sender,
        RuntimeHostClientSessionStatusChangedEventArgs eventArgs)
    {
        dispatcher.Post(
            () =>
                viewModel.ApplySessionStatus(
                    eventArgs.Current));
    }
}

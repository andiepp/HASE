using System.IO;
using System.Windows;
using Hase.Client.Diagnostics;
using Hase.Client.Wpf.AppHost.Hosting;
using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Client.Wpf.Views;
using Hase.Client.Configuration;
using Hase.Client.Grpc.Configuration;
using Hase.Client.Wpf.AppHost.Media;
using System.ComponentModel;
using System.Windows.Threading;
using Prism.DryIoc;
using Prism.Ioc;

namespace Hase.Client.Wpf.AppHost;

public partial class App
    : PrismApplication
{
    private const string SingleInstanceMutexName =
        @"Local\HASE.Client";

    private RuntimeHostClientSessionController? sessionController;
    private IClientDiagnosticsWindowController? diagnosticsWindowController;
    private ClientMediaWindowController? mediaWindowController;
    private HaseClientSingleInstanceLease? singleInstanceLease;
    private IMultiHostClientSessionCoordinator? multiHostCoordinator;
    private IClientUiDispatcher? uiDispatcher;
    private MainWindowViewModel? mainWindowViewModel;
    private ClientMediaApplicationControlClient? mediaControlClient;
    private RuntimeHostProfileId? mediaWatchProfileId;
    private RuntimeHostClientSessionState? mediaWatchState;

    protected override void OnStartup(
        StartupEventArgs eventArgs)
    {
        singleInstanceLease =
            HaseClientSingleInstanceLease.TryAcquire(
                SingleInstanceMutexName);

        if (singleInstanceLease is null)
        {
            MessageBox.Show(
                "HASE Client is already running.\n\n"
                + "Close the existing client before starting another instance.",
                "HASE Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(
            eventArgs);
    }

    protected override Window CreateShell()
    {
        sessionController =
            Container.Resolve<RuntimeHostClientSessionController>();

        MainWindowViewModel viewModel =
            Container.Resolve<MainWindowViewModel>();
        mainWindowViewModel = viewModel;

        diagnosticsWindowController =
            Container.Resolve<IClientDiagnosticsWindowController>();
        mediaWindowController = new ClientMediaWindowController(viewModel.Media);

        viewModel.Configure(
            sessionController,
            Container.Resolve<IClientConfigurationFilePicker>(),
            diagnosticsWindowController,
            mediaWindowController,
            Container.Resolve<IClientInstrumentPanelRegistry>());

        PrivateNetworkRuntimeHostProfileRegistry registry =
            Container.Resolve<PrivateNetworkRuntimeHostProfileRegistry>();
        multiHostCoordinator = Container.Resolve<IMultiHostClientSessionCoordinator>();
        uiDispatcher = Container.Resolve<IClientUiDispatcher>();
        viewModel.ConfigureRuntimeHosts(registry.CoreProfiles);
        Container.Resolve<ClientDiagnosticsViewModel>()
            .ConfigureRuntimeHosts(registry.CoreProfiles);
        viewModel.ConfigureMultiHostCoordinator(multiHostCoordinator);
        multiHostCoordinator.SnapshotChanged += MultiHostSnapshotChanged;
        multiHostCoordinator.EventOccurred += MultiHostEventOccurred;

        MainWindow window = Container.Resolve<MainWindow>();
        mediaControlClient = new ClientMediaApplicationControlClient(
            profileId =>
                ((MultiHostClientSessionCoordinator)multiHostCoordinator)
                    .GetMediaControlClient(profileId),
            mediaWindowController.PresentationBoundary,
            new DispatcherSynchronizationContext(Dispatcher),
            Container.Resolve<ClientDiagnosticPublisher>());
        viewModel.Media.Configure(mediaControlClient);
        mediaControlClient.SelectRuntimeHost(
            viewModel.SelectedRuntimeHost?.ProfileId);
        mediaWatchProfileId = viewModel.SelectedRuntimeHost?.ProfileId;
        mediaWatchState = viewModel.SelectedRuntimeHost?.SessionState;
        viewModel.Media.RestartCapabilityWatch();
        mediaControlClient.RuntimeHostBindingChanged += MediaRuntimeHostBindingChanged;
        viewModel.PropertyChanged += MainWindowViewModelPropertyChanged;

        return window;
    }

    protected override void RegisterTypes(
        IContainerRegistry containerRegistry)
    {
        LaptopClientStartupConfiguration startupConfiguration =
            LaptopClientStartupConfiguration.Parse(
                Environment.GetCommandLineArgs());

        containerRegistry.RegisterInstance(
            startupConfiguration);
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        var diagnosticCollector = new BoundedClientDiagnosticCollector(
            2000,
            ClientDiagnosticLevel.Bytes);
        var diagnosticPublisher = new ClientDiagnosticPublisher(diagnosticCollector);
        containerRegistry.RegisterInstance(diagnosticCollector);
        containerRegistry.RegisterInstance(diagnosticPublisher);
        containerRegistry.RegisterInstance<IRuntimeHostClientSessionFactory>(
            RuntimeHostClientComposition.CreateSessionFactory(diagnosticPublisher));
        containerRegistry.RegisterInstance<IClientUiDispatcher>(
            RuntimeHostClientComposition.CreateDispatcher(
                Dispatcher));
        PrivateNetworkRuntimeHostProfileRegistry registry =
            PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(
                    startupConfiguration.ConfigurationFilePath)
                .GetAwaiter()
                .GetResult();
        containerRegistry.RegisterInstance(registry);
        var profileSessionFactory = new PrivateNetworkRuntimeHostProfileClientSessionFactory(
            registry,
            RuntimeHostClientComposition.CreateSessionFactory(diagnosticPublisher));
        containerRegistry.RegisterInstance<IRuntimeHostProfileClientSessionFactory>(profileSessionFactory);
        var controllerFactory = new RuntimeHostProfileSessionControllerFactory(profileSessionFactory, diagnosticPublisher);
        containerRegistry.RegisterInstance<IRuntimeHostProfileSessionControllerFactory>(controllerFactory);
        containerRegistry.RegisterInstance<IMultiHostClientSessionCoordinator>(
            new MultiHostClientSessionCoordinator(registry.CoreProfiles, controllerFactory));
        containerRegistry.RegisterInstance<
            IClientConfigurationFilePicker>(
                new StartupClientConfigurationFilePicker(
                    startupConfiguration));
        containerRegistry.RegisterSingleton<
            RuntimeHostClientSessionController>();
        containerRegistry.RegisterSingleton<
            IClientDiagnosticExportFilePicker,
            WpfClientDiagnosticExportFilePicker>();
        containerRegistry.RegisterSingleton<ClientDiagnosticsViewModel>();
        containerRegistry.RegisterSingleton<
            IClientDiagnosticsWindowController,
            ClientDiagnosticsWindowController>();

        // The panels this application composes into the workspace. An
        // instrument is offered a panel only when it declares one of these
        // identifiers. The application also names where a panel's stored
        // settings live, because a path that exists on one computer need
        // not exist on another and a panel carries no machine knowledge.
        containerRegistry.RegisterInstance<IClientInstrumentPanelRegistry>(
            new ClientInstrumentPanelRegistry(
                CreateInstrumentPanels()));
    }

    /// <summary>
    /// Composes the instrument panels this application ships.
    /// </summary>
    /// <remarks>
    /// This application ships none, exactly as the Client library ships
    /// none. A composition root that ships panels overrides this, and an
    /// instrument is offered one only when it declares an identifier the
    /// registry resolves.
    /// </remarks>
    protected virtual IEnumerable<IClientInstrumentPanel>
        CreateInstrumentPanels() =>
        [];

    protected override void OnExit(
        ExitEventArgs eventArgs)
    {
        try
        {
            if (mainWindowViewModel is not null)
            {
                mainWindowViewModel.PropertyChanged -=
                    MainWindowViewModelPropertyChanged;
            }
            if (mediaControlClient is not null)
            {
                mediaControlClient.RuntimeHostBindingChanged -=
                    MediaRuntimeHostBindingChanged;
            }
            mediaControlClient?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            diagnosticsWindowController ??=
                Container.Resolve<IClientDiagnosticsWindowController>();
            diagnosticsWindowController.Close();
            mediaWindowController?.Close();
            if (multiHostCoordinator is not null)
            {
                multiHostCoordinator.SnapshotChanged -= MultiHostSnapshotChanged;
                multiHostCoordinator.EventOccurred -= MultiHostEventOccurred;
                multiHostCoordinator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            sessionController?.DisposeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            try
            {
                base.OnExit(
                    eventArgs);
            }
            finally
            {
                singleInstanceLease?.Dispose();
                singleInstanceLease = null;
            }
        }
    }

    private void MainWindowViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.SelectedRuntimeHost))
        {
            RuntimeHostProfileItemViewModel? selected =
                mainWindowViewModel?.SelectedRuntimeHost;
            RuntimeHostProfileId? profileId = selected?.ProfileId;
            if (mediaControlClient is null
                || profileId == mediaControlClient.BoundRuntimeHostProfileId)
            {
                return;
            }
            mediaControlClient.SelectRuntimeHost(profileId);
            if (profileId != mediaControlClient.BoundRuntimeHostProfileId)
            {
                // An active media session pinned the binding; the selection
                // is applied when the session ends. Keep watching the
                // pinned Runtime Host until then.
                return;
            }
            mediaWatchProfileId = profileId;
            mediaWatchState = selected?.SessionState;
            mainWindowViewModel?.Media.RestartCapabilityWatch();
        }
    }

    private void MediaRuntimeHostBindingChanged(object? sender, EventArgs eventArgs)
    {
        RuntimeHostProfileItemViewModel? selected =
            mainWindowViewModel?.SelectedRuntimeHost;
        mediaWatchProfileId = mediaControlClient?.BoundRuntimeHostProfileId;
        mediaWatchState = selected?.ProfileId == mediaWatchProfileId
            ? selected?.SessionState
            : null;
        mainWindowViewModel?.Media.ResetForRuntimeHostChange();
        mainWindowViewModel?.Media.RestartCapabilityWatch();
    }

    private void MultiHostSnapshotChanged(object? sender, EventArgs eventArgs)
    {
        MultiHostClientSessionSnapshot snapshot = multiHostCoordinator!.Snapshot;
        uiDispatcher!.Post(() =>
        {
            mainWindowViewModel!.ApplyMultiHostSnapshot(snapshot);

            // Notify every profile state: a pinned media session must learn
            // that its Runtime Host disconnected even while another host is
            // selected in the inventory.
            foreach (RuntimeHostProfileSessionSnapshot profileSession in snapshot.Sessions)
            {
                mediaControlClient?.NotifyRuntimeHostState(
                    profileSession.ProfileId,
                    profileSession.Status.State);
            }

            RuntimeHostProfileSessionSnapshot? watched = mediaWatchProfileId is null
                ? null
                : snapshot.Sessions.SingleOrDefault(
                    profileSession => profileSession.ProfileId == mediaWatchProfileId);
            if (watched is not null)
            {
                bool recovered = watched.Status.State ==
                        RuntimeHostClientSessionState.Connected &&
                    mediaWatchState != RuntimeHostClientSessionState.Connected;
                mediaWatchState = watched.Status.State;
                if (recovered)
                {
                    mainWindowViewModel.Media.RestartCapabilityWatch();
                }
            }
        });
    }

    private void MultiHostEventOccurred(object? sender, RuntimeHostProfileEventOccurredEventArgs eventArgs)
    {
        uiDispatcher!.Post(() => mainWindowViewModel!.ApplyMultiHostEventOccurred(eventArgs));
    }
}

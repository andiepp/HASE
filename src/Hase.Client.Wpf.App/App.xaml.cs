using System.Windows;
using Hase.Client.Diagnostics;
using Hase.Client.Wpf.AppHost.Hosting;
using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Client.Wpf.Views;
using Hase.Client.Configuration;
using Hase.Client.Grpc.Configuration;
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
    private HaseClientSingleInstanceLease? singleInstanceLease;
    private IMultiHostClientSessionCoordinator? multiHostCoordinator;
    private IClientUiDispatcher? uiDispatcher;
    private MainWindowViewModel? mainWindowViewModel;

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

        viewModel.Configure(
            sessionController,
            Container.Resolve<IClientConfigurationFilePicker>(),
            diagnosticsWindowController);

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

        Window window =
            Container.Resolve<MainWindow>();

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
        containerRegistry.RegisterSingleton<ClientDiagnosticsViewModel>();
        containerRegistry.RegisterSingleton<
            IClientDiagnosticsWindowController,
            ClientDiagnosticsWindowController>();
    }

    protected override void OnExit(
        ExitEventArgs eventArgs)
    {
        try
        {
            diagnosticsWindowController ??=
                Container.Resolve<IClientDiagnosticsWindowController>();
            diagnosticsWindowController.Close();
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

    private void MultiHostSnapshotChanged(object? sender, EventArgs eventArgs)
    {
        MultiHostClientSessionSnapshot snapshot = multiHostCoordinator!.Snapshot;
        uiDispatcher!.Post(() => mainWindowViewModel!.ApplyMultiHostSnapshot(snapshot));
    }

    private void MultiHostEventOccurred(object? sender, RuntimeHostProfileEventOccurredEventArgs eventArgs)
    {
        uiDispatcher!.Post(() => mainWindowViewModel!.ApplyMultiHostEventOccurred(eventArgs));
    }
}

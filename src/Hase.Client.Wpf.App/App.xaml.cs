using System.Windows;
using Hase.Client.Diagnostics;
using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Client.Wpf.Views;
using Prism.DryIoc;
using Prism.Ioc;

namespace Hase.Client.Wpf.AppHost;

public partial class App
    : PrismApplication
{
    private RuntimeHostClientSessionController? sessionController;
    private IClientDiagnosticsWindowController? diagnosticsWindowController;

    protected override Window CreateShell()
    {
        sessionController =
            Container.Resolve<RuntimeHostClientSessionController>();

        MainWindowViewModel viewModel =
            Container.Resolve<MainWindowViewModel>();

        diagnosticsWindowController =
            Container.Resolve<IClientDiagnosticsWindowController>();

        viewModel.Configure(
            sessionController,
            Container.Resolve<IClientConfigurationFilePicker>(),
            diagnosticsWindowController);

        Window window =
            Container.Resolve<MainWindow>();

        window.Loaded +=
            async (_, _) =>
                await viewModel.ConnectAsync(
                    Container.Resolve<
                        LaptopClientStartupConfiguration>()
                        .ConfigurationFilePath);

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
        var diagnosticCollector = new BoundedClientDiagnosticCollector(2000);
        var diagnosticPublisher = new ClientDiagnosticPublisher(diagnosticCollector);
        containerRegistry.RegisterInstance(diagnosticCollector);
        containerRegistry.RegisterInstance(diagnosticPublisher);
        containerRegistry.RegisterInstance<IRuntimeHostClientSessionFactory>(
            RuntimeHostClientComposition.CreateSessionFactory(diagnosticPublisher));
        containerRegistry.RegisterInstance<IClientUiDispatcher>(
            RuntimeHostClientComposition.CreateDispatcher(
                Dispatcher));
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
            sessionController?.DisposeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            base.OnExit(
                eventArgs);
        }
    }
}

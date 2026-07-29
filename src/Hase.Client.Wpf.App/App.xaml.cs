using System.Windows;
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

    protected override Window CreateShell()
    {
        sessionController =
            Container.Resolve<RuntimeHostClientSessionController>();

        MainWindowViewModel viewModel =
            Container.Resolve<MainWindowViewModel>();

        viewModel.Configure(
            sessionController,
            Container.Resolve<IClientConfigurationFilePicker>());

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
        containerRegistry.RegisterInstance<IRuntimeHostClientSessionFactory>(
            RuntimeHostClientComposition.CreateSessionFactory());
        containerRegistry.RegisterInstance<IClientUiDispatcher>(
            RuntimeHostClientComposition.CreateDispatcher(
                Dispatcher));
        containerRegistry.RegisterInstance<
            IClientConfigurationFilePicker>(
                new StartupClientConfigurationFilePicker(
                    startupConfiguration));
        containerRegistry.RegisterSingleton<
            RuntimeHostClientSessionController>();
    }

    protected override void OnExit(
        ExitEventArgs eventArgs)
    {
        try
        {
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



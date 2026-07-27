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
        Container.Resolve<MainWindowViewModel>()
            .Configure(
                sessionController,
                Container.Resolve<IClientConfigurationFilePicker>());

        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(
        IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.RegisterInstance<IRuntimeHostClientSessionFactory>(
            RuntimeHostClientComposition.CreateSessionFactory());
        containerRegistry.RegisterInstance<IClientUiDispatcher>(
            RuntimeHostClientComposition.CreateDispatcher(
                Dispatcher));
        containerRegistry.RegisterSingleton<
            IClientConfigurationFilePicker,
            WpfClientConfigurationFilePicker>();
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

using System.Windows;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.App.ViewModels;
using Hase.DesktopHost.App.Views;
using Prism.DryIoc;
using Prism.Ioc;

namespace Hase.DesktopHost.App;

public partial class App : PrismApplication
{
    private MainWindowViewModel? mainWindowViewModel;

    protected override Window CreateShell()
    {
        mainWindowViewModel = Container.Resolve<MainWindowViewModel>();

        var window = Container.Resolve<MainWindow>();
        window.DataContext = mainWindowViewModel;

        mainWindowViewModel.StartAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return window;
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IDesktopRuntimeHostBackend, ShellValidationRuntimeHostBackend>();
        containerRegistry.RegisterSingleton<IDesktopRuntimeHost, DesktopRuntimeHost>();
        containerRegistry.RegisterInstance(
            new DesktopRuntimeHostShellInformation(
                Composition: "Shell validation backend",
                HostIdentity: "Not available until production runtime integration",
                ApiVersion: "Not available until northbound host integration",
                LoopbackBinding: "Not configured in this increment",
                PrivateNetworkBinding: "Not configured in this increment"));
        containerRegistry.RegisterSingleton<DesktopRuntimeHostViewModel>();
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.RegisterSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        try
        {
            mainWindowViewModel?.StopAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            mainWindowViewModel?.Dispose();
            base.OnExit(eventArgs);
        }
    }
}

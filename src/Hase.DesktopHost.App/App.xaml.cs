using System.Windows;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.App.ViewModels;
using Hase.DesktopHost.App.Views;
using Hase.Runtime.Northbound;
using Prism.DryIoc;
using Prism.Ioc;

namespace Hase.DesktopHost.App;

public partial class App : PrismApplication
{
    private MainWindowViewModel? mainWindowViewModel;

    protected override Window CreateShell()
    {
        mainWindowViewModel =
            Container.Resolve<MainWindowViewModel>();

        var window =
            Container.Resolve<MainWindow>();
        window.DataContext =
            mainWindowViewModel;

        window.Loaded +=
            OnMainWindowLoaded;

        return window;
    }

    protected override void RegisterTypes(
        IContainerRegistry containerRegistry)
    {
        DesktopRuntimeHostStartupConfiguration startupConfiguration =
            DesktopRuntimeHostStartupConfiguration.Parse(
                Environment.GetCommandLineArgs());

        containerRegistry.RegisterInstance(
            startupConfiguration);
        containerRegistry.RegisterSingleton<
            IDesktopRuntimeHostBackend,
            ProductionPrivateNetworkRuntimeHostBackend>();
        containerRegistry.RegisterSingleton<
            IDesktopRuntimeHost,
            DesktopRuntimeHost>();
        containerRegistry.RegisterInstance(
            new DesktopRuntimeHostShellInformation(
                Composition:
                    "Production private-network runtime host",
                HostIdentity:
                    ProductionPrivateNetworkRuntimeHostBackend
                        .RuntimeHostId
                        .Value,
                ApiVersion:
                    RuntimeHostApiVersion.Current.ToString(),
                LoopbackBinding:
                    "Deferred - private-network binding is active "
                    + "in this increment",
                PrivateNetworkBinding:
                    startupConfiguration.PrivateNetworkBindingDisplay));
        containerRegistry.RegisterSingleton<
            DesktopRuntimeHostViewModel>();
        containerRegistry.RegisterSingleton<
            MainWindowViewModel>();
        containerRegistry.RegisterSingleton<
            MainWindow>();
    }

    protected override void OnExit(
        ExitEventArgs eventArgs)
    {
        try
        {
            mainWindowViewModel?.StopAsync(
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            mainWindowViewModel?.Dispose();
            base.OnExit(
                eventArgs);
        }
    }

    private async void OnMainWindowLoaded(
        object sender,
        RoutedEventArgs eventArgs)
    {
        if (sender is Window window)
        {
            window.Loaded -=
                OnMainWindowLoaded;
        }

        if (mainWindowViewModel is null)
        {
            return;
        }

        try
        {
            await mainWindowViewModel.StartAsync(
                CancellationToken.None);
        }
        catch
        {
            // The lifecycle coordinator projects startup failures through
            // Faulted status and LastError. Keeping the window open allows
            // the operator to inspect that information.
        }
    }
}

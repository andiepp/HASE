using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.App.ViewModels;
using Hase.DesktopHost.App.Views;
using Hase.DesktopHost.App.Media;
using Hase.Runtime.Northbound;
using Prism.DryIoc;
using Prism.Ioc;

namespace Hase.DesktopHost.App;

public partial class App : PrismApplication
{
    private const string SingleInstanceMutexName =
        @"Local\HASE.DesktopRuntimeHost";

    private readonly DispatcherTimer inventoryRefreshTimer =
        new()
        {
            Interval =
                TimeSpan.FromSeconds(1)
        };

    private MainWindowViewModel? mainWindowViewModel;
    private DesktopRuntimeHostWindowShutdownCoordinator? shutdownCoordinator;
    private DesktopRuntimeHostSingleInstanceLease? singleInstanceLease;
    private ProductionPrivateNetworkRuntimeHostBackend? productionBackend;

    protected override void OnStartup(
        StartupEventArgs eventArgs)
    {
        singleInstanceLease =
            DesktopRuntimeHostSingleInstanceLease.TryAcquire(
                SingleInstanceMutexName);

        if (singleInstanceLease is null)
        {
            MessageBox.Show(
                "HASE Runtime Host is already running.\n\n"
                + "Close the existing Runtime Host before starting another instance.",
                "HASE Runtime Host",
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
        mainWindowViewModel =
            Container.Resolve<MainWindowViewModel>();

        var window =
            Container.Resolve<MainWindow>();
        if (productionBackend is not null
            && Container.Resolve<DesktopRuntimeHostStartupConfiguration>()
                .MediaConfiguration is not null)
        {
            productionBackend.ConfigureMediaBoundary(
                new WebView2RuntimeHostMediaCaptureBoundary(
                    window.CreateMediaCaptureWebView(),
                    Path.Combine(AppContext.BaseDirectory, "Media", "Assets")));
        }
        window.DataContext =
            mainWindowViewModel;

        window.Loaded +=
            OnMainWindowLoaded;
        window.Closing +=
            OnMainWindowClosing;

        shutdownCoordinator =
            new DesktopRuntimeHostWindowShutdownCoordinator(
                mainWindowViewModel.StopAsync);

        inventoryRefreshTimer.Tick +=
            OnInventoryRefreshTimerTick;

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

        productionBackend =
            new ProductionPrivateNetworkRuntimeHostBackend(
                startupConfiguration);

        containerRegistry.RegisterInstance<
            IDesktopRuntimeHostBackend>(
                productionBackend);
        containerRegistry.RegisterInstance<
            IDesktopRuntimeHostInventorySource>(
                productionBackend);
        containerRegistry.RegisterInstance<
            IDesktopRuntimeHostOperator>(
                productionBackend);
        containerRegistry.RegisterInstance<
            IDesktopRuntimeHostEventSource>(
                productionBackend);
        containerRegistry.RegisterInstance<
            IDesktopRuntimeDiagnosticSource>(
                productionBackend);
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
            RuntimeInventoryViewModel>();
        containerRegistry.RegisterSingleton<
            EndpointDetailsViewModel>();
        containerRegistry.RegisterInstance(
            DesktopRuntimeByteInterpretationService.CreateDefault());
        containerRegistry.RegisterSingleton<
            RuntimeDiagnosticsViewModel>();
        containerRegistry.RegisterSingleton<
            IDesktopDiagnosticsWindowFactory,
            WpfDesktopDiagnosticsWindowFactory>();
        containerRegistry.RegisterSingleton<
            IDesktopDiagnosticsWindowService,
            DesktopDiagnosticsWindowService>();
        containerRegistry.RegisterSingleton<
            MainWindowViewModel>();
        containerRegistry.RegisterSingleton<
            MainWindow>();
    }

    protected override void OnExit(
        ExitEventArgs eventArgs)
    {
        inventoryRefreshTimer.Stop();
        inventoryRefreshTimer.Tick -=
            OnInventoryRefreshTimerTick;

        try
        {
            mainWindowViewModel?.Dispose();
            base.OnExit(
                eventArgs);
        }
        finally
        {
            singleInstanceLease?.Dispose();
            singleInstanceLease =
                null;
        }
    }

    private async void OnMainWindowClosing(
        object? sender,
        CancelEventArgs eventArgs)
    {
        DesktopRuntimeHostWindowShutdownCoordinator? coordinator =
            shutdownCoordinator;

        if (coordinator is null
            || coordinator.IsCompleted)
        {
            return;
        }

        eventArgs.Cancel =
            true;

        if (coordinator.IsStarted)
        {
            return;
        }

        inventoryRefreshTimer.Stop();

        try
        {
            await coordinator.StopAsync(
                CancellationToken.None);
        }
        catch
        {
            // Shutdown remains terminal. The Runtime Host view model already
            // retains any backend stop failure for diagnostics.
        }
        finally
        {
            if (sender is Window window)
            {
                window.Closing -=
                    OnMainWindowClosing;
                window.Close();
            }
            else
            {
                Shutdown();
            }
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

            inventoryRefreshTimer.Start();
        }
        catch
        {
            // The lifecycle coordinator projects startup failures through
            // Faulted status and LastError. Keeping the window open allows
            // the operator to inspect that information.
        }
    }

    private void OnInventoryRefreshTimerTick(
        object? sender,
        EventArgs eventArgs)
    {
        try
        {
            mainWindowViewModel?.RefreshInventory();
        }
        catch
        {
            // Inventory projection is observational. A refresh failure must
            // not terminate the runtime-host process or the WPF dispatcher.
        }
    }
}

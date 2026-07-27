namespace Hase.DesktopHost.App.ViewModels;

public sealed class MainWindowViewModel : IDisposable
{
    private readonly DesktopRuntimeHostViewModel runtimeHostViewModel;

    public MainWindowViewModel(DesktopRuntimeHostViewModel runtimeHostViewModel)
    {
        this.runtimeHostViewModel = runtimeHostViewModel
            ?? throw new ArgumentNullException(nameof(runtimeHostViewModel));
    }

    public string ApplicationTitle => "HASE Desktop Runtime Host";

    public DesktopRuntimeHostViewModel RuntimeHost => runtimeHostViewModel;

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        runtimeHostViewModel.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        runtimeHostViewModel.StopAsync(cancellationToken);

    public void Dispose() => runtimeHostViewModel.Dispose();
}

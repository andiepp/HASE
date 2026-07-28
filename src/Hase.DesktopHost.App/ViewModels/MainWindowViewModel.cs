namespace Hase.DesktopHost.App.ViewModels;

public sealed class MainWindowViewModel : IDisposable
{
    private readonly DesktopRuntimeHostViewModel runtimeHostViewModel;

    public MainWindowViewModel(
        DesktopRuntimeHostViewModel runtimeHostViewModel,
        RuntimeInventoryViewModel inventoryViewModel)
    {
        this.runtimeHostViewModel =
            runtimeHostViewModel
            ?? throw new ArgumentNullException(
                nameof(runtimeHostViewModel));
        Inventory =
            inventoryViewModel
            ?? throw new ArgumentNullException(
                nameof(inventoryViewModel));
    }

    public string ApplicationTitle =>
        "HASE Desktop Runtime Host";

    public DesktopRuntimeHostViewModel RuntimeHost =>
        runtimeHostViewModel;

    public RuntimeInventoryViewModel Inventory
    {
        get;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        await runtimeHostViewModel.StartAsync(
            cancellationToken);

        Inventory.Refresh();
    }

    public Task StopAsync(
        CancellationToken cancellationToken = default) =>
        runtimeHostViewModel.StopAsync(
            cancellationToken);

    public void RefreshInventory() =>
        Inventory.Refresh();

    public void Dispose() =>
        runtimeHostViewModel.Dispose();
}

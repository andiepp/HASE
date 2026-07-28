using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class MainWindowViewModel
    : INotifyPropertyChanged,
      IDisposable
{
    private readonly DesktopRuntimeHostViewModel runtimeHostViewModel;
    private readonly IDesktopRuntimeHostInventorySource inventorySource;

    public MainWindowViewModel(
        DesktopRuntimeHostViewModel runtimeHostViewModel,
        IDesktopRuntimeHostInventorySource inventorySource)
    {
        this.runtimeHostViewModel =
            runtimeHostViewModel
            ?? throw new ArgumentNullException(
                nameof(runtimeHostViewModel));
        this.inventorySource =
            inventorySource
            ?? throw new ArgumentNullException(
                nameof(inventorySource));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ApplicationTitle =>
        "HASE Desktop Runtime Host";

    public DesktopRuntimeHostViewModel RuntimeHost =>
        runtimeHostViewModel;

    public ObservableCollection<DesktopRuntimeEndpointViewModel> Endpoints
    {
        get;
    } =
        [];

    public int PublishedEndpointCount =>
        Endpoints.Count;

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        await runtimeHostViewModel.StartAsync(
            cancellationToken);

        RefreshInventory();
    }

    public Task StopAsync(
        CancellationToken cancellationToken = default) =>
        runtimeHostViewModel.StopAsync(
            cancellationToken);

    public void RefreshInventory()
    {
        IReadOnlyList<DesktopRuntimeEndpointSnapshot> snapshots =
            inventorySource.Capture();

        var projected =
            snapshots
                .OrderBy(
                    snapshot =>
                        snapshot.DisplayName,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    snapshot =>
                        snapshot.EndpointId,
                    StringComparer.Ordinal)
                .Select(
                    snapshot =>
                        new DesktopRuntimeEndpointViewModel(
                            snapshot.EndpointId,
                            snapshot.DisplayName,
                            snapshot.ConnectionState,
                            snapshot.AttachmentGeneration))
                .ToArray();

        Endpoints.Clear();

        foreach (
            DesktopRuntimeEndpointViewModel endpoint
            in projected)
        {
            Endpoints.Add(
                endpoint);
        }

        OnPropertyChanged(
            nameof(PublishedEndpointCount));
    }

    public void Dispose() =>
        runtimeHostViewModel.Dispose();

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class RuntimeInventoryViewModel
    : INotifyPropertyChanged
{
    private readonly IDesktopRuntimeHostInventorySource inventorySource;

    public RuntimeInventoryViewModel(
        IDesktopRuntimeHostInventorySource inventorySource)
    {
        this.inventorySource =
            inventorySource
            ?? throw new ArgumentNullException(
                nameof(inventorySource));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DesktopRuntimeEndpointViewModel> Endpoints
    {
        get;
    } =
        [];

    public int PublishedEndpointCount =>
        Endpoints.Count;

    public void Refresh()
    {
        IReadOnlyList<DesktopRuntimeEndpointSnapshot> snapshots =
            inventorySource.Capture();

        var orderedSnapshots =
            snapshots
                .OrderBy(
                    snapshot =>
                        snapshot.DisplayName,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    snapshot =>
                        snapshot.EndpointId,
                    StringComparer.Ordinal)
                .ToArray();

        var existingById =
            Endpoints.ToDictionary(
                endpoint =>
                    endpoint.EndpointId,
                StringComparer.Ordinal);
        var desiredIds =
            orderedSnapshots
                .Select(
                    snapshot =>
                        snapshot.EndpointId)
                .ToHashSet(
                    StringComparer.Ordinal);

        for (
            int index = Endpoints.Count - 1;
            index >= 0;
            index--)
        {
            if (!desiredIds.Contains(
                    Endpoints[index].EndpointId))
            {
                Endpoints.RemoveAt(
                    index);
            }
        }

        foreach (
            DesktopRuntimeEndpointSnapshot snapshot
            in orderedSnapshots)
        {
            if (existingById.TryGetValue(
                    snapshot.EndpointId,
                    out DesktopRuntimeEndpointViewModel? existing))
            {
                existing.Update(
                    snapshot);
            }
            else
            {
                Endpoints.Add(
                    new DesktopRuntimeEndpointViewModel(
                        snapshot.EndpointId,
                        snapshot.DisplayName,
                        snapshot.ConnectionState,
                        snapshot.AttachmentGeneration));
            }
        }

        ReorderToMatch(
            orderedSnapshots);

        OnPropertyChanged(
            nameof(PublishedEndpointCount));
    }

    private void ReorderToMatch(
        IReadOnlyList<DesktopRuntimeEndpointSnapshot> orderedSnapshots)
    {
        for (
            int desiredIndex = 0;
            desiredIndex < orderedSnapshots.Count;
            desiredIndex++)
        {
            string desiredEndpointId =
                orderedSnapshots[desiredIndex].EndpointId;
            int currentIndex =
                FindEndpointIndex(
                    desiredEndpointId);

            if (currentIndex >= 0
                && currentIndex != desiredIndex)
            {
                Endpoints.Move(
                    currentIndex,
                    desiredIndex);
            }
        }
    }

    private int FindEndpointIndex(
        string endpointId)
    {
        for (
            int index = 0;
            index < Endpoints.Count;
            index++)
        {
            if (string.Equals(
                    Endpoints[index].EndpointId,
                    endpointId,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}

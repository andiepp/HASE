using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimeEndpointViewModel
    : INotifyPropertyChanged
{
    private string displayName;
    private string connectionState;
    private string attachmentGeneration;
    private string description =
        string.Empty;
    private readonly ObservableCollection<DesktopRuntimeInstrumentViewModel>
        instruments =
        [];

    public DesktopRuntimeEndpointViewModel(
        string endpointId,
        string displayName,
        string connectionState,
        string attachmentGeneration)
    {
        EndpointId =
            string.IsNullOrWhiteSpace(endpointId)
                ? throw new ArgumentException(
                    "The endpoint identity must not be empty.",
                    nameof(endpointId))
                : endpointId;
        this.displayName =
            string.IsNullOrWhiteSpace(displayName)
                ? endpointId
                : displayName;
        this.connectionState =
            string.IsNullOrWhiteSpace(connectionState)
                ? "Unknown"
                : connectionState;
        this.attachmentGeneration =
            string.IsNullOrWhiteSpace(attachmentGeneration)
                ? throw new ArgumentException(
                    "The attachment generation must not be empty.",
                    nameof(attachmentGeneration))
                : attachmentGeneration;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string EndpointId
    {
        get;
    }

    public string DisplayName
    {
        get =>
            displayName;
        private set =>
            SetProperty(
                ref displayName,
                value);
    }

    public string ConnectionState
    {
        get =>
            connectionState;
        private set =>
            SetProperty(
                ref connectionState,
                value);
    }

    public string AttachmentGeneration
    {
        get =>
            attachmentGeneration;
        private set =>
            SetProperty(
                ref attachmentGeneration,
                value);
    }

    public string Description
    {
        get =>
            description;
        private set =>
            SetProperty(
                ref description,
                value);
    }

    public ObservableCollection<DesktopRuntimeInstrumentViewModel> Instruments =>
        instruments;

    public int InstrumentCount =>
        Instruments.Count;

    public bool IsReady =>
        IsState(
            "Ready");

    public bool IsRecovering =>
        IsState(
            "Connecting")
        || IsState(
            "Synchronizing")
        || IsState(
            "Reconnecting");

    public bool IsFaulted =>
        IsState(
            "Faulted");

    public bool IsDisconnected =>
        IsState(
            "Disconnected");

    public string StateIndicatorText =>
        IsReady
            ? "● Ready"
            : IsRecovering
                ? "◐ " + ConnectionState
                : IsFaulted
                    ? "⚠ Faulted"
                    : IsDisconnected
                        ? "○ Disconnected"
                        : "• " + ConnectionState;

    public void Update(
        DesktopRuntimeEndpointSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        if (!string.Equals(
                EndpointId,
                snapshot.EndpointId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "An endpoint view model cannot be updated from a different "
                + "endpoint identity.",
                nameof(snapshot));
        }

        DisplayName =
            string.IsNullOrWhiteSpace(snapshot.DisplayName)
                ? snapshot.EndpointId
                : snapshot.DisplayName;
        AttachmentGeneration =
            snapshot.AttachmentGeneration;
        Description =
            snapshot.Description
            ?? string.Empty;
        ReconcileInstruments(
            snapshot.Instruments);

        if (!string.Equals(
                ConnectionState,
                snapshot.ConnectionState,
                StringComparison.Ordinal))
        {
            ConnectionState =
                snapshot.ConnectionState;
            RaiseStatePropertiesChanged();
        }
    }

    private void ReconcileInstruments(
        IReadOnlyList<DesktopRuntimeInstrumentSnapshot> snapshots)
    {
        var existingById =
            Instruments.ToDictionary(
                instrument =>
                    instrument.InstrumentId,
                StringComparer.Ordinal);
        var desiredIds =
            snapshots
                .Select(
                    instrument =>
                        instrument.InstrumentId)
                .ToHashSet(
                    StringComparer.Ordinal);

        for (
            int index = Instruments.Count - 1;
            index >= 0;
            index--)
        {
            if (!desiredIds.Contains(
                    Instruments[index].InstrumentId))
            {
                Instruments.RemoveAt(
                    index);
            }
        }

        foreach (
            DesktopRuntimeInstrumentSnapshot snapshot
            in snapshots)
        {
            if (existingById.TryGetValue(
                    snapshot.InstrumentId,
                    out DesktopRuntimeInstrumentViewModel? existing))
            {
                existing.Update(
                    snapshot);
            }
            else
            {
                Instruments.Add(
                    new DesktopRuntimeInstrumentViewModel(
                        snapshot));
            }
        }

        for (
            int desiredIndex = 0;
            desiredIndex < snapshots.Count;
            desiredIndex++)
        {
            string desiredInstrumentId =
                snapshots[desiredIndex].InstrumentId;
            int currentIndex =
                FindInstrumentIndex(
                    desiredInstrumentId);

            if (currentIndex >= 0
                && currentIndex != desiredIndex)
            {
                Instruments.Move(
                    currentIndex,
                    desiredIndex);
            }
        }

        OnPropertyChanged(
            nameof(InstrumentCount));
    }

    private int FindInstrumentIndex(
        string instrumentId)
    {
        for (
            int index = 0;
            index < Instruments.Count;
            index++)
        {
            if (string.Equals(
                    Instruments[index].InstrumentId,
                    instrumentId,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private bool IsState(
        string state) =>
        string.Equals(
            ConnectionState,
            state,
            StringComparison.Ordinal);

    private void RaiseStatePropertiesChanged()
    {
        OnPropertyChanged(
            nameof(IsReady));
        OnPropertyChanged(
            nameof(IsRecovering));
        OnPropertyChanged(
            nameof(IsFaulted));
        OnPropertyChanged(
            nameof(IsDisconnected));
        OnPropertyChanged(
            nameof(StateIndicatorText));
    }

    private bool SetProperty(
        ref string field,
        string value,
        [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(
                field,
                value,
                StringComparison.Ordinal))
        {
            return false;
        }

        field =
            value;
        OnPropertyChanged(
            propertyName);
        return true;
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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimeInstrumentViewModel
    : INotifyPropertyChanged
{
    private static readonly string[] ModeSelectionOrder =
    [
        "CC",
        "CV",
        "CR",
        "CW",
        "SHORT"
    ];

    private static readonly string[] InputControlOrder =
    [
        "Activate",
        "Deactivate"
    ];

    private string name;
    private string kind;
    private string manufacturer;
    private string model;
    private string serialNumber;
    private string firmwareVersion;
    private string hardwareRevision;
    private string description;
    private DesktopRuntimeCommandViewModel? selectedModeCommand;

    public DesktopRuntimeInstrumentViewModel(
        DesktopRuntimeInstrumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        InstrumentId =
            snapshot.InstrumentId;
        name =
            snapshot.Name;
        kind =
            snapshot.Kind;
        manufacturer =
            snapshot.Manufacturer
            ?? string.Empty;
        model =
            snapshot.Model
            ?? string.Empty;
        serialNumber =
            snapshot.SerialNumber
            ?? string.Empty;
        firmwareVersion =
            snapshot.FirmwareVersion
            ?? string.Empty;
        hardwareRevision =
            snapshot.HardwareRevision
            ?? string.Empty;
        description =
            snapshot.Description
            ?? string.Empty;

        ReconcileProperties(
            snapshot.Properties);
        ReconcileCommands(
            snapshot.Commands);
        ReconcileEvents(
            snapshot.Events);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string InstrumentId
    {
        get;
    }

    public string Name
    {
        get =>
            name;
        private set =>
            SetProperty(
                ref name,
                value);
    }

    public string Kind
    {
        get =>
            kind;
        private set =>
            SetProperty(
                ref kind,
                value);
    }

    public string Manufacturer
    {
        get =>
            manufacturer;
        private set =>
            SetProperty(
                ref manufacturer,
                value);
    }

    public string Model
    {
        get =>
            model;
        private set =>
            SetProperty(
                ref model,
                value);
    }

    public string SerialNumber
    {
        get =>
            serialNumber;
        private set =>
            SetProperty(
                ref serialNumber,
                value);
    }

    public string FirmwareVersion
    {
        get =>
            firmwareVersion;
        private set =>
            SetProperty(
                ref firmwareVersion,
                value);
    }

    public string HardwareRevision
    {
        get =>
            hardwareRevision;
        private set =>
            SetProperty(
                ref hardwareRevision,
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

    public ObservableCollection<DesktopRuntimePropertyViewModel> Properties
    {
        get;
    } =
        [];

    public int PropertyCount =>
        Properties.Count;

    public ObservableCollection<DesktopRuntimeCommandViewModel> Commands
    {
        get;
    } =
        [];

    public int CommandCount =>
        Commands.Count;

    public ObservableCollection<DesktopRuntimeCommandViewModel>
        GeneralCommands
    {
        get;
    } =
        [];

    public ObservableCollection<DesktopRuntimeCommandViewModel>
        ModeSelectionCommands
    {
        get;
    } =
        [];

    public bool HasModeSelectionSelector =>
        ModeSelectionCommands.Count
        == ModeSelectionOrder.Length;

    public DesktopRuntimeCommandViewModel? SelectedModeCommand
    {
        get =>
            selectedModeCommand;
        set
        {
            if (value is not null
                && !ModeSelectionCommands.Contains(value))
            {
                throw new ArgumentException(
                    "The selected mode Command must belong to this instrument.",
                    nameof(value));
            }

            if (ReferenceEquals(
                    selectedModeCommand,
                    value))
            {
                return;
            }

            selectedModeCommand =
                value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<DesktopRuntimeCommandViewModel>
        InputControlCommands
    {
        get;
    } =
        [];

    public bool HasInputControlControls =>
        InputControlCommands.Count
        == InputControlOrder.Length;

    public DesktopRuntimeCommandViewModel? ActivateInputCommand =>
        InputControlCommands.SingleOrDefault(
            command =>
                string.Equals(
                    command.InputControlLabel,
                    "Activate",
                    StringComparison.Ordinal));

    public DesktopRuntimeCommandViewModel? DeactivateInputCommand =>
        InputControlCommands.SingleOrDefault(
            command =>
                string.Equals(
                    command.InputControlLabel,
                    "Deactivate",
                    StringComparison.Ordinal));

    public DesktopRuntimeCommandViewModel? ShortCircuitActivationCommand =>
        HasInputControlControls
            ? Commands.SingleOrDefault(
                command =>
                    command.IsConfirmedShortCircuitActivation)
            : null;

    public bool HasShortCircuitActivationControl =>
        ShortCircuitActivationCommand is not null;

    public ObservableCollection<DesktopRuntimeEventViewModel> Events
    {
        get;
    } =
        [];

    public int EventCount =>
        Events.Count;

    public void Update(
        DesktopRuntimeInstrumentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        if (!string.Equals(
                InstrumentId,
                snapshot.InstrumentId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "An instrument view model cannot be updated from a different "
                + "instrument identity.",
                nameof(snapshot));
        }

        Name =
            snapshot.Name;
        Kind =
            snapshot.Kind;
        Manufacturer =
            snapshot.Manufacturer
            ?? string.Empty;
        Model =
            snapshot.Model
            ?? string.Empty;
        SerialNumber =
            snapshot.SerialNumber
            ?? string.Empty;
        FirmwareVersion =
            snapshot.FirmwareVersion
            ?? string.Empty;
        HardwareRevision =
            snapshot.HardwareRevision
            ?? string.Empty;
        Description =
            snapshot.Description
            ?? string.Empty;

        ReconcileProperties(
            snapshot.Properties);
        ReconcileCommands(
            snapshot.Commands);
        ReconcileEvents(
            snapshot.Events);
    }

    private void ReconcileProperties(
        IReadOnlyList<DesktopRuntimePropertySnapshot> snapshots)
    {
        var existingById =
            Properties.ToDictionary(
                property =>
                    property.PropertyId,
                StringComparer.Ordinal);
        var desiredIds =
            snapshots
                .Select(
                    property =>
                        property.PropertyId)
                .ToHashSet(
                    StringComparer.Ordinal);

        for (
            int index = Properties.Count - 1;
            index >= 0;
            index--)
        {
            if (!desiredIds.Contains(
                    Properties[index].PropertyId))
            {
                Properties.RemoveAt(
                    index);
            }
        }

        foreach (
            DesktopRuntimePropertySnapshot snapshot
            in snapshots)
        {
            if (existingById.TryGetValue(
                    snapshot.PropertyId,
                    out DesktopRuntimePropertyViewModel? existing))
            {
                existing.Update(
                    snapshot);
            }
            else
            {
                Properties.Add(
                    new DesktopRuntimePropertyViewModel(
                        snapshot));
            }
        }

        for (
            int desiredIndex = 0;
            desiredIndex < snapshots.Count;
            desiredIndex++)
        {
            string desiredPropertyId =
                snapshots[desiredIndex].PropertyId;
            int currentIndex =
                FindPropertyIndex(
                    desiredPropertyId);

            if (currentIndex >= 0
                && currentIndex != desiredIndex)
            {
                Properties.Move(
                    currentIndex,
                    desiredIndex);
            }
        }

        OnPropertyChanged(
            nameof(PropertyCount));
    }

    private int FindPropertyIndex(
        string propertyId)
    {
        for (
            int index = 0;
            index < Properties.Count;
            index++)
        {
            if (string.Equals(
                    Properties[index].PropertyId,
                    propertyId,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void ReconcileCommands(
        IReadOnlyList<DesktopRuntimeCommandSnapshot> snapshots)
    {
        var desiredPaths =
            snapshots
                .Select(
                    command =>
                        command.Path)
                .ToHashSet(
                    StringComparer.Ordinal);

        for (
            int index = Commands.Count - 1;
            index >= 0;
            index--)
        {
            if (!desiredPaths.Contains(
                    Commands[index].Path))
            {
                Commands.RemoveAt(
                    index);
            }
        }

        for (
            int desiredIndex = 0;
            desiredIndex < snapshots.Count;
            desiredIndex++)
        {
            DesktopRuntimeCommandSnapshot snapshot =
                snapshots[desiredIndex];
            int currentIndex =
                FindCommandIndex(
                    snapshot.Path);

            if (currentIndex < 0)
            {
                Commands.Insert(
                    desiredIndex,
                    new DesktopRuntimeCommandViewModel(
                        snapshot));
                continue;
            }

            DesktopRuntimeCommandViewModel existing =
                Commands[currentIndex];

            if (!existing.HasSameDescriptor(
                    snapshot))
            {
                Commands[currentIndex] =
                    new DesktopRuntimeCommandViewModel(
                        snapshot);
            }
            else
            {
                existing.Update(
                    snapshot);
            }

            currentIndex =
                FindCommandIndex(
                    snapshot.Path);

            if (currentIndex != desiredIndex)
            {
                Commands.Move(
                    currentIndex,
                    desiredIndex);
            }
        }

        OnPropertyChanged(
            nameof(CommandCount));
        ReconcileCommandPresentation();
    }

    private void ReconcileCommandPresentation()
    {
        string? selectedPath =
            SelectedModeCommand?.Path;
        DesktopRuntimeCommandViewModel[] candidates =
            Commands
                .Where(
                    command =>
                        command.IsModeSelectionCandidate)
                .ToArray();
        bool hasCompleteSelector =
            candidates.Length == ModeSelectionOrder.Length
            && ModeSelectionOrder.All(
                label =>
                    candidates.Count(
                        command =>
                            string.Equals(
                                command.ModeSelectionLabel,
                                label,
                                StringComparison.Ordinal))
                    == 1);
        DesktopRuntimeCommandViewModel[] inputCandidates =
            Commands
                .Where(
                    command =>
                        command.IsInputControlCandidate)
                .ToArray();
        bool hasCompleteInputControls =
            inputCandidates.Length == InputControlOrder.Length
            && InputControlOrder.All(
                label =>
                    inputCandidates.Count(
                        command =>
                            string.Equals(
                                command.InputControlLabel,
                                label,
                                StringComparison.Ordinal))
                    == 1);
        DesktopRuntimeCommandViewModel? shortCircuitActivation =
            Commands.SingleOrDefault(
                command =>
                    command.IsConfirmedShortCircuitActivation);
        bool hasShortCircuitActivationControl =
            hasCompleteInputControls
            && shortCircuitActivation is not null;

        ReplaceContents(
            ModeSelectionCommands,
            hasCompleteSelector
                ? ModeSelectionOrder.Select(
                    label =>
                        candidates.Single(
                            command =>
                                string.Equals(
                                    command.ModeSelectionLabel,
                                    label,
                                    StringComparison.Ordinal)))
                : []);
        ReplaceContents(
            InputControlCommands,
            hasCompleteInputControls
                ? InputControlOrder.Select(
                    label =>
                        inputCandidates.Single(
                            command =>
                                string.Equals(
                                    command.InputControlLabel,
                                    label,
                                    StringComparison.Ordinal)))
                : []);
        ReplaceContents(
            GeneralCommands,
            Commands.Where(
                command =>
                    (!hasCompleteSelector
                        || !command.IsModeSelectionCandidate)
                    && (!hasCompleteInputControls
                        || !command.IsInputControlCandidate)
                    && (!hasShortCircuitActivationControl
                        || !ReferenceEquals(
                            command,
                            shortCircuitActivation))));

        SelectedModeCommand =
            selectedPath is null
                ? null
                : ModeSelectionCommands.SingleOrDefault(
                    command =>
                        string.Equals(
                            command.Path,
                            selectedPath,
                            StringComparison.Ordinal));
        OnPropertyChanged(
            nameof(HasModeSelectionSelector));
        OnPropertyChanged(
            nameof(HasInputControlControls));
        OnPropertyChanged(
            nameof(ActivateInputCommand));
        OnPropertyChanged(
            nameof(DeactivateInputCommand));
        OnPropertyChanged(
            nameof(ShortCircuitActivationCommand));
        OnPropertyChanged(
            nameof(HasShortCircuitActivationControl));
    }

    private static void ReplaceContents<T>(
        ObservableCollection<T> destination,
        IEnumerable<T> source)
    {
        destination.Clear();
        foreach (T item in source)
        {
            destination.Add(item);
        }
    }

    private int FindCommandIndex(
        string path)
    {
        for (
            int index = 0;
            index < Commands.Count;
            index++)
        {
            if (string.Equals(
                    Commands[index].Path,
                    path,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void ReconcileEvents(
        IReadOnlyList<DesktopRuntimeEventSnapshot> snapshots)
    {
        var desiredPaths =
            snapshots
                .Select(
                    eventSnapshot =>
                        eventSnapshot.Path)
                .ToHashSet(
                    StringComparer.Ordinal);

        for (
            int index = Events.Count - 1;
            index >= 0;
            index--)
        {
            if (!desiredPaths.Contains(
                    Events[index].Path))
            {
                Events.RemoveAt(
                    index);
            }
        }

        for (
            int desiredIndex = 0;
            desiredIndex < snapshots.Count;
            desiredIndex++)
        {
            DesktopRuntimeEventSnapshot snapshot =
                snapshots[desiredIndex];
            int currentIndex =
                FindEventIndex(
                    snapshot.Path);

            if (currentIndex < 0)
            {
                Events.Insert(
                    desiredIndex,
                    new DesktopRuntimeEventViewModel(
                        snapshot));
                continue;
            }

            DesktopRuntimeEventViewModel existing =
                Events[currentIndex];

            if (!existing.HasSameDescriptor(
                    snapshot))
            {
                Events[currentIndex] =
                    new DesktopRuntimeEventViewModel(
                        snapshot);
            }

            currentIndex =
                FindEventIndex(
                    snapshot.Path);

            if (currentIndex != desiredIndex)
            {
                Events.Move(
                    currentIndex,
                    desiredIndex);
            }
        }

        OnPropertyChanged(
            nameof(EventCount));
    }

    private int FindEventIndex(
        string path)
    {
        for (
            int index = 0;
            index < Events.Count;
            index++)
        {
            if (string.Equals(
                    Events[index].Path,
                    path,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
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

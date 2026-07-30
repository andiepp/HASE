using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hase.Runtime.Diagnostics;
using Prism.Commands;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class RuntimeDiagnosticsViewModel
    : INotifyPropertyChanged
{
    private readonly IDesktopRuntimeDiagnosticSource source;
    private readonly Dictionary<long, DesktopRuntimeDiagnosticEntry>
        retainedEntries =
            [];
    private readonly ObservableCollection<
        DesktopRuntimeDiagnosticEntry> entries =
            [];

    private DesktopRuntimeDiagnosticEntry? selectedEntry;
    private RuntimeDiagnosticLevel selectedDisplayMaximumLevel;

    public RuntimeDiagnosticsViewModel(
        IDesktopRuntimeDiagnosticSource? source = null)
    {
        this.source =
            source
            ?? EmptyDiagnosticSource.Instance;

        CaptureMaximumLevel =
            this.source.MaximumLevel;

        AvailableDisplayLevels =
            Array.AsReadOnly(
                Enum.GetValues<RuntimeDiagnosticLevel>()
                    .Where(
                        level =>
                            level <= CaptureMaximumLevel)
                    .ToArray());

        selectedDisplayMaximumLevel =
            CaptureMaximumLevel;

        Entries =
            new ReadOnlyObservableCollection<
                DesktopRuntimeDiagnosticEntry>(
                    entries);

        ClearDiagnosticsCommand =
            new DelegateCommand(
                ClearDiagnostics,
                () =>
                    retainedEntries.Count > 0);
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public ReadOnlyObservableCollection<
        DesktopRuntimeDiagnosticEntry> Entries
    {
        get;
    }

    public IReadOnlyList<RuntimeDiagnosticLevel>
        AvailableDisplayLevels
    {
        get;
    }

    public RuntimeDiagnosticLevel CaptureMaximumLevel
    {
        get;
    }

    public string CaptureMaximumLevelText =>
        CaptureMaximumLevel.ToString();

    public bool IsByteCaptureEnabled =>
        CaptureMaximumLevel
        == RuntimeDiagnosticLevel.Bytes;

    public RuntimeDiagnosticLevel SelectedDisplayMaximumLevel
    {
        get =>
            selectedDisplayMaximumLevel;

        set
        {
            if (!AvailableDisplayLevels.Contains(
                    value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The display level exceeds the session capture level.");
            }

            if (selectedDisplayMaximumLevel == value)
            {
                return;
            }

            selectedDisplayMaximumLevel =
                value;

            ApplyDisplayFilter();
            OnPropertyChanged();
        }
    }

    public DesktopRuntimeDiagnosticEntry? SelectedEntry
    {
        get =>
            selectedEntry;

        set
        {
            if (ReferenceEquals(
                    selectedEntry,
                    value))
            {
                return;
            }

            selectedEntry =
                value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasSelection));
        }
    }

    public int RetainedEntryCount =>
        retainedEntries.Count;

    public int DisplayedEntryCount =>
        entries.Count;

    public bool IsEmpty =>
        entries.Count == 0;

    public bool HasSelection =>
        SelectedEntry is not null;

    public DelegateCommand ClearDiagnosticsCommand
    {
        get;
    }

    public void Refresh()
    {
        IReadOnlyList<RuntimeDiagnosticRecord> snapshot =
            source.CaptureDiagnostics();

        if (snapshot.Count == 0)
        {
            retainedEntries.Clear();
            ApplyDisplayFilter();

            return;
        }

        if (IsReplacementSession(
                snapshot))
        {
            retainedEntries.Clear();
        }

        HashSet<long> retainedSequences =
            snapshot
                .Select(
                    record =>
                        record.Sequence)
                .ToHashSet();

        foreach (long sequence
            in retainedEntries.Keys
                .Where(
                    sequence =>
                        !retainedSequences.Contains(
                            sequence))
                .ToArray())
        {
            retainedEntries.Remove(
                sequence);
        }

        foreach (RuntimeDiagnosticRecord record
            in snapshot)
        {
            if (!retainedEntries.ContainsKey(
                    record.Sequence))
            {
                retainedEntries.Add(
                    record.Sequence,
                    DesktopRuntimeDiagnosticEntryProjector.Project(
                        record));
            }
        }

        ApplyDisplayFilter();
    }

    private bool IsReplacementSession(
        IReadOnlyList<RuntimeDiagnosticRecord> snapshot)
    {
        if (retainedEntries.Count == 0)
        {
            return false;
        }

        bool foundOverlap =
            false;

        foreach (RuntimeDiagnosticRecord record
            in snapshot)
        {
            if (!retainedEntries.TryGetValue(
                    record.Sequence,
                    out DesktopRuntimeDiagnosticEntry? existing))
            {
                continue;
            }

            foundOverlap =
                true;

            if (existing.TimestampUtc != record.TimestampUtc
                || existing.Level != record.Level
                || existing.Category != record.Category
                || !string.Equals(
                    existing.EventName,
                    record.EventName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return !foundOverlap;
    }

    private void ApplyDisplayFilter()
    {
        long? selectedSequence =
            SelectedEntry?.Sequence;

        SelectedEntry =
            null;

        entries.Clear();

        foreach (DesktopRuntimeDiagnosticEntry entry
            in retainedEntries.Values
                .Where(
                    entry =>
                        entry.Level
                        <= SelectedDisplayMaximumLevel)
                .OrderByDescending(
                    entry =>
                        entry.Sequence))
        {
            entries.Add(
                entry);
        }

        SelectedEntry =
            selectedSequence is null
                ? entries.FirstOrDefault()
                : entries.FirstOrDefault(
                    entry =>
                        entry.Sequence
                        == selectedSequence.Value)
                    ?? entries.FirstOrDefault();

        OnPropertyChanged(
            nameof(RetainedEntryCount));
        OnPropertyChanged(
            nameof(DisplayedEntryCount));
        OnPropertyChanged(
            nameof(IsEmpty));

        ClearDiagnosticsCommand.RaiseCanExecuteChanged();
    }

    private void ClearDiagnostics()
    {
        source.ClearDiagnostics();
        Refresh();
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }

    private sealed class EmptyDiagnosticSource
        : IDesktopRuntimeDiagnosticSource
    {
        public static EmptyDiagnosticSource Instance
        {
            get;
        } =
            new();

        public RuntimeDiagnosticLevel MaximumLevel =>
            RuntimeDiagnosticLevel.Operational;

        public IReadOnlyList<RuntimeDiagnosticRecord> CaptureDiagnostics()
        {
            return [];
        }

        public void ClearDiagnostics()
        {
        }
    }
}

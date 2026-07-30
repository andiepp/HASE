using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class RuntimeDiagnosticsViewModel
    : INotifyPropertyChanged
{
    private readonly IDesktopRuntimeDiagnosticSource source;
    private readonly ObservableCollection<
        DesktopRuntimeDiagnosticEntry> entries =
            [];

    private DesktopRuntimeDiagnosticEntry? selectedEntry;

    public RuntimeDiagnosticsViewModel(
        IDesktopRuntimeDiagnosticSource? source = null)
    {
        this.source =
            source
            ?? EmptyDiagnosticSource.Instance;

        Entries =
            new ReadOnlyObservableCollection<
                DesktopRuntimeDiagnosticEntry>(
                    entries);
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public ReadOnlyObservableCollection<
        DesktopRuntimeDiagnosticEntry> Entries
    {
        get;
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
        entries.Count;

    public bool IsEmpty =>
        entries.Count == 0;

    public bool HasSelection =>
        SelectedEntry is not null;

    public void Refresh()
    {
        IReadOnlyList<RuntimeDiagnosticRecord> snapshot =
            source.CaptureDiagnostics();

        if (snapshot.Count == 0)
        {
            ReplaceAll(
                []);

            return;
        }

        if (IsReplacementSession(
                snapshot))
        {
            ReplaceAll(
                snapshot);

            return;
        }

        HashSet<long> retainedSequences =
            snapshot
                .Select(
                    record =>
                        record.Sequence)
                .ToHashSet();

        for (
            int index = entries.Count - 1;
            index >= 0;
            index--)
        {
            if (!retainedSequences.Contains(
                    entries[index].Sequence))
            {
                entries.RemoveAt(
                    index);
            }
        }

        HashSet<long> projectedSequences =
            entries
                .Select(
                    entry =>
                        entry.Sequence)
                .ToHashSet();

        foreach (RuntimeDiagnosticRecord record
            in snapshot
                .Where(
                    record =>
                        !projectedSequences.Contains(
                            record.Sequence))
                .OrderBy(
                    record =>
                        record.Sequence))
        {
            entries.Insert(
                0,
                DesktopRuntimeDiagnosticEntryProjector.Project(
                    record));
        }

        ReconcileSelection();
        NotifyCollectionState();
    }

    private bool IsReplacementSession(
        IReadOnlyList<RuntimeDiagnosticRecord> snapshot)
    {
        if (entries.Count == 0)
        {
            return false;
        }

        Dictionary<long, DesktopRuntimeDiagnosticEntry> existingBySequence =
            entries.ToDictionary(
                entry =>
                    entry.Sequence);

        bool foundOverlap =
            false;

        foreach (RuntimeDiagnosticRecord record
            in snapshot)
        {
            if (!existingBySequence.TryGetValue(
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

    private void ReplaceAll(
        IReadOnlyList<RuntimeDiagnosticRecord> snapshot)
    {
        long? selectedSequence =
            SelectedEntry?.Sequence;

        entries.Clear();

        foreach (RuntimeDiagnosticRecord record
            in snapshot.OrderByDescending(
                record =>
                    record.Sequence))
        {
            entries.Add(
                DesktopRuntimeDiagnosticEntryProjector.Project(
                    record));
        }

        SelectedEntry =
            selectedSequence is null
                ? entries.FirstOrDefault()
                : entries.FirstOrDefault(
                    entry =>
                        entry.Sequence
                        == selectedSequence.Value)
                    ?? entries.FirstOrDefault();

        NotifyCollectionState();
    }

    private void ReconcileSelection()
    {
        if (SelectedEntry is not null
            && entries.Contains(
                SelectedEntry))
        {
            return;
        }

        SelectedEntry =
            entries.FirstOrDefault();
    }

    private void NotifyCollectionState()
    {
        OnPropertyChanged(
            nameof(RetainedEntryCount));
        OnPropertyChanged(
            nameof(IsEmpty));
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

        public IReadOnlyList<RuntimeDiagnosticRecord> CaptureDiagnostics()
        {
            return [];
        }

        public void ClearDiagnostics()
        {
        }
    }
}

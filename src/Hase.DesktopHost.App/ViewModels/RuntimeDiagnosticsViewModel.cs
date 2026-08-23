using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Hase.Diagnostics.Export;
using Hase.Runtime.Diagnostics;
using Prism.Commands;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class RuntimeDiagnosticsViewModel
    : INotifyPropertyChanged
{
    private readonly IDesktopRuntimeDiagnosticSource source;
    private readonly DesktopRuntimeByteInterpretationService
        byteInterpretationService;
    private readonly Dictionary<long, DesktopRuntimeDiagnosticEntry>
        retainedEntries =
            [];
    private readonly ObservableCollection<
        DesktopRuntimeDiagnosticEntry> entries =
            [];

    private readonly IDesktopDiagnosticExportDialogService?
        exportDialogService;
    private readonly string? hostIdentity;
    private readonly Func<DateTimeOffset> utcNow;

    private DesktopRuntimeDiagnosticEntry? selectedEntry;
    private RuntimeDiagnosticLevel selectedDisplayMaximumLevel;
    private bool isPresentationPaused;
    private bool isExporting;
    private string exportStatusText =
        string.Empty;

    public RuntimeDiagnosticsViewModel(
        IDesktopRuntimeDiagnosticSource? source = null,
        DesktopRuntimeByteInterpretationService?
            byteInterpretationService = null,
        IDesktopDiagnosticExportDialogService? exportDialogService = null,
        DesktopRuntimeHostShellInformation? shellInformation = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.source =
            source
            ?? EmptyDiagnosticSource.Instance;
        this.byteInterpretationService =
            byteInterpretationService
            ?? DesktopRuntimeByteInterpretationService.CreateDefault();
        this.exportDialogService =
            exportDialogService;
        hostIdentity =
            shellInformation?.HostIdentity;
        this.utcNow =
            utcNow
            ?? (() => DateTimeOffset.UtcNow);

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

        PausePresentationCommand =
            new DelegateCommand(
                PausePresentation,
                () =>
                    !IsPresentationPaused);

        ResumePresentationCommand =
            new DelegateCommand(
                ResumePresentation,
                () =>
                    IsPresentationPaused);

        ExportDiagnosticsCommand =
            new DelegateCommand(
                async () =>
                    await ExportDiagnosticsAsync(),
                () =>
                    !isExporting);
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

    public bool IsPresentationPaused
    {
        get =>
            isPresentationPaused;

        private set
        {
            if (isPresentationPaused == value)
            {
                return;
            }

            isPresentationPaused =
                value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(PresentationStatusText));
            OnPropertyChanged(
                nameof(PresentationStatusDescription));
            PausePresentationCommand.RaiseCanExecuteChanged();
            ResumePresentationCommand.RaiseCanExecuteChanged();
        }
    }

    public string PresentationStatusText =>
        IsPresentationPaused
            ? "Presentation: Paused"
            : "Presentation: Running";

    public string PresentationStatusDescription =>
        IsPresentationPaused
            ? "Presentation is paused. Diagnostic capture and bounded retention continue."
            : "Presentation updates automatically from the retained diagnostic session.";

    public DelegateCommand ClearDiagnosticsCommand
    {
        get;
    }

    public DelegateCommand PausePresentationCommand
    {
        get;
    }

    public DelegateCommand ResumePresentationCommand
    {
        get;
    }

    public DelegateCommand ExportDiagnosticsCommand
    {
        get;
    }

    public string ExportStatusText
    {
        get =>
            exportStatusText;

        private set
        {
            if (string.Equals(
                    exportStatusText,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            exportStatusText =
                value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasExportStatus));
        }
    }

    public bool HasExportStatus =>
        ExportStatusText.Length > 0;

    /// <summary>
    /// Exports the complete retained diagnostic session to an
    /// operator-chosen file, independent of the display filter and of
    /// presentation pause. Never overwrites an existing file.
    /// </summary>
    public async Task ExportDiagnosticsAsync()
    {
        if (isExporting
            || exportDialogService is null)
        {
            return;
        }

        isExporting =
            true;
        ExportDiagnosticsCommand.RaiseCanExecuteChanged();

        try
        {
            DateTimeOffset exportedAtUtc =
                utcNow().ToUniversalTime();

            string suggestedFileName =
                "runtime-host-diagnostics-"
                + exportedAtUtc.ToString(
                    "yyyyMMdd-HHmmss")
                + "Z.jsonl";

            string? targetPath =
                exportDialogService.SelectExportTarget(
                    suggestedFileName);

            if (targetPath is null)
            {
                ExportStatusText =
                    "Export cancelled.";

                return;
            }

            IReadOnlyList<RuntimeDiagnosticRecord> records =
                source.CaptureDiagnostics();

            DiagnosticExportDocument document =
                RuntimeHostDiagnosticExport.ToDocument(
                    source.MaximumLevel,
                    hostIdentity,
                    exportedAtUtc,
                    records);

            await DiagnosticExportFile.WriteNewAsync(
                targetPath,
                document);

            ExportStatusText =
                $"Exported {records.Count} records to "
                + $"{Path.GetFileName(targetPath)}.";
        }
        catch (Exception exception)
        {
            ExportStatusText =
                $"Export failed: {exception.Message}";
        }
        finally
        {
            isExporting =
                false;
            ExportDiagnosticsCommand.RaiseCanExecuteChanged();
        }
    }

    public void Refresh()
    {
        if (IsPresentationPaused)
        {
            return;
        }

        ReconcileSourceSnapshot();
    }

    private void ReconcileSourceSnapshot()
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
                        record,
                        byteInterpretationService));
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
        retainedEntries.Clear();
        ApplyDisplayFilter();
    }

    private void PausePresentation()
    {
        IsPresentationPaused =
            true;
    }

    private void ResumePresentation()
    {
        IsPresentationPaused =
            false;
        ReconcileSourceSnapshot();
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

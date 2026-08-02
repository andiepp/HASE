using Hase.Client.Diagnostics;
using Prism.Commands;
using Prism.Mvvm;
using Hase.Client.Configuration;

namespace Hase.Client.Wpf.ViewModels;

public sealed class ClientDiagnosticsViewModel : BindableBase
{
    private readonly BoundedClientDiagnosticCollector collector;
    private IReadOnlyList<ClientDiagnosticRecord> records = [];
    private IReadOnlyList<ClientDiagnosticRecord> presentationSource = [];
    private ClientDiagnosticRecord? selectedRecord;
    private string selectedLevelFilter = "All";
    private string selectedCategoryFilter = "All";
    private long evictedRecordCount;
    private bool isPaused;
    private int pendingRecordCount;
    private long presentationWatermark;
    private RuntimeHostDiagnosticFilterItem selectedRuntimeHostFilter;

    public ClientDiagnosticsViewModel(BoundedClientDiagnosticCollector collector)
    {
        this.collector = collector ?? throw new ArgumentNullException(nameof(collector));
        LevelFilters = new[] { "All" }
            .Concat(Enum.GetNames<ClientDiagnosticLevel>())
            .ToArray();
        CategoryFilters = new[] { "All" }
            .Concat(Enum.GetNames<ClientDiagnosticCategory>())
            .ToArray();
        RuntimeHostFilters = [new RuntimeHostDiagnosticFilterItem("All Runtime Hosts", null)];
        selectedRuntimeHostFilter = RuntimeHostFilters[0];
        ClearCommand = new DelegateCommand(Clear);
        PauseCommand = new DelegateCommand(Pause, () => !IsPaused);
        ResumeCommand = new DelegateCommand(Resume, () => IsPaused);
        Refresh();
    }

    public string Title => "HASE Laptop Client Diagnostics";
    public IReadOnlyList<string> LevelFilters { get; }
    public IReadOnlyList<string> CategoryFilters { get; }
    public IReadOnlyList<RuntimeHostDiagnosticFilterItem> RuntimeHostFilters { get; private set; }
    public DelegateCommand ClearCommand { get; }
    public DelegateCommand PauseCommand { get; }
    public DelegateCommand ResumeCommand { get; }
    public IReadOnlyList<ClientDiagnosticRecord> Records => records;
    public int RecordCount => records.Count;
    public long EvictedRecordCount => evictedRecordCount;
    public bool IsPaused => isPaused;
    public string PresentationState => IsPaused ? "Paused" : "Running";
    public int PendingRecordCount => pendingRecordCount;
    public bool IsBytesUnavailable =>
        string.Equals(SelectedLevelFilter, nameof(ClientDiagnosticLevel.Bytes), StringComparison.Ordinal);
    public string BytesUnavailableMessage => IsBytesUnavailable
        ? "Exact gRPC / HTTP/2 / TLS transport bytes are unavailable at the client application boundary. No reconstructed values are presented as captured bytes."
        : string.Empty;

    public string SelectedLevelFilter
    {
        get => selectedLevelFilter;
        set
        {
            if (SetProperty(ref selectedLevelFilter, value))
            {
                RaisePropertyChanged(nameof(IsBytesUnavailable));
                RaisePropertyChanged(nameof(BytesUnavailableMessage));
                ApplyFilter();
            }
        }
    }

    public string SelectedCategoryFilter
    {
        get => selectedCategoryFilter;
        set
        {
            if (SetProperty(ref selectedCategoryFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public RuntimeHostDiagnosticFilterItem SelectedRuntimeHostFilter
    {
        get => selectedRuntimeHostFilter;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!RuntimeHostFilters.Contains(value))
                throw new ArgumentException("The Runtime Host filter is not available.", nameof(value));
            if (SetProperty(ref selectedRuntimeHostFilter, value)) ApplyFilter();
        }
    }

    public void ConfigureRuntimeHosts(RuntimeHostProfileRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        RuntimeHostFilters = new[] { new RuntimeHostDiagnosticFilterItem("All Runtime Hosts", null) }
            .Concat(registry.Profiles.Select(profile =>
                new RuntimeHostDiagnosticFilterItem(profile.DisplayName, profile.ProfileId)))
            .ToArray();
        selectedRuntimeHostFilter = RuntimeHostFilters[0];
        RaisePropertyChanged(nameof(RuntimeHostFilters));
        RaisePropertyChanged(nameof(SelectedRuntimeHostFilter));
        ApplyFilter();
    }

    public ClientDiagnosticRecord? SelectedRecord
    {
        get => selectedRecord;
        set
        {
            if (SetProperty(ref selectedRecord, value))
            {
                RaisePropertyChanged(nameof(MetadataText));
            }
        }
    }

    public string MetadataText => SelectedRecord is null
        ? string.Empty
        : string.Join(
            Environment.NewLine,
            SelectedRecord.Metadata
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}: {item.Value}"));

    public void Refresh()
    {
        ClientDiagnosticSnapshot snapshot = collector.GetSnapshot();
        SetProperty(ref evictedRecordCount, snapshot.EvictedRecordCount, nameof(EvictedRecordCount));
        long newestSequence = snapshot.Records.LastOrDefault()?.Sequence ?? presentationWatermark;

        if (IsPaused)
        {
            SetPendingRecordCount(
                snapshot.Records.Count(record => record.Sequence > presentationWatermark));
            return;
        }

        presentationSource = snapshot.Records;
        presentationWatermark = newestSequence;
        SetPendingRecordCount(0);
        ApplyFilter();
    }

    private void Clear()
    {
        ClientDiagnosticSnapshot beforeClear = collector.GetSnapshot();
        presentationWatermark = Math.Max(
            presentationWatermark,
            beforeClear.Records.LastOrDefault()?.Sequence ?? presentationWatermark);
        collector.Clear();
        presentationSource = [];
        SetProperty(ref records, Array.Empty<ClientDiagnosticRecord>(), nameof(Records));
        SetProperty(ref evictedRecordCount, 0, nameof(EvictedRecordCount));
        SetPendingRecordCount(0);
        SelectedRecord = null;
        RaisePropertyChanged(nameof(RecordCount));
    }

    private void Pause()
    {
        Refresh();
        SetProperty(ref isPaused, true, nameof(IsPaused));
        RaisePropertyChanged(nameof(PresentationState));
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
    }

    private void Resume()
    {
        SetProperty(ref isPaused, false, nameof(IsPaused));
        RaisePropertyChanged(nameof(PresentationState));
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        Refresh();
    }

    private void ApplyFilter()
    {
        ClientDiagnosticLevel? level = ParseFilter<ClientDiagnosticLevel>(SelectedLevelFilter);
        ClientDiagnosticCategory? category = ParseFilter<ClientDiagnosticCategory>(SelectedCategoryFilter);
        RuntimeHostProfileId? profileId = SelectedRuntimeHostFilter.ProfileId;
        long? selectedSequence = SelectedRecord?.Sequence;
        IReadOnlyList<ClientDiagnosticRecord> filtered = presentationSource
            .Where(record =>
                (level is null || record.Level <= level) &&
                (category is null || record.Category == category) &&
                (profileId is null || record.SessionContext?.ProfileId == profileId))
            .ToArray();

        SetProperty(ref records, filtered, nameof(Records));
        RaisePropertyChanged(nameof(RecordCount));
        SelectedRecord = selectedSequence is null
            ? records.LastOrDefault()
            : records.FirstOrDefault(record => record.Sequence == selectedSequence)
                ?? records.LastOrDefault();
    }

    private void SetPendingRecordCount(int value)
    {
        SetProperty(ref pendingRecordCount, value, nameof(PendingRecordCount));
    }

    private static TEnum? ParseFilter<TEnum>(string value)
        where TEnum : struct, Enum =>
        string.Equals(value, "All", StringComparison.Ordinal)
            ? null
            : Enum.Parse<TEnum>(value);
}

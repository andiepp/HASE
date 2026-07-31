using Hase.Client.Diagnostics;
using Prism.Commands;
using Prism.Mvvm;

namespace Hase.Client.Wpf.ViewModels;

public sealed class ClientDiagnosticsViewModel : BindableBase
{
    private readonly BoundedClientDiagnosticCollector collector;
    private IReadOnlyList<ClientDiagnosticRecord> records = [];
    private ClientDiagnosticRecord? selectedRecord;
    private string selectedLevelFilter = "All";
    private string selectedCategoryFilter = "All";
    private long evictedRecordCount;

    public ClientDiagnosticsViewModel(BoundedClientDiagnosticCollector collector)
    {
        this.collector = collector ?? throw new ArgumentNullException(nameof(collector));
        LevelFilters = new[] { "All" }
            .Concat(Enum.GetNames<ClientDiagnosticLevel>())
            .ToArray();
        CategoryFilters = new[] { "All" }
            .Concat(Enum.GetNames<ClientDiagnosticCategory>())
            .ToArray();
        ClearCommand = new DelegateCommand(Clear);
        Refresh();
    }

    public string Title => "HASE Laptop Client Diagnostics";
    public IReadOnlyList<string> LevelFilters { get; }
    public IReadOnlyList<string> CategoryFilters { get; }
    public DelegateCommand ClearCommand { get; }
    public IReadOnlyList<ClientDiagnosticRecord> Records => records;
    public int RecordCount => records.Count;
    public long EvictedRecordCount => evictedRecordCount;

    public string SelectedLevelFilter
    {
        get => selectedLevelFilter;
        set
        {
            if (SetProperty(ref selectedLevelFilter, value))
            {
                Refresh();
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
                Refresh();
            }
        }
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
        ClientDiagnosticLevel? level = ParseFilter<ClientDiagnosticLevel>(SelectedLevelFilter);
        ClientDiagnosticCategory? category = ParseFilter<ClientDiagnosticCategory>(SelectedCategoryFilter);
        ClientDiagnosticSnapshot snapshot = collector.GetSnapshot(level, category);
        long? selectedSequence = SelectedRecord?.Sequence;

        SetProperty(ref records, snapshot.Records, nameof(Records));
        SetProperty(ref evictedRecordCount, snapshot.EvictedRecordCount, nameof(EvictedRecordCount));
        RaisePropertyChanged(nameof(RecordCount));

        SelectedRecord = selectedSequence is null
            ? records.LastOrDefault()
            : records.FirstOrDefault(record => record.Sequence == selectedSequence)
                ?? records.LastOrDefault();
    }

    private void Clear()
    {
        collector.Clear();
        Refresh();
    }

    private static TEnum? ParseFilter<TEnum>(string value)
        where TEnum : struct, Enum =>
        string.Equals(value, "All", StringComparison.Ordinal)
            ? null
            : Enum.Parse<TEnum>(value);
}

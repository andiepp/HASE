using System.Collections.ObjectModel;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class OperatorActivityViewModel
{
    public const int Capacity =
        100;

    private readonly ObservableCollection<
        DesktopRuntimeOperatorActivityEntry> entries =
            [];
    private readonly TimeProvider timeProvider;

    public OperatorActivityViewModel()
        : this(
            TimeProvider.System)
    {
    }

    public OperatorActivityViewModel(
        TimeProvider timeProvider)
    {
        this.timeProvider =
            timeProvider
            ?? throw new ArgumentNullException(
                nameof(timeProvider));
        Entries =
            new ReadOnlyObservableCollection<
                DesktopRuntimeOperatorActivityEntry>(
                    entries);
    }

    public ReadOnlyObservableCollection<
        DesktopRuntimeOperatorActivityEntry> Entries
    {
        get;
    }

    public void Record(
        DesktopRuntimeOperatorActivityKind kind,
        string endpointId,
        string attachmentGeneration,
        string instrumentId,
        string operationPath,
        string inputSummary,
        DesktopRuntimeOperatorActivityOutcome outcome,
        string diagnostic = "",
        string reconciliation = "")
    {
        entries.Insert(
            0,
            new DesktopRuntimeOperatorActivityEntry(
                timeProvider.GetUtcNow()
                    .ToUniversalTime(),
                kind,
                endpointId,
                attachmentGeneration,
                instrumentId,
                operationPath,
                inputSummary,
                outcome,
                diagnostic,
                reconciliation));

        if (entries.Count > Capacity)
        {
            entries.RemoveAt(
                entries.Count - 1);
        }
    }
}

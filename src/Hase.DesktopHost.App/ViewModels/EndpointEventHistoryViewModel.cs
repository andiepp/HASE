using System.Collections.ObjectModel;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class EndpointEventHistoryViewModel
{
    public const int Capacity =
        100;

    private readonly ObservableCollection<
        DesktopRuntimeEventOccurrence> occurrences =
            [];

    public EndpointEventHistoryViewModel()
    {
        Occurrences =
            new ReadOnlyObservableCollection<
                DesktopRuntimeEventOccurrence>(
                    occurrences);
    }

    public ReadOnlyObservableCollection<
        DesktopRuntimeEventOccurrence> Occurrences
    {
        get;
    }

    public void Record(
        DesktopRuntimeEventOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(
            occurrence);

        occurrences.Insert(
            0,
            occurrence);

        if (occurrences.Count > Capacity)
        {
            occurrences.RemoveAt(
                occurrences.Count - 1);
        }
    }
}

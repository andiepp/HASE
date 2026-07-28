namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimePropertyViewModel
{
    public DesktopRuntimePropertyViewModel(
        DesktopRuntimePropertySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        PropertyId =
            snapshot.PropertyId;
        DisplayName =
            snapshot.DisplayName;
        Path =
            snapshot.Path;
        Access =
            snapshot.Access;
        Value =
            snapshot.Value;
        Quality =
            snapshot.Quality;
        TimestampUtc =
            snapshot.TimestampUtc;
        IsKnown =
            snapshot.IsKnown;
    }

    public string PropertyId
    {
        get;
    }

    public string DisplayName
    {
        get;
    }

    public string Path
    {
        get;
    }

    public string Access
    {
        get;
    }

    public string Value
    {
        get;
    }

    public string Quality
    {
        get;
    }

    public string TimestampUtc
    {
        get;
    }

    public bool IsKnown
    {
        get;
    }
}

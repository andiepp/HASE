namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimeEventViewModel
{
    public DesktopRuntimeEventViewModel(
        DesktopRuntimeEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        Path =
            string.IsNullOrWhiteSpace(snapshot.Path)
                ? throw new ArgumentException(
                    "The Event path must not be empty.",
                    nameof(snapshot))
                : snapshot.Path;
        DisplayName =
            string.IsNullOrWhiteSpace(snapshot.DisplayName)
                ? throw new ArgumentException(
                    "The Event display name must not be empty.",
                    nameof(snapshot))
                : snapshot.DisplayName;
        Description =
            snapshot.Description
            ?? string.Empty;
    }

    public string Path
    {
        get;
    }

    public string DisplayName
    {
        get;
    }

    public string Description
    {
        get;
    }

    public bool HasSameDescriptor(
        DesktopRuntimeEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        return string.Equals(
                Path,
                snapshot.Path,
                StringComparison.Ordinal)
            && string.Equals(
                DisplayName,
                snapshot.DisplayName,
                StringComparison.Ordinal)
            && string.Equals(
                Description,
                snapshot.Description
                    ?? string.Empty,
                StringComparison.Ordinal);
    }
}

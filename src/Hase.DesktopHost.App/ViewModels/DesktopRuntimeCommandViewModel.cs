namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimeCommandViewModel
{
    public DesktopRuntimeCommandViewModel(
        DesktopRuntimeCommandSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Path =
            string.IsNullOrWhiteSpace(snapshot.Path)
                ? throw new ArgumentException(
                    "The Command path must not be empty.",
                    nameof(snapshot))
                : snapshot.Path;

        DisplayName =
            string.IsNullOrWhiteSpace(snapshot.DisplayName)
                ? throw new ArgumentException(
                    "The Command display name must not be empty.",
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
        DesktopRuntimeCommandSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

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

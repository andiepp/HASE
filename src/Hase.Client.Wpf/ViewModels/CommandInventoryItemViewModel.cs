namespace Hase.Client.Wpf.ViewModels;

public sealed record CommandInventoryItemViewModel(
    RemoteCommandTarget Target,
    string Path,
    string DisplayName,
    string? Description,
    bool CanExecute)
{
    public bool RequiresArgument
    {
        get;
        init;
    }

    public string? ArgumentDisplayName
    {
        get;
        init;
    }

    public string? ArgumentDescription
    {
        get;
        init;
    }

    public string? ArgumentDataType
    {
        get;
        init;
    }
}

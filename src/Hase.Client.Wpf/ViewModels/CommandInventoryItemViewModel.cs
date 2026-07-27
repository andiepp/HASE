namespace Hase.Client.Wpf.ViewModels;

public sealed record CommandInventoryItemViewModel(
    RemoteCommandTarget Target,
    string Path,
    string DisplayName,
    string? Description,
    bool CanExecute);

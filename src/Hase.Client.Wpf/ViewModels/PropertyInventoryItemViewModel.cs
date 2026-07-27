namespace Hase.Client.Wpf.ViewModels;

public sealed record PropertyInventoryItemViewModel(
    RemotePropertyTarget Target,
    string PropertyId,
    string Path,
    string DisplayName,
    string AccessMode,
    string DataType,
    string? Unit,
    string Value,
    string? TimestampUtc,
    string? Quality);

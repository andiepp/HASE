using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hase.Operator.Input;

namespace Hase.Client.Wpf.ViewModels;

public sealed record CommandInventoryItemViewModel(
    RemoteCommandTarget Target,
    string Path,
    string DisplayName,
    string? Description,
    bool EndpointReady)
    : INotifyPropertyChanged
{
    private string requestedArgumentText =
        string.Empty;

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

    public string RequestedArgumentText
    {
        get =>
            requestedArgumentText;
        set
        {
            value ??=
                string.Empty;

            if (requestedArgumentText == value)
            {
                return;
            }

            requestedArgumentText =
                value;
            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasValidArgument));
            OnPropertyChanged(
                nameof(CanExecute));
        }
    }

    public bool IsEditingArgument
    {
        get;
        set;
    }

    public bool HasValidArgument =>
        !RequiresArgument
        || (ArgumentDataType == "ByteArray"
            && ByteArrayHexadecimalParser.TryParse(
                RequestedArgumentText,
                out _));

    public bool CanExecute =>
        EndpointReady
        && HasValidArgument;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}

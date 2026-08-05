using System.ComponentModel;
using System.Runtime.CompilerServices;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
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
    private static readonly IReadOnlyDictionary<string, string>
        Kel103ModeSelectionLabels =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Mode.SelectConstantCurrent"] = "CC",
                ["Mode.SelectConstantVoltage"] = "CV",
                ["Mode.SelectConstantResistance"] = "CR",
                ["Mode.SelectConstantPower"] = "CW",
                ["Mode.SelectShortCircuit"] = "SHORT"
            };

    private string requestedArgumentText =
        string.Empty;

    public CommandDescriptor? Descriptor
    {
        get;
        init;
    }

    public bool RequiresArgument =>
        Descriptor?.Argument is not null;

    public string? ModeSelectionLabel =>
        Descriptor is not null
        && !RequiresArgument
        && string.Equals(
            Path,
            Descriptor.Path.ToString(),
            StringComparison.Ordinal)
        && string.Equals(
            Path,
            Target.CommandPath.ToString(),
            StringComparison.Ordinal)
        && Kel103ModeSelectionLabels.TryGetValue(
            Path,
            out string? label)
                ? label
                : null;

    public bool IsModeSelectionCandidate =>
        ModeSelectionLabel is not null;

    public string? ArgumentDisplayName =>
        Descriptor?.Argument?.DisplayName;

    public string? ArgumentDescription =>
        Descriptor?.Argument?.Description;

    public string? ArgumentDataType =>
        Descriptor?.Argument?.Data switch
        {
            NumericDataDescriptor =>
                "Numeric",
            BooleanDataDescriptor =>
                "Boolean",
            StringDataDescriptor =>
                "String",
            ByteArrayDataDescriptor =>
                "ByteArray",
            null =>
                null,
            DataDescriptor data =>
                data.GetType().Name
        };

    public CommandArgumentEditorKind EditorKind =>
        Descriptor?.Argument?.Data switch
        {
            BooleanDataDescriptor =>
                CommandArgumentEditorKind.Boolean,
            NumericDataDescriptor
                or StringDataDescriptor
                or ByteArrayDataDescriptor =>
                    CommandArgumentEditorKind.Text,
            _ =>
                CommandArgumentEditorKind.None
        };

    public bool HasBooleanEditor =>
        EditorKind
        == CommandArgumentEditorKind.Boolean;

    public bool HasTextEditor =>
        EditorKind
        == CommandArgumentEditorKind.Text;

    public bool HasArgumentEditor =>
        !RequiresArgument
        || EditorKind
            != CommandArgumentEditorKind.None;

    public bool? RequestedBooleanArgument
    {
        get =>
            bool.TryParse(
                requestedArgumentText,
                out bool value)
                    ? value
                    : null;
        set
        {
            string text =
                value?.ToString()
                ?? string.Empty;

            if (requestedArgumentText == text)
            {
                return;
            }

            requestedArgumentText =
                text;
            OnPropertyChanged();
            OnPropertyChanged(
                nameof(RequestedArgumentText));
            RaiseInputStateChanged();
        }
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
                nameof(RequestedBooleanArgument));
            RaiseInputStateChanged();
        }
    }

    public bool IsEditingArgument
    {
        get;
        set;
    }

    public CommandArgumentInputParseResult InputResult =>
        Descriptor is null
            ? CommandArgumentInputParseResult.Parameterless()
            : CommandArgumentInputParser.Parse(
                Descriptor,
                HasBooleanEditor
                    ? RequestedBooleanArgument?.ToString()
                    : RequestedArgumentText);

    public bool HasValidArgument =>
        HasArgumentEditor
        && InputResult.IsSuccess;

    public string ValidationMessage =>
        RequiresArgument
        && !InputResult.IsSuccess
            ? InputResult.Message
            : string.Empty;

    public bool CanExecute =>
        EndpointReady
        && HasValidArgument;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaiseInputStateChanged()
    {
        OnPropertyChanged(
            nameof(InputResult));
        OnPropertyChanged(
            nameof(HasValidArgument));
        OnPropertyChanged(
            nameof(ValidationMessage));
        OnPropertyChanged(
            nameof(CanExecute));
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}

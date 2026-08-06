using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Operator.Input;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.App.ViewModels;

public enum CommandArgumentEditorKind
{
    None,
    Boolean,
    Text
}

public sealed class DesktopRuntimeCommandViewModel
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

    private static readonly IReadOnlyDictionary<string, string>
        Kel103InputControlLabels =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Input.Activate"] = "Activate",
                ["Input.Deactivate"] = "Deactivate"
            };

    private RuntimeHostCommandTarget target;
    private bool isEndpointReady;
    private string requestedArgumentText =
        string.Empty;
    private DesktopRuntimeCommandExecutionState executionState =
        DesktopRuntimeCommandExecutionState.Ready;
    private string executionMessage =
        string.Empty;
    private string returnValue =
        string.Empty;
    private bool hasReturnValue;

    public DesktopRuntimeCommandViewModel(
        DesktopRuntimeCommandSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        target =
            snapshot.Target
            ?? throw new ArgumentException(
                "The Command target must not be null.",
                nameof(snapshot));

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

        Descriptor =
            snapshot.Descriptor
            ?? throw new ArgumentException(
                "The Command descriptor must not be null.",
                nameof(snapshot));

        isEndpointReady =
            snapshot.IsEndpointReady;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RuntimeHostCommandTarget Target
    {
        get =>
            target;
        private set =>
            SetProperty(
                ref target,
                value);
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

    public CommandDescriptor Descriptor
    {
        get;
    }

    public bool RequiresArgument =>
        Descriptor.Argument is not null;

    public string? ModeSelectionLabel =>
        !RequiresArgument
        && string.Equals(
            Path,
            Descriptor.Path.ToString(),
            StringComparison.Ordinal)
        && Kel103ModeSelectionLabels.TryGetValue(
            Path,
            out string? label)
                ? label
                : null;

    public bool IsModeSelectionCandidate =>
        ModeSelectionLabel is not null;

    public string? InputControlLabel =>
        !RequiresArgument
        && string.Equals(
            Path,
            Descriptor.Path.ToString(),
            StringComparison.Ordinal)
        && Kel103InputControlLabels.TryGetValue(
            Path,
            out string? label)
                ? label
                : null;

    public bool IsInputControlCandidate =>
        InputControlLabel is not null;

    public string? ArgumentDisplayName =>
        Descriptor.Argument?.DisplayName;

    public string? ArgumentDescription =>
        Descriptor.Argument?.Description;

    public string? ArgumentDataType =>
        Descriptor.Argument?.Data switch
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
        Descriptor.Argument?.Data switch
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

    public CommandArgumentInputParseResult InputResult =>
        CommandArgumentInputParser.Parse(
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

    public bool IsEndpointReady
    {
        get =>
            isEndpointReady;
        private set =>
            SetProperty(
                ref isEndpointReady,
                value);
    }

    public DesktopRuntimeCommandExecutionState ExecutionState
    {
        get =>
            executionState;
        private set =>
            SetProperty(
                ref executionState,
                value);
    }

    public string ExecutionMessage
    {
        get =>
            executionMessage;
        private set =>
            SetProperty(
                ref executionMessage,
                value);
    }

    public string ReturnValue
    {
        get =>
            returnValue;
        private set =>
            SetProperty(
                ref returnValue,
                value);
    }

    public bool HasReturnValue
    {
        get =>
            hasReturnValue;
        private set =>
            SetProperty(
                ref hasReturnValue,
                value);
    }

    public bool IsExecuting =>
        ExecutionState
        == DesktopRuntimeCommandExecutionState.Executing;

    public bool CanExecute =>
        IsEndpointReady
        && !IsExecuting
        && HasValidArgument;

    public void Update(
        DesktopRuntimeCommandSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!HasSameDescriptor(
                snapshot))
        {
            throw new ArgumentException(
                "A Command view model cannot be updated from a different "
                + "Command descriptor.",
                nameof(snapshot));
        }

        Target =
            snapshot.Target;
        IsEndpointReady =
            snapshot.IsEndpointReady;
        OnPropertyChanged(
            nameof(CanExecute));
    }

    public RuntimeHostCommandTarget? TryBeginExecution()
    {
        if (!CanExecute)
        {
            return null;
        }

        RuntimeHostCommandTarget capturedTarget =
            Target;

        ReturnValue =
            string.Empty;
        HasReturnValue =
            false;
        ExecutionMessage =
            "Executing Command...";
        SetExecutionState(
            DesktopRuntimeCommandExecutionState.Executing);

        return capturedTarget;
    }

    public void CompleteExecution(
        RuntimeHostCommandOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        if (result.IsSuccess)
        {
            if (result.ReturnValue is not null)
            {
                ReturnValue =
                    FormatReturnValue(
                        result.ReturnValue);
                HasReturnValue =
                    true;
            }

            ExecutionMessage =
                "Command succeeded; awaiting authoritative inventory refresh.";
            SetExecutionState(
                DesktopRuntimeCommandExecutionState.Succeeded);
            return;
        }

        ExecutionMessage =
            result.Diagnostic
            ?? $"Command failed: {result.Status}.";
        SetExecutionState(
            IsRejected(
                result.Status)
                ? DesktopRuntimeCommandExecutionState.Rejected
                : DesktopRuntimeCommandExecutionState.Failed);
    }

    public void CancelExecution()
    {
        ExecutionMessage =
            "Command cancelled.";
        SetExecutionState(
            DesktopRuntimeCommandExecutionState.Cancelled);
    }

    public void CompletePropertyReconciliation(
        int refreshedPropertyCount)
    {
        if (refreshedPropertyCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshedPropertyCount));
        }

        ExecutionMessage =
            refreshedPropertyCount == 0
                ? "Command succeeded; no readable Properties required "
                    + "refresh."
                : $"Command succeeded; authoritatively refreshed "
                    + $"{refreshedPropertyCount} "
                    + $"{(refreshedPropertyCount == 1 ? "Property" : "Properties")}.";
    }

    public void ReportPropertyReconciliationWarning(
        string warning)
    {
        if (string.IsNullOrWhiteSpace(
                warning))
        {
            throw new ArgumentException(
                "The Property reconciliation warning must not be empty.",
                nameof(warning));
        }

        ExecutionMessage =
            "Command succeeded; Property reconciliation warning: "
            + warning.Trim();
    }

    public void FailExecution(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        ExecutionMessage =
            string.IsNullOrWhiteSpace(exception.Message)
                ? "Command failed."
                : exception.Message;
        SetExecutionState(
            DesktopRuntimeCommandExecutionState.Failed);
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
                StringComparison.Ordinal)
            && Equals(
                Descriptor,
                snapshot.Descriptor);
    }

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

    private void SetExecutionState(
        DesktopRuntimeCommandExecutionState state)
    {
        if (ExecutionState == state)
        {
            return;
        }

        ExecutionState =
            state;
        OnPropertyChanged(
            nameof(IsExecuting));
        OnPropertyChanged(
            nameof(CanExecute));
    }

    private static bool IsRejected(
        RuntimeHostCommandOperationStatus status) =>
        status
        is RuntimeHostCommandOperationStatus.AttachmentNotCurrent
            or RuntimeHostCommandOperationStatus.InstrumentNotFound
            or RuntimeHostCommandOperationStatus.CommandNotFound
            or RuntimeHostCommandOperationStatus.ArgumentNotSupported
            or RuntimeHostCommandOperationStatus.EndpointRejected;

    private static string FormatReturnValue(
        object value) =>
        value is IFormattable formattable
            ? formattable.ToString(
                format: null,
                CultureInfo.InvariantCulture)
                ?? string.Empty
            : value.ToString()
                ?? string.Empty;

    private bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(
                field,
                value))
        {
            return false;
        }

        field =
            value;
        OnPropertyChanged(
            propertyName);
        return true;
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

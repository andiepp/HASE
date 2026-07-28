using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimeCommandViewModel
    : INotifyPropertyChanged
{
    private RuntimeHostCommandTarget target;
    private bool isEndpointReady;
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
        && !IsExecuting;

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
                StringComparison.Ordinal);
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

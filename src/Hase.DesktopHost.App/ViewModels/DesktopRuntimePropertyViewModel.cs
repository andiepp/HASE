using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Hase.Runtime.Northbound;
using Prism.Commands;

namespace Hase.DesktopHost.App.ViewModels;

public sealed class DesktopRuntimePropertyViewModel
    : INotifyPropertyChanged
{
    private static readonly TimeSpan ChangeHighlightDuration =
        TimeSpan.FromMilliseconds(1500);

    private readonly DispatcherTimer highlightTimer;

    private string displayName;
    private string path;
    private string access;
    private string value;
    private string quality;
    private string timestampUtc;
    private bool isKnown;
    private bool isRecentlyChanged;
    private DesktopRuntimePropertyDataKind dataKind;
    private bool canRead;
    private bool canWrite;
    private bool? currentBooleanValue;
    private bool? requestedBooleanValue;
    private RuntimeHostPropertyTarget target;
    private bool isEndpointReady;
    private DesktopRuntimePropertyWriteState writeState =
        DesktopRuntimePropertyWriteState.Ready;
    private string writeMessage =
        string.Empty;

    public DesktopRuntimePropertyViewModel(
        DesktopRuntimePropertySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        PropertyId =
            string.IsNullOrWhiteSpace(snapshot.PropertyId)
                ? throw new ArgumentException(
                    "The Property identity must not be empty.",
                    nameof(snapshot))
                : snapshot.PropertyId;
        target =
            snapshot.Target
            ?? throw new ArgumentException(
                "The Property target must not be null.",
                nameof(snapshot));
        displayName =
            snapshot.DisplayName;
        path =
            snapshot.Path;
        access =
            snapshot.Access;
        value =
            snapshot.Value;
        quality =
            snapshot.Quality;
        timestampUtc =
            snapshot.TimestampUtc;
        isKnown =
            snapshot.IsKnown;
        dataKind =
            snapshot.DataKind;
        canRead =
            snapshot.CanRead;
        canWrite =
            snapshot.CanWrite;
        currentBooleanValue =
            snapshot.BooleanValue;
        requestedBooleanValue =
            IsWritableBoolean(
                snapshot.DataKind,
                snapshot.CanWrite)
                ? snapshot.BooleanValue
                : null;
        isEndpointReady =
            snapshot.IsEndpointReady;

        highlightTimer =
            new DispatcherTimer(
                DispatcherPriority.Background)
            {
                Interval =
                    ChangeHighlightDuration
            };
        highlightTimer.Tick +=
            OnHighlightTimerTick;

        ResetRequestedValueCommand =
            new DelegateCommand(
                ResetRequestedValue,
                CanResetRequestedValue);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PropertyId
    {
        get;
    }

    public RuntimeHostPropertyTarget Target
    {
        get =>
            target;
        private set =>
            SetProperty(
                ref target,
                value);
    }

    public string DisplayName
    {
        get =>
            displayName;
        private set =>
            SetProperty(
                ref displayName,
                value);
    }

    public string Path
    {
        get =>
            path;
        private set =>
            SetProperty(
                ref path,
                value);
    }

    public string Access
    {
        get =>
            access;
        private set =>
            SetProperty(
                ref access,
                value);
    }

    public string Value
    {
        get =>
            value;
        private set =>
            SetProperty(
                ref this.value,
                value);
    }

    public string Quality
    {
        get =>
            quality;
        private set =>
            SetProperty(
                ref quality,
                value);
    }

    public string TimestampUtc
    {
        get =>
            timestampUtc;
        private set =>
            SetProperty(
                ref timestampUtc,
                value);
    }

    public bool IsKnown
    {
        get =>
            isKnown;
        private set =>
            SetProperty(
                ref isKnown,
                value);
    }

    public bool IsRecentlyChanged
    {
        get =>
            isRecentlyChanged;
        private set =>
            SetProperty(
                ref isRecentlyChanged,
                value);
    }

    public DesktopRuntimePropertyDataKind DataKind
    {
        get =>
            dataKind;
        private set =>
            SetProperty(
                ref dataKind,
                value);
    }

    public bool CanWrite
    {
        get =>
            canWrite;
        private set =>
            SetProperty(
                ref canWrite,
                value);
    }

    public bool CanRead
    {
        get =>
            canRead;
        private set =>
            SetProperty(
                ref canRead,
                value);
    }

    public bool HasBooleanEditor =>
        IsWritableBoolean(
            DataKind,
            CanWrite);

    public bool? CurrentBooleanValue
    {
        get =>
            currentBooleanValue;
        private set =>
            SetProperty(
                ref currentBooleanValue,
                value);
    }

    public bool? RequestedBooleanValue
    {
        get =>
            requestedBooleanValue;
        set =>
            SetRequestedBooleanValue(
                value);
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

    public DesktopRuntimePropertyWriteState WriteState
    {
        get =>
            writeState;
        private set =>
            SetProperty(
                ref writeState,
                value);
    }

    public string WriteMessage
    {
        get =>
            writeMessage;
        private set =>
            SetProperty(
                ref writeMessage,
                value);
    }

    public bool IsWriteExecuting =>
        WriteState
        == DesktopRuntimePropertyWriteState.Executing;

    public bool CanWriteRequestedValue =>
        HasBooleanEditor
        && RequestedBooleanValue.HasValue
        && IsEndpointReady
        && !IsWriteExecuting;

    public DelegateCommand ResetRequestedValueCommand
    {
        get;
    }

    public void Update(
        DesktopRuntimePropertySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        if (!string.Equals(
                PropertyId,
                snapshot.PropertyId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A Property view model cannot be updated from a different "
                + "Property identity.",
                nameof(snapshot));
        }

        bool valueStateChanged =
            !string.Equals(
                Value,
                snapshot.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                Quality,
                snapshot.Quality,
                StringComparison.Ordinal)
            || IsKnown != snapshot.IsKnown;
        bool requestedValueMetadataChanged =
            DataKind != snapshot.DataKind
            || CanWrite != snapshot.CanWrite;

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
        Target =
            snapshot.Target;
        DataKind =
            snapshot.DataKind;
        CanRead =
            snapshot.CanRead;
        CanWrite =
            snapshot.CanWrite;
        CurrentBooleanValue =
            snapshot.BooleanValue;
        IsEndpointReady =
            snapshot.IsEndpointReady;

        if (requestedValueMetadataChanged)
        {
            RequestedBooleanValue =
                IsWritableBoolean(
                    snapshot.DataKind,
                    snapshot.CanWrite)
                    ? snapshot.BooleanValue
                    : null;
            OnPropertyChanged(
                nameof(HasBooleanEditor));
        }

        ResetRequestedValueCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(
            nameof(CanWriteRequestedValue));

        if (valueStateChanged)
        {
            RestartChangeHighlight();
        }
    }

    public DesktopRuntimeBooleanPropertyWriteRequest? TryBeginBooleanWrite()
    {
        if (!CanWriteRequestedValue)
        {
            return null;
        }

        RuntimeHostPropertyTarget capturedTarget =
            Target;
        bool capturedValue =
            RequestedBooleanValue!.Value;

        WriteMessage =
            "Writing requested value...";
        SetWriteState(
            DesktopRuntimePropertyWriteState.Executing);

        return new DesktopRuntimeBooleanPropertyWriteRequest(
            capturedTarget,
            capturedValue);
    }

    public void CompleteWrite(
        RuntimeHostPropertyOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        if (result.IsSuccess)
        {
            WriteMessage =
                "Write succeeded; awaiting authoritative inventory refresh.";
            SetWriteState(
                DesktopRuntimePropertyWriteState.Succeeded);
            return;
        }

        WriteMessage =
            result.Diagnostic
            ?? GetDefaultFailureMessage(
                result.Status);
        SetWriteState(
            IsRejected(
                result.Status)
                ? DesktopRuntimePropertyWriteState.Rejected
                : DesktopRuntimePropertyWriteState.Failed);
    }

    public void CancelWrite()
    {
        WriteMessage =
            "Write cancelled.";
        SetWriteState(
            DesktopRuntimePropertyWriteState.Cancelled);
    }

    public void FailWrite(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        WriteMessage =
            string.IsNullOrWhiteSpace(exception.Message)
                ? "Write failed."
                : exception.Message;
        SetWriteState(
            DesktopRuntimePropertyWriteState.Failed);
    }

    private void ResetRequestedValue()
    {
        RequestedBooleanValue =
            CurrentBooleanValue;
    }

    private void SetRequestedBooleanValue(
        bool? value)
    {
        if (SetProperty(
                ref requestedBooleanValue,
                value,
                nameof(RequestedBooleanValue)))
        {
            OnPropertyChanged(
                nameof(CanWriteRequestedValue));
        }
    }

    private void SetWriteState(
        DesktopRuntimePropertyWriteState state)
    {
        if (WriteState == state)
        {
            return;
        }

        WriteState =
            state;
        OnPropertyChanged(
            nameof(IsWriteExecuting));
        OnPropertyChanged(
            nameof(CanWriteRequestedValue));
    }

    private static bool IsRejected(
        RuntimeHostPropertyOperationStatus status) =>
        status
        is RuntimeHostPropertyOperationStatus.AttachmentNotCurrent
            or RuntimeHostPropertyOperationStatus.InstrumentNotFound
            or RuntimeHostPropertyOperationStatus.PropertyNotFound
            or RuntimeHostPropertyOperationStatus.WriteNotSupported
            or RuntimeHostPropertyOperationStatus.InvalidValue
            or RuntimeHostPropertyOperationStatus.EndpointRejected;

    private static string GetDefaultFailureMessage(
        RuntimeHostPropertyOperationStatus status) =>
        $"Write failed: {status}.";

    private bool CanResetRequestedValue() =>
        HasBooleanEditor
        && CurrentBooleanValue.HasValue;

    private static bool IsWritableBoolean(
        DesktopRuntimePropertyDataKind dataKind,
        bool canWrite) =>
        dataKind == DesktopRuntimePropertyDataKind.Boolean
        && canWrite;

    private void RestartChangeHighlight()
    {
        highlightTimer.Stop();
        IsRecentlyChanged =
            true;
        highlightTimer.Start();
    }

    private void OnHighlightTimerTick(
        object? sender,
        EventArgs eventArgs)
    {
        highlightTimer.Stop();
        IsRecentlyChanged =
            false;
    }

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

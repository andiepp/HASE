using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Operator.Input;
using Hase.Runtime.Northbound;
using Prism.Commands;
using CorePropertyDescriptor =
    Hase.Core.Domain.Properties.PropertyDescriptor;

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
    private object? currentTypedValue;
    private string requestedValueText = string.Empty;
    private CorePropertyDescriptor descriptor;
    private DesktopRuntimePropertyEditorKind editorKind;
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
        currentTypedValue =
            snapshot.CurrentValue
            ?? snapshot.BooleanValue;
        descriptor =
            snapshot.Descriptor
            ?? CreateCompatibilityDescriptor(
                snapshot);
        editorKind =
            GetEditorKind(
                descriptor);
        requestedBooleanValue =
            IsWritableBoolean(
                snapshot.DataKind,
                snapshot.CanWrite)
                ? snapshot.BooleanValue
                : null;
        requestedValueText =
            editorKind == DesktopRuntimePropertyEditorKind.Text
                ? FormatEditableValue(
                    currentTypedValue)
                : string.Empty;
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
        EditorKind == DesktopRuntimePropertyEditorKind.Boolean;

    public bool HasTextEditor =>
        EditorKind == DesktopRuntimePropertyEditorKind.Text;

    public bool HasEditor =>
        EditorKind != DesktopRuntimePropertyEditorKind.None;

    public DesktopRuntimePropertyEditorKind EditorKind
    {
        get =>
            editorKind;
        private set =>
            SetProperty(
                ref editorKind,
                value);
    }

    public CorePropertyDescriptor Descriptor
    {
        get =>
            descriptor;
        private set =>
            SetProperty(
                ref descriptor,
                value);
    }

    public object? CurrentTypedValue
    {
        get =>
            currentTypedValue;
        private set =>
            SetProperty(
                ref currentTypedValue,
                value);
    }

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

    public string RequestedValueText
    {
        get =>
            requestedValueText;
        set
        {
            ArgumentNullException.ThrowIfNull(
                value);

            if (SetProperty(
                    ref requestedValueText,
                    value))
            {
                RaiseInputStateChanged();
            }
        }
    }

    public PropertyInputParseResult InputResult =>
        PropertyInputParser.Parse(
            Descriptor,
            HasBooleanEditor
                ? RequestedBooleanValue?.ToString()
                : RequestedValueText);

    public bool HasValidRequestedValue =>
        HasEditor
        && InputResult.IsSuccess;

    public string ValidationMessage =>
        HasEditor
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
        HasValidRequestedValue
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
            || CanWrite != snapshot.CanWrite
            || snapshot.Descriptor is not null
                && Descriptor != snapshot.Descriptor;

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
        CurrentTypedValue =
            snapshot.CurrentValue
            ?? snapshot.BooleanValue;
        Descriptor =
            snapshot.Descriptor
            ?? CreateCompatibilityDescriptor(
                snapshot);
        EditorKind =
            GetEditorKind(
                Descriptor);
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
            RequestedValueText =
                HasTextEditor
                    ? FormatEditableValue(
                        CurrentTypedValue)
                    : string.Empty;
            OnPropertyChanged(
                nameof(HasBooleanEditor));
            OnPropertyChanged(
                nameof(HasTextEditor));
            OnPropertyChanged(
                nameof(HasEditor));
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

    public DesktopRuntimePropertyWriteRequest? TryBeginWrite()
    {
        if (!CanWriteRequestedValue)
        {
            return null;
        }

        PropertyInputParseResult result =
            InputResult;
        if (!result.IsSuccess)
        {
            return null;
        }

        WriteMessage =
            "Writing requested value...";
        SetWriteState(
            DesktopRuntimePropertyWriteState.Executing);

        return new DesktopRuntimePropertyWriteRequest(
            Target,
            result.Value!,
            FormatInputSummary(
                result.Value!));
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
        if (HasBooleanEditor)
        {
            RequestedBooleanValue =
                CurrentBooleanValue;
        }
        else if (HasTextEditor)
        {
            RequestedValueText =
                FormatEditableValue(
                    CurrentTypedValue);
        }
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
            RaiseInputStateChanged();
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
            ? CurrentBooleanValue.HasValue
            : HasTextEditor
                && CurrentTypedValue is not null;

    private static bool IsWritableBoolean(
        DesktopRuntimePropertyDataKind dataKind,
        bool canWrite) =>
        dataKind == DesktopRuntimePropertyDataKind.Boolean
        && canWrite;

    private void RaiseInputStateChanged()
    {
        OnPropertyChanged(
            nameof(InputResult));
        OnPropertyChanged(
            nameof(HasValidRequestedValue));
        OnPropertyChanged(
            nameof(ValidationMessage));
        OnPropertyChanged(
            nameof(CanWriteRequestedValue));
        ResetRequestedValueCommand.RaiseCanExecuteChanged();
    }

    private static DesktopRuntimePropertyEditorKind GetEditorKind(
        CorePropertyDescriptor value)
    {
        if (!value.AccessMode.HasFlag(
                PropertyAccessMode.Write))
        {
            return DesktopRuntimePropertyEditorKind.None;
        }

        return value.Data switch
        {
            BooleanDataDescriptor =>
                DesktopRuntimePropertyEditorKind.Boolean,
            NumericDataDescriptor
                or StringDataDescriptor
                or ByteArrayDataDescriptor =>
                    DesktopRuntimePropertyEditorKind.Text,
            _ =>
                DesktopRuntimePropertyEditorKind.None
        };
    }

    private static string FormatEditableValue(
        object? value)
    {
        return value switch
        {
            null =>
                string.Empty,
            double numeric =>
                numeric.ToString(
                    "G17",
                    CultureInfo.InvariantCulture),
            string text =>
                text,
            ByteArrayValue bytes =>
                string.Join(
                    " ",
                    bytes.ToArray()
                        .Select(
                            item =>
                                item.ToString(
                                    "X2",
                                    CultureInfo.InvariantCulture))),
            bool boolean =>
                boolean.ToString(),
            _ =>
                Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture)
                ?? string.Empty
        };
    }

    private static string FormatInputSummary(
        object value)
    {
        return FormatEditableValue(
            value);
    }

    private static CorePropertyDescriptor CreateCompatibilityDescriptor(
        DesktopRuntimePropertySnapshot snapshot)
    {
        DataDescriptor data =
            snapshot.DataKind switch
            {
                DesktopRuntimePropertyDataKind.Boolean =>
                    new BooleanDataDescriptor(),
                DesktopRuntimePropertyDataKind.Numeric =>
                    new NumericDataDescriptor(
                        Quantities.Temperature,
                        Units.Celsius),
                DesktopRuntimePropertyDataKind.String =>
                    new StringDataDescriptor(),
                DesktopRuntimePropertyDataKind.ByteArray =>
                    new ByteArrayDataDescriptor(),
                _ =>
                    new StringDataDescriptor()
            };

        return new CorePropertyDescriptor(
            new PropertyId(
                snapshot.PropertyId),
            DescriptorPath.Parse(
                snapshot.Path),
            snapshot.DisplayName,
            data)
        {
            AccessMode =
                snapshot.CanWrite
                    ? PropertyAccessMode.ReadWrite
                    : PropertyAccessMode.Read
        };
    }

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

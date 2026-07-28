using System.IO;
using Hase.Client.Wpf.Services;
using Prism.Commands;
using Prism.Mvvm;

namespace Hase.Client.Wpf.ViewModels;

/// <summary>
/// Projects normalized runtime-host client-session status for the application
/// shell without owning transport or physical endpoint lifecycles.
/// </summary>
public sealed class MainWindowViewModel
    : BindableBase
{
    private const int MaximumEventOccurrences =
        100;
    private RuntimeHostClientSessionStatus sessionStatus =
        new(
            RuntimeHostClientSessionState.Disconnected);
    private RemoteObservationState currentState =
        RemoteObservationState.Empty;
    private IRuntimeHostClientSessionController? sessionController;
    private IClientConfigurationFilePicker? configurationFilePicker;
    private bool isBusy;
    private string? failureMessage;
    private RuntimeHostClientFailureCategory? lastFailureCategory;
    private IReadOnlyList<EndpointInventoryItemViewModel> endpoints =
        [];
    private readonly Dictionary<
        RemotePropertyTarget,
        RemotePropertyValue> confirmedReads =
        [];
    private readonly Dictionary<
        RemotePropertyTarget,
        bool> requestedBooleanValues =
        [];
    private readonly Dictionary<
        RemoteCommandTarget,
        string> requestedCommandArgumentTexts =
        [];
    private string? propertyReadMessage;
    private IReadOnlyList<EventOccurrenceItemViewModel> eventOccurrences =
        [];

    public MainWindowViewModel()
    {
        ConnectCommand =
            new DelegateCommand(
                ExecuteConnect,
                () =>
                    sessionController is not null
                    && !IsBusy
                    && CanConnect);
        DisconnectCommand =
            new DelegateCommand(
                ExecuteDisconnect,
                () =>
                    sessionController is not null
                    && !IsBusy
                    && CanDisconnect);
        ReadPropertyCommand =
            new DelegateCommand<PropertyInventoryItemViewModel>(
                ExecuteReadProperty,
                property =>
                    property is not null
                    && sessionController is not null
                    && IsOperational
                    && !IsBusy
                    && property.CanRead);
        WriteBooleanPropertyCommand =
            new DelegateCommand<PropertyInventoryItemViewModel>(
                ExecuteWriteBooleanProperty,
                property =>
                    property is not null
                    && sessionController is not null
                    && IsOperational
                    && !IsBusy
                    && property.CanWrite);
        ExecuteCommand =
            new DelegateCommand<CommandInventoryItemViewModel>(
                ExecuteParameterlessCommand,
                command =>
                    command is not null
                    && sessionController is not null
                    && IsOperational
                    && !IsBusy
                    && command.EndpointReady);
    }

    public string Title =>
        "HASE Laptop Client";

    public RuntimeHostClientSessionStatus SessionStatus =>
        sessionStatus;

    public string SessionState =>
        sessionStatus.State.ToString();

    public string RuntimeHostId =>
        sessionStatus.RuntimeHostId?.Value
        ?? "Not connected";

    public string ApiVersion =>
        sessionStatus.ApiVersion?.ToString()
        ?? "Not available";

    public bool CanConnect =>
        sessionStatus.State is
            RuntimeHostClientSessionState.Disconnected
            or RuntimeHostClientSessionState.Faulted;

    public bool CanDisconnect =>
        sessionStatus.State is
            RuntimeHostClientSessionState.Connecting
            or RuntimeHostClientSessionState.Connected
            or RuntimeHostClientSessionState.Reconnecting;

    public bool IsOperational =>
        sessionStatus.State
            == RuntimeHostClientSessionState.Connected;

    public bool IsStale =>
        sessionStatus.State
            == RuntimeHostClientSessionState.Reconnecting;

    public RemoteObservationState CurrentState =>
        currentState;

    public int EndpointCount =>
        currentState.Snapshot?.Attachments.Count
        ?? 0;

    public IReadOnlyList<EndpointInventoryItemViewModel> Endpoints =>
        endpoints;

    public bool HasEndpoints =>
        endpoints.Count > 0;

    public IReadOnlyList<EventOccurrenceItemViewModel> EventOccurrences =>
        eventOccurrences;

    public bool HasEventOccurrences =>
        eventOccurrences.Count > 0;

    public bool IsBusy
    {
        get =>
            isBusy;
        private set
        {
            if (SetProperty(
                    ref isBusy,
                    value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string? FailureMessage
    {
        get =>
            failureMessage;
        private set =>
            SetProperty(
                ref failureMessage,
                value);
    }

    public string? PropertyReadMessage
    {
        get =>
            propertyReadMessage;
        private set =>
            SetProperty(
                ref propertyReadMessage,
                value);
    }

    public RuntimeHostClientFailureCategory? LastFailureCategory
    {
        get =>
            lastFailureCategory;
        private set =>
            SetProperty(
                ref lastFailureCategory,
                value);
    }

    public DelegateCommand ConnectCommand
    {
        get;
    }

    public DelegateCommand DisconnectCommand
    {
        get;
    }

    public DelegateCommand<PropertyInventoryItemViewModel>
        ReadPropertyCommand
    {
        get;
    }

    public DelegateCommand<PropertyInventoryItemViewModel>
        WriteBooleanPropertyCommand
    {
        get;
    }

    public DelegateCommand<CommandInventoryItemViewModel> ExecuteCommand
    {
        get;
    }

    public void Configure(
        IRuntimeHostClientSessionController controller,
        IClientConfigurationFilePicker filePicker)
    {
        ArgumentNullException.ThrowIfNull(
            controller);
        ArgumentNullException.ThrowIfNull(
            filePicker);

        if (sessionController is not null)
        {
            throw new InvalidOperationException(
                "The main window client services are already configured.");
        }

        sessionController =
            controller;
        configurationFilePicker =
            filePicker;
        RaiseCommandStateChanged();
    }

    public void ApplySessionStatus(
        RuntimeHostClientSessionStatus value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        RuntimeHostClientSessionState previousState =
            sessionStatus.State;
        SetProperty(
            ref sessionStatus,
            value,
            nameof(SessionStatus));
        RaisePropertyChanged(
            nameof(SessionState));
        RaisePropertyChanged(
            nameof(RuntimeHostId));
        RaisePropertyChanged(
            nameof(ApiVersion));
        RaisePropertyChanged(
            nameof(CanConnect));
        RaisePropertyChanged(
            nameof(CanDisconnect));
        RaisePropertyChanged(
            nameof(IsOperational));
        RaisePropertyChanged(
            nameof(IsStale));
        if (value.State
                == RuntimeHostClientSessionState.Connected
            && previousState
                != RuntimeHostClientSessionState.Connected)
        {
            ClearEventOccurrences();
        }
        RaiseCommandStateChanged();
    }

    public void ApplyEventOccurred(
        RemoteRuntimeHostObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        if (observation.Payload
            is not RemoteEventOccurredObservationPayload payload)
        {
            throw new ArgumentException(
                "An Event-occurrence observation is required.",
                nameof(observation));
        }

        RemoteEndpointAttachmentSnapshot endpoint =
            currentState.Snapshot?.Attachments.SingleOrDefault(
                attachment =>
                    attachment.Key
                    == observation.Attachment)
            ?? throw new InvalidDataException(
                "The Event attachment is not present in the current "
                + "runtime-host snapshot.");
        Hase.Core.Domain.Instruments.InstrumentDescriptor instrument =
            endpoint.Descriptor.Instruments.Single(
                candidate =>
                    candidate.Id
                    == payload.InstrumentId);
        Hase.Core.Domain.Events.EventDescriptor descriptor =
            instrument.Interface.Events.Single(
                candidate =>
                    candidate.Path
                    == payload.EventPath);
        var occurrence =
            new EventOccurrenceItemViewModel(
                observation.Sequence.Value,
                observation.Attachment.EndpointId.Value,
                observation.Attachment.Generation.ToString(),
                payload.InstrumentId.Value,
                payload.EventPath.ToString(),
                descriptor.DisplayName,
                payload.OccurredAtUtc.ToString(
                    "O",
                    System.Globalization.CultureInfo.InvariantCulture),
                FormatEventValue(
                    payload.Value));
        SetProperty(
            ref eventOccurrences,
            new[]
            {
                occurrence
            }
                .Concat(
                    eventOccurrences)
                .Take(
                    MaximumEventOccurrences)
                .ToArray(),
            nameof(EventOccurrences));
        RaisePropertyChanged(
            nameof(HasEventOccurrences));
    }

    public void ApplyObservationState(
        RemoteObservationState value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        PreserveRequestedBooleanValues();
        PreserveRequestedCommandArgumentTexts();
        SetProperty(
            ref currentState,
            value,
            nameof(CurrentState));
        confirmedReads.Clear();
        SetProperty(
            ref endpoints,
            RuntimeHostInventoryProjector.Project(
                value,
                confirmedReads,
                requestedBooleanValues,
                requestedCommandArgumentTexts),
            nameof(Endpoints));
        RaisePropertyChanged(
            nameof(EndpointCount));
        RaisePropertyChanged(
            nameof(HasEndpoints));
    }

    public async Task ConnectAsync()
    {
        if (sessionController is null
            || configurationFilePicker is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        string? configurationFilePath =
            configurationFilePicker.PickConfigurationFile();

        if (configurationFilePath is null)
        {
            return;
        }

        IsBusy =
            true;
        FailureMessage =
            null;
        LastFailureCategory =
            null;

        try
        {
            await sessionController.ConnectAsync(
                    configurationFilePath)
                .ConfigureAwait(
                    true);
        }
        catch
        {
            FailureMessage =
                "The runtime-host connection could not be started.";
        }
        finally
        {
            IsBusy =
                false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (sessionController is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        IsBusy =
            true;
        FailureMessage =
            null;

        try
        {
            await sessionController.DisconnectAsync()
                .ConfigureAwait(
                    true);
        }
        catch
        {
            FailureMessage =
                "The runtime-host connection could not be stopped cleanly.";
        }
        finally
        {
            IsBusy =
                false;
        }
    }

    public async Task ReadPropertyAsync(
        PropertyInventoryItemViewModel property)
    {
        ArgumentNullException.ThrowIfNull(
            property);

        if (sessionController is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        if (!IsOperational
            || !property.CanRead)
        {
            return;
        }

        IsBusy =
            true;
        PropertyReadMessage =
            $"Reading {property.DisplayName}...";

        try
        {
            RemotePropertyOperationResult result =
                await sessionController.ReadPropertyAsync(
                        property.Target)
                    .ConfigureAwait(
                        true);

            if (result.IsSuccess)
            {
                RemotePropertyValue confirmedValue =
                    result.ConfirmedValue
                    ?? throw new InvalidDataException(
                        "A successful Property read has no confirmed value.");
                confirmedReads[property.Target] =
                    confirmedValue;
                SetProperty(
                    ref endpoints,
                    RuntimeHostInventoryProjector.Project(
                        currentState,
                        confirmedReads,
                        requestedBooleanValues,
                        requestedCommandArgumentTexts),
                    nameof(Endpoints));
                PropertyReadMessage =
                    $"{property.DisplayName}: endpoint-confirmed value "
                    + "received.";
            }
            else
            {
                PropertyReadMessage =
                    $"{property.DisplayName}: read failed "
                    + $"({result.Status}).";
            }
        }
        catch (RuntimeHostClientException exception)
        {
            PropertyReadMessage =
                $"{property.DisplayName}: read failed "
                + $"({exception.Category}).";
        }
        catch
        {
            PropertyReadMessage =
                $"{property.DisplayName}: read failed.";
        }
        finally
        {
            IsBusy =
                false;
        }
    }

    public async Task WriteBooleanPropertyAsync(
        PropertyInventoryItemViewModel property)
    {
        ArgumentNullException.ThrowIfNull(
            property);

        if (sessionController is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        if (!IsOperational
            || !property.CanWrite
            || !property.SupportsBooleanWrite)
        {
            return;
        }

        IsBusy =
            true;
        requestedBooleanValues[property.Target] =
            property.RequestedBooleanValue;
        PropertyReadMessage =
            $"Writing {property.DisplayName}...";

        try
        {
            RemotePropertyOperationResult result =
                await sessionController.WritePropertyAsync(
                        property.Target,
                        RemoteValue.FromBoolean(
                            property.RequestedBooleanValue))
                    .ConfigureAwait(
                        true);

            if (result.IsSuccess)
            {
                RemotePropertyValue confirmedValue =
                    result.ConfirmedValue
                    ?? throw new InvalidDataException(
                        "A successful Property write has no confirmed value.");
                confirmedReads[property.Target] =
                    confirmedValue;
                SetProperty(
                    ref endpoints,
                    RuntimeHostInventoryProjector.Project(
                        currentState,
                        confirmedReads,
                        requestedBooleanValues,
                        requestedCommandArgumentTexts),
                    nameof(Endpoints));
                PropertyReadMessage =
                    $"{property.DisplayName}: endpoint-confirmed write "
                    + "completed.";
            }
            else
            {
                PropertyReadMessage =
                    $"{property.DisplayName}: write failed "
                    + $"({result.Status}).";
            }
        }
        catch (RuntimeHostClientException exception)
        {
            PropertyReadMessage =
                $"{property.DisplayName}: write failed "
                + $"({exception.Category}).";
        }
        catch
        {
            PropertyReadMessage =
                $"{property.DisplayName}: write failed.";
        }
        finally
        {
            IsBusy =
                false;
        }
    }

    public async Task ExecuteCommandAsync(
        CommandInventoryItemViewModel command)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        if (sessionController is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        if (!IsOperational
            || !command.EndpointReady)
        {
            return;
        }

        RemoteValue? argument =
            null;

        if (command.RequiresArgument)
        {
            if (command.ArgumentDataType != "ByteArray"
                || !ByteArrayHexadecimalParser.TryParse(
                    command.RequestedArgumentText,
                    out Hase.Core.Domain.Data.ByteArrayValue?
                        byteArrayArgument))
            {
                PropertyReadMessage =
                    $"{command.DisplayName}: enter valid hexadecimal bytes.";
                return;
            }

            argument =
                RemoteValue.FromByteArray(
                    byteArrayArgument);
        }

        IsBusy =
            true;
        PropertyReadMessage =
            $"Executing {command.DisplayName}...";

        try
        {
            RemoteCommandOperationResult result =
                await sessionController.ExecuteCommandAsync(
                    new RemoteCommandExecutionRequest(
                            command.Target,
                            argument))
                    .ConfigureAwait(
                        true);

            PropertyReadMessage =
                result.IsSuccess
                    ? $"{command.DisplayName}: Command completed."
                    : $"{command.DisplayName}: Command failed "
                        + $"({result.Status}).";
        }
        catch (RuntimeHostClientException exception)
        {
            PropertyReadMessage =
                $"{command.DisplayName}: Command failed "
                + $"({exception.Category}).";
        }
        catch
        {
            PropertyReadMessage =
                $"{command.DisplayName}: Command failed.";
        }
        finally
        {
            IsBusy =
                false;
        }
    }

    private async void ExecuteConnect()
    {
        await ConnectAsync();
    }

    private void PreserveRequestedBooleanValues()
    {
        requestedBooleanValues.Clear();

        foreach (PropertyInventoryItemViewModel property
            in endpoints
                .SelectMany(
                    endpoint =>
                        endpoint.Instruments)
                .SelectMany(
                    instrument =>
                        instrument.Properties)
                .Where(
                    property =>
                        property.SupportsBooleanWrite))
        {
            requestedBooleanValues[property.Target] =
                property.RequestedBooleanValue;
        }
    }

    private void PreserveRequestedCommandArgumentTexts()
    {
        requestedCommandArgumentTexts.Clear();

        foreach (CommandInventoryItemViewModel command
            in endpoints
                .SelectMany(
                    endpoint =>
                        endpoint.Instruments)
                .SelectMany(
                    instrument =>
                        instrument.Commands)
                .Where(
                    command =>
                        command.RequiresArgument))
        {
            requestedCommandArgumentTexts[command.Target] =
                command.RequestedArgumentText;
        }
    }

    private void ClearEventOccurrences()
    {
        SetProperty(
            ref eventOccurrences,
            [],
            nameof(EventOccurrences));
        RaisePropertyChanged(
            nameof(HasEventOccurrences));
    }

    private static string FormatEventValue(
        RemoteValue? value) =>
        value?.Kind switch
        {
            RemoteValueKind.Boolean =>
                value.BooleanValue!.Value
                    ? "True"
                    : "False",
            RemoteValueKind.String =>
                value.StringValue!,
            RemoteValueKind.Numeric =>
                value.NumericValue!.Value.ToString(
                    "G17",
                    System.Globalization.CultureInfo.InvariantCulture),
            _ =>
                "No value"
        };

    public void ApplySessionFailure(
        RuntimeHostClientFailureCategory category)
    {
        if (!Enum.IsDefined(
                category)
            || category == RuntimeHostClientFailureCategory.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(category));
        }

        LastFailureCategory =
            category;
        FailureMessage =
            category switch
            {
                RuntimeHostClientFailureCategory.Authentication =>
                    "Runtime-host authentication failed.",
                RuntimeHostClientFailureCategory.Authorization =>
                    "Runtime-host access was denied.",
                RuntimeHostClientFailureCategory.ApiCompatibility =>
                    "The runtime-host API version is not supported.",
                RuntimeHostClientFailureCategory.LocalConfiguration =>
                    "The client configuration is invalid.",
                _ =>
                    "The runtime-host session ended unexpectedly."
            };
    }

    private async void ExecuteDisconnect()
    {
        await DisconnectAsync();
    }

    private async void ExecuteReadProperty(
        PropertyInventoryItemViewModel? property)
    {
        if (property is not null)
        {
            await ReadPropertyAsync(
                property);
        }
    }

    private async void ExecuteWriteBooleanProperty(
        PropertyInventoryItemViewModel? property)
    {
        if (property is not null)
        {
            await WriteBooleanPropertyAsync(
                property);
        }
    }

    private async void ExecuteParameterlessCommand(
        CommandInventoryItemViewModel? command)
    {
        if (command is not null)
        {
            await ExecuteCommandAsync(
                command);
        }
    }

    private void RaiseCommandStateChanged()
    {
        ConnectCommand.RaiseCanExecuteChanged();
        DisconnectCommand.RaiseCanExecuteChanged();
        ReadPropertyCommand.RaiseCanExecuteChanged();
        WriteBooleanPropertyCommand.RaiseCanExecuteChanged();
        ExecuteCommand.RaiseCanExecuteChanged();
    }
}

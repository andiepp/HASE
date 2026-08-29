using System.IO;
using Hase.Client.Wpf.Services;
using Hase.Client.Configuration;
using Hase.Operator.Input;
using Hase.Operator.Presentation;
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
    private IClientDiagnosticsWindowController? diagnosticsWindowController;
    private IClientMediaWindowController? mediaWindowController;
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
        RemotePropertyTarget,
        string> requestedPropertyValueTexts =
        [];
    private readonly Dictionary<
        RemoteCommandTarget,
        string> requestedCommandArgumentTexts =
        [];
    private readonly Dictionary<RemoteRuntimeHostPropertyTarget, long>
        initialModeReadAttempts =
        [];
    private readonly Dictionary<
        RemoteRuntimeHostPropertyTarget,
        RemotePropertyValue> initialModeReadValues =
        [];
    private long nextInitialModeReadAttemptId;
    private string? propertyReadMessage;
    private IReadOnlyList<EventOccurrenceItemViewModel> eventOccurrences =
        [];
    private readonly RuntimeHostProfileListProjector runtimeHostProjector = new();
    private RuntimeHostProfileRegistry? runtimeHostRegistry;
    private MultiHostClientSessionSnapshot? multiHostSnapshot;
    private RuntimeHostProfileId? selectedRuntimeHostProfileId;
    private IReadOnlyList<RuntimeHostProfileItemViewModel> runtimeHosts = [];
    private IMultiHostClientSessionCoordinator? multiHostCoordinator;
    private RemoteEndpointAttachmentKey? selectedEndpointKey;

    public MainWindowViewModel()
    {
        Media = new RuntimeHostMediaViewModel();
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
                    && IsOperationHostConnected
                    && !IsBusy
                    && property.CanRead);
        ReadPropertyGroupCommand =
            new DelegateCommand<PropertyGroupItemViewModel>(
                ExecuteReadPropertyGroup,
                group =>
                    group is not null
                    && sessionController is not null
                    && IsOperationHostConnected
                    && !IsBusy
                    && group.CanRead);
        WritePropertyCommand =
            new DelegateCommand<PropertyInventoryItemViewModel>(
                ExecuteWriteProperty,
                property =>
                    property is not null
                    && sessionController is not null
                    && IsOperationHostConnected
                    && !IsBusy
                    && property.CanSubmitWrite);
        ExecuteCommand =
            new DelegateCommand<CommandInventoryItemViewModel>(
                ExecuteParameterlessCommand,
                command =>
                    command is not null
                    && sessionController is not null
                    && IsOperationHostConnected
                    && !IsBusy
                    && command.EndpointReady);
        OpenDiagnosticsCommand =
            new DelegateCommand(
                () => diagnosticsWindowController!.Open(),
                () => diagnosticsWindowController is not null);
        OpenMediaCommand =
            new DelegateCommand(
                () => mediaWindowController!.Open(),
                () => mediaWindowController is not null && Media.HasSources);
        Media.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(RuntimeHostMediaViewModel.HasSources))
            {
                OpenMediaCommand.RaiseCanExecuteChanged();
            }
        };
        ConnectSelectedRuntimeHostCommand = new DelegateCommand(
            ExecuteConnectSelectedRuntimeHost,
            () => multiHostCoordinator is not null && !IsBusy
                && SelectedRuntimeHost is { IsEnabled: true, SessionState: RuntimeHostClientSessionState.Disconnected or RuntimeHostClientSessionState.Faulted });
        DisconnectSelectedRuntimeHostCommand = new DelegateCommand(
            ExecuteDisconnectSelectedRuntimeHost,
            () => multiHostCoordinator is not null && !IsBusy
                && SelectedRuntimeHost is { SessionState: RuntimeHostClientSessionState.Connecting or RuntimeHostClientSessionState.Connected or RuntimeHostClientSessionState.Reconnecting });
        ToggleRuntimeHostConnectionCommand =
            new DelegateCommand<RuntimeHostProfileItemViewModel>(
                ExecuteToggleRuntimeHostConnection,
                host => host is not null && host.IsEnabled && !IsBusy);
    }

    public string Title =>
        "HASE Laptop Client";

    public RuntimeHostMediaViewModel Media { get; }

    public RuntimeHostClientSessionStatus SessionStatus =>
        sessionStatus;

    public string SessionState =>
        multiHostSnapshot is null
            ? sessionStatus.State.ToString()
            : SelectedRuntimeHostSession?.Status.State.ToString()
                ?? "No host selected";

    public string RuntimeHostId =>
        (multiHostSnapshot is null
            ? sessionStatus.RuntimeHostId
            : SelectedRuntimeHostSession?.Status.RuntimeHostId)?.Value
            ?? "Not connected";

    public string ApiVersion =>
        (multiHostSnapshot is null
            ? sessionStatus.ApiVersion
            : SelectedRuntimeHostSession?.Status.ApiVersion)?.ToString()
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

    private bool IsOperationHostConnected =>
        multiHostCoordinator is null
            ? IsOperational
            : SelectedRuntimeHost?.SessionState == RuntimeHostClientSessionState.Connected;

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

    public EndpointInventoryItemViewModel? SelectedEndpoint
    {
        get => selectedEndpointKey is null
            ? null
            : endpoints.SingleOrDefault(endpoint => endpoint.Key == selectedEndpointKey);
        set
        {
            // Replacing the immutable endpoint projection makes WPF briefly
            // write a null SelectedItem back to the view model. Preserve the
            // logical attachment selection while that attachment still exists.
            if (value is null
                && selectedEndpointKey is not null
                && endpoints.Any(endpoint => endpoint.Key == selectedEndpointKey))
            {
                return;
            }

            RemoteEndpointAttachmentKey? valueKey = value?.Key;
            if (valueKey == selectedEndpointKey)
            {
                return;
            }

            selectedEndpointKey = valueKey;
            ApplyEndpointSelectionFlags();
            RaisePropertyChanged();
        }
    }

    public IReadOnlyList<EventOccurrenceItemViewModel> EventOccurrences =>
        eventOccurrences;

    public IReadOnlyList<RuntimeHostProfileItemViewModel> RuntimeHosts => runtimeHosts;

    public RuntimeHostProfileItemViewModel? SelectedRuntimeHost
    {
        get => runtimeHosts.SingleOrDefault(item => item.IsSelected);
        set
        {
            // Replacing the immutable host projection makes WPF briefly write
            // a null SelectedItem back to the view model. Preserve the logical
            // profile selection across that presentation-only reset.
            if (value is null && selectedRuntimeHostProfileId is not null)
            {
                return;
            }

            RuntimeHostProfileId? valueId = value?.ProfileId;
            if (valueId != selectedRuntimeHostProfileId)
                SelectRuntimeHost(valueId);
        }
    }

    private RuntimeHostProfileSessionSnapshot? SelectedRuntimeHostSession =>
        selectedRuntimeHostProfileId is null
            ? null
            : multiHostSnapshot?.Sessions.Single(
                session => session.ProfileId == selectedRuntimeHostProfileId);

    public DelegateCommand ConnectSelectedRuntimeHostCommand { get; }
    public DelegateCommand DisconnectSelectedRuntimeHostCommand { get; }
    public DelegateCommand<RuntimeHostProfileItemViewModel> ToggleRuntimeHostConnectionCommand { get; }

    public void ConfigureMultiHostCoordinator(IMultiHostClientSessionCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        if (multiHostCoordinator is not null)
            throw new InvalidOperationException("The multi-host coordinator is already configured.");
        multiHostCoordinator = coordinator;
        ApplyMultiHostSnapshot(coordinator.Snapshot);
        RaiseCommandStateChanged();
    }

    public void ConfigureRuntimeHosts(RuntimeHostProfileRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (runtimeHostRegistry is not null)
            throw new InvalidOperationException("The runtime-host registry is already configured.");
        runtimeHostRegistry = registry;
    }

    public void ApplyMultiHostSnapshot(MultiHostClientSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        RuntimeHostProfileRegistry registry = runtimeHostRegistry
            ?? throw new InvalidOperationException("The runtime-host registry is not configured.");
        RuntimeHostProfileSessionSnapshot? previousSelectedSession =
            SelectedRuntimeHostSession;
        multiHostSnapshot = snapshot;
        RetainCurrentInitialModeReadAttempts(snapshot);
        if (selectedRuntimeHostProfileId is not null && !registry.TryGet(selectedRuntimeHostProfileId, out _))
            selectedRuntimeHostProfileId = null;
        ApplyRuntimeHostProjection(registry, snapshot);
        ApplySelectedHostState(
            ShouldClearEventOccurrences(
                previousSelectedSession,
                SelectedRuntimeHostSession));
        StartInitialModeReadsIfRequired();
    }

    public void SelectRuntimeHost(RuntimeHostProfileId? profileId)
    {
        RuntimeHostProfileRegistry registry = runtimeHostRegistry
            ?? throw new InvalidOperationException("The runtime-host registry is not configured.");
        if (profileId is not null && !registry.TryGet(profileId, out _))
            throw new ArgumentException("The selected runtime-host profile is not registered.", nameof(profileId));
        selectedRuntimeHostProfileId = profileId;
        selectedEndpointKey = null;
        RaisePropertyChanged(nameof(SelectedEndpoint));
        Media.ResetForRuntimeHostChange();
        if (multiHostSnapshot is not null)
        {
            ApplyRuntimeHostProjection(registry, multiHostSnapshot);
            ApplySelectedHostState();
            StartInitialModeReadsIfRequired();
        }
        RaiseCommandStateChanged();
    }

    private void ApplyRuntimeHostProjection(RuntimeHostProfileRegistry registry, MultiHostClientSessionSnapshot snapshot)
    {
        runtimeHosts = runtimeHostProjector.Project(registry, snapshot, selectedRuntimeHostProfileId);
        RaisePropertyChanged(nameof(RuntimeHosts));
        RaisePropertyChanged(nameof(SelectedRuntimeHost));
        RaiseCommandStateChanged();
    }

    private void ApplySelectedHostState(
        bool clearEventOccurrences = true)
    {
        RuntimeHostProfileSessionSnapshot? selectedSession =
            SelectedRuntimeHostSession;
        Dictionary<RemoteCommandTarget, string>
            retainedShortCircuitConfirmations =
                CaptureConfirmedShortCircuitConfirmations();
        Dictionary<RemotePropertyTarget, bool>
            retainedRequestedBooleanValues =
                CaptureRequestedBooleanValues();
        Dictionary<RemotePropertyTarget, string>
            retainedRequestedPropertyValueTexts =
                CaptureRequestedPropertyValueTexts();
        bool hadActivePropertyValueEditor =
            HasActivePropertyValueEditor();
        RemoteObservationState previousState =
            currentState;
        bool mayPresentState = selectedSession?.Status.State is
            RuntimeHostClientSessionState.Connected or RuntimeHostClientSessionState.Reconnecting;

        confirmedReads.Clear();
        RestoreInitialModeReadValues(
            selectedSession);
        requestedBooleanValues.Clear();
        requestedPropertyValueTexts.Clear();
        requestedCommandArgumentTexts.Clear();
        if (clearEventOccurrences)
        {
            ClearEventOccurrences();
        }
        RemoteObservationState selectedState =
            mayPresentState && selectedSession!.CurrentState is not null
                ? selectedSession.CurrentState
                : RemoteObservationState.Empty;
        bool mayRetainInteractiveState =
            selectedSession?.Status.State
                == RuntimeHostClientSessionState.Connected
            && HaveSameRuntimeHostAndAttachmentKeys(
                previousState,
                selectedState);
        if (mayRetainInteractiveState)
        {
            RestoreConfirmedShortCircuitConfirmations(
                selectedState,
                retainedShortCircuitConfirmations);
            foreach (KeyValuePair<
                RemotePropertyTarget,
                bool> item
                in retainedRequestedBooleanValues)
            {
                requestedBooleanValues[item.Key] =
                    item.Value;
            }
            foreach (KeyValuePair<
                RemotePropertyTarget,
                string> item
                in retainedRequestedPropertyValueTexts)
            {
                requestedPropertyValueTexts[item.Key] =
                    item.Value;
            }
        }
        bool deferEndpointProjection =
            !clearEventOccurrences
            && mayRetainInteractiveState
            && (HasActiveDirectCommandInteraction()
                || hadActivePropertyValueEditor);
        SetProperty(ref currentState, selectedState, nameof(CurrentState));
        if (!deferEndpointProjection)
        {
            SetProperty(
                ref endpoints,
                RuntimeHostInventoryProjector.Project(
                    selectedState,
                    confirmedReads,
                    requestedBooleanValues,
                    requestedCommandArgumentTexts,
                    requestedPropertyValueTexts),
                nameof(Endpoints));
            ReconcileSelectedEndpoint();
        }
        RaisePropertyChanged(nameof(EndpointCount));
        RaisePropertyChanged(nameof(HasEndpoints));
        RaisePropertyChanged(nameof(SessionState));
        RaisePropertyChanged(nameof(RuntimeHostId));
        RaisePropertyChanged(nameof(ApiVersion));
        PropertyReadMessage = selectedSession switch
        {
            null => "Select a Runtime Host to view its endpoints.",
            { Status.State: RuntimeHostClientSessionState.Connected } => null,
            { Status.State: RuntimeHostClientSessionState.Reconnecting } =>
                "The selected Runtime Host is reconnecting; retained endpoint state is read-only.",
            _ => "The selected Runtime Host is not connected."
        };
    }

    private static bool HaveSameAttachmentKeys(
        RemoteObservationState first,
        RemoteObservationState second)
    {
        HashSet<RemoteEndpointAttachmentKey> firstKeys =
            first.Snapshot?.Attachments
                .Select(
                    attachment =>
                        attachment.Key)
                .ToHashSet()
            ?? [];
        HashSet<RemoteEndpointAttachmentKey> secondKeys =
            second.Snapshot?.Attachments
                .Select(
                    attachment =>
                        attachment.Key)
                .ToHashSet()
            ?? [];

        return firstKeys.SetEquals(
            secondKeys);
    }

    private static bool HaveSameRuntimeHostAndAttachmentKeys(
        RemoteObservationState first,
        RemoteObservationState second) =>
        first.Snapshot is not null
        && second.Snapshot is not null
        && first.Snapshot.RuntimeHostId
            == second.Snapshot.RuntimeHostId
        && HaveSameAttachmentKeys(
            first,
            second);

    private Dictionary<RemoteCommandTarget, string>
        CaptureConfirmedShortCircuitConfirmations()
    {
        return endpoints
            .SelectMany(endpoint => endpoint.Instruments)
            .Where(instrument =>
                instrument.HasConfirmedShortCircuitActivation)
            .Select(instrument =>
                instrument.ConfirmedShortCircuitActivationCommand!)
            .Where(command =>
                command.IsShortCircuitActivationConfirmed)
            .ToDictionary(
                command => command.Target,
                _ => bool.TrueString);
    }

    private void RestoreConfirmedShortCircuitConfirmations(
        RemoteObservationState selectedState,
        IReadOnlyDictionary<RemoteCommandTarget, string> retained)
    {
        if (retained.Count == 0)
        {
            return;
        }

        HashSet<RemoteCommandTarget> exactReadyTargets =
            RuntimeHostInventoryProjector.Project(
                    selectedState)
                .SelectMany(endpoint => endpoint.Instruments)
                .SelectMany(instrument => instrument.Commands)
                .Where(command =>
                    command.EndpointReady
                    && command.IsConfirmedShortCircuitActivation)
                .Select(command => command.Target)
                .ToHashSet();

        foreach (KeyValuePair<RemoteCommandTarget, string> item
            in retained.Where(item =>
                exactReadyTargets.Contains(item.Key)))
        {
            requestedCommandArgumentTexts[item.Key] =
                item.Value;
        }
    }

    private void RetainCurrentInitialModeReadAttempts(
        MultiHostClientSessionSnapshot snapshot)
    {
        foreach (RemoteRuntimeHostPropertyTarget attempt
            in initialModeReadAttempts.Keys
                .Where(
                    attempt =>
                        !snapshot.Sessions.Any(
                            session =>
                                session.Status.State
                                    == RuntimeHostClientSessionState.Connected
                                && session.Status.RuntimeHostId
                                    == attempt.RuntimeHostId
                                && session.CurrentState?.Snapshot?.Attachments.Any(
                                    attachment =>
                                        attachment.Key
                                            == attempt.Target.Attachment)
                                    == true))
                .ToArray())
        {
            initialModeReadAttempts.Remove(attempt);
            initialModeReadValues.Remove(attempt);
        }

        foreach (RemoteRuntimeHostPropertyTarget target
            in initialModeReadValues.Keys
                .Where(
                    target =>
                        snapshot.Sessions.Any(
                            session =>
                                session.Status.RuntimeHostId
                                    == target.RuntimeHostId
                                && session.CurrentState?.PropertyValues.TryGetValue(
                                    target.Target,
                                    out RemotePropertyValue? value)
                                    == true
                                && value.Quality
                                    == RemotePropertyQuality.Good))
                .ToArray())
        {
            initialModeReadValues.Remove(target);
        }
    }

    private void RestoreInitialModeReadValues(
        RuntimeHostProfileSessionSnapshot? selectedSession)
    {
        if (selectedSession?.Status is not
            {
                State: RuntimeHostClientSessionState.Connected,
                RuntimeHostId: { } runtimeHostId
            })
        {
            return;
        }

        foreach (KeyValuePair<
            RemoteRuntimeHostPropertyTarget,
            RemotePropertyValue> item
            in initialModeReadValues.Where(
                item =>
                    item.Key.RuntimeHostId == runtimeHostId
                    && selectedSession.CurrentState?.Snapshot?.Attachments.Any(
                        attachment =>
                            attachment.Key
                                == item.Key.Target.Attachment)
                        == true))
        {
            confirmedReads[item.Key.Target] =
                item.Value;
        }
    }

    private void StartInitialModeReadsIfRequired()
    {
        if (multiHostCoordinator is null
            || SelectedRuntimeHost is not
            {
                SessionState: RuntimeHostClientSessionState.Connected,
                AuthoritativeRuntimeHostId: { } runtimeHostId
            })
        {
            return;
        }

        foreach (InstrumentInventoryItemViewModel instrument
            in endpoints
                .Where(endpoint => endpoint.IsReady)
                .SelectMany(endpoint => endpoint.Instruments)
                .Where(instrument =>
                    instrument.HasModeSelectionSelector
                    && !instrument.ModeSelectionCommands.Any(
                        command => command.IsActiveModeSelection)))
        {
            PropertyInventoryItemViewModel? operatingMode =
                instrument.Properties.SingleOrDefault(
                    property =>
                        property.CanRead
                        && string.Equals(
                            property.Path,
                            "Operating.Mode",
                            StringComparison.Ordinal));
            if (operatingMode is null)
            {
                continue;
            }

            var target = new RemoteRuntimeHostPropertyTarget(
                runtimeHostId,
                operatingMode.Target);
            if (!initialModeReadAttempts.ContainsKey(target))
            {
                long attemptId =
                    checked(++nextInitialModeReadAttemptId);
                initialModeReadAttempts.Add(
                    target,
                    attemptId);
                ReadInitialModeAsync(
                    target,
                    attemptId);
            }
        }
    }

    private async void ReadInitialModeAsync(
        RemoteRuntimeHostPropertyTarget target,
        long attemptId)
    {
        RemotePropertyOperationResult result;
        try
        {
            result = await multiHostCoordinator!
                .ReadPropertyAsync(target)
                .ConfigureAwait(true);
        }
        catch
        {
            return;
        }

        RemotePropertyValue? confirmedValue =
            result.IsSuccess
                ? result.ConfirmedValue
                : null;
        if (confirmedValue?.Quality
                != RemotePropertyQuality.Good
            || !initialModeReadAttempts.TryGetValue(
                target,
                out long currentAttemptId)
            || currentAttemptId != attemptId
            || SelectedRuntimeHost?.SessionState
                != RuntimeHostClientSessionState.Connected
            || SelectedRuntimeHost?.AuthoritativeRuntimeHostId
                != target.RuntimeHostId
            || currentState.Snapshot?.Attachments.Any(
                attachment =>
                    attachment.Key
                        == target.Target.Attachment)
                != true)
        {
            return;
        }

        confirmedReads[target.Target] =
            confirmedValue;
        initialModeReadValues[target] =
            confirmedValue;
        SetProperty(
            ref endpoints,
            RuntimeHostInventoryProjector.Project(
                currentState,
                confirmedReads,
                requestedBooleanValues,
                requestedCommandArgumentTexts,
                requestedPropertyValueTexts),
            nameof(Endpoints));
        ReconcileSelectedEndpoint();
    }

    private static bool ShouldClearEventOccurrences(
        RuntimeHostProfileSessionSnapshot? previousSession,
        RuntimeHostProfileSessionSnapshot? currentSession)
    {
        if (previousSession is null
            || currentSession is null
            || previousSession.ProfileId != currentSession.ProfileId)
        {
            return true;
        }

        if (currentSession.Status.State
            is RuntimeHostClientSessionState.Disconnected
                or RuntimeHostClientSessionState.Connecting
                or RuntimeHostClientSessionState.Disconnecting
                or RuntimeHostClientSessionState.Faulted)
        {
            return true;
        }

        return currentSession.Status.State
                == RuntimeHostClientSessionState.Connected
            && previousSession.Status.State
                != RuntimeHostClientSessionState.Connected;
    }

    public async Task ConnectSelectedRuntimeHostAsync()
    {
        RuntimeHostProfileItemViewModel selected = SelectedRuntimeHost
            ?? throw new InvalidOperationException("A runtime-host profile must be selected.");
        if (multiHostCoordinator is null)
            throw new InvalidOperationException("The multi-host coordinator is not configured.");
        if (!selected.IsEnabled)
            throw new InvalidOperationException("The selected runtime-host profile is disabled.");
        IsBusy = true;
        FailureMessage = null;
        try { await multiHostCoordinator.ConnectAsync(selected.ProfileId).ConfigureAwait(true); }
        catch { FailureMessage = "The selected runtime-host connection could not be started."; }
        finally { IsBusy = false; }
    }

    public async Task DisconnectSelectedRuntimeHostAsync()
    {
        RuntimeHostProfileItemViewModel selected = SelectedRuntimeHost
            ?? throw new InvalidOperationException("A runtime-host profile must be selected.");
        if (multiHostCoordinator is null)
            throw new InvalidOperationException("The multi-host coordinator is not configured.");
        IsBusy = true;
        FailureMessage = null;
        try { await multiHostCoordinator.DisconnectAsync(selected.ProfileId).ConfigureAwait(true); }
        catch { FailureMessage = "The selected runtime-host connection could not be stopped cleanly."; }
        finally { IsBusy = false; }
    }

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

    public DelegateCommand<PropertyGroupItemViewModel>
        ReadPropertyGroupCommand
    {
        get;
    }

    public DelegateCommand<PropertyInventoryItemViewModel>
        WritePropertyCommand
    {
        get;
    }

    public DelegateCommand<PropertyInventoryItemViewModel>
        WriteBooleanPropertyCommand =>
            WritePropertyCommand;

    public DelegateCommand<CommandInventoryItemViewModel> ExecuteCommand
    {
        get;
    }

    public DelegateCommand OpenDiagnosticsCommand { get; }
    public DelegateCommand OpenMediaCommand { get; }

    public void Configure(
        IRuntimeHostClientSessionController controller,
        IClientConfigurationFilePicker filePicker,
        IClientDiagnosticsWindowController? diagnosticsController = null,
        IClientMediaWindowController? mediaController = null)
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
        diagnosticsWindowController = diagnosticsController;
        mediaWindowController = mediaController;
        OpenDiagnosticsCommand.RaiseCanExecuteChanged();
        OpenMediaCommand.RaiseCanExecuteChanged();
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
        EventPayloadFormatResult payloadPresentation =
            EventPayloadFormatter.Format(
                descriptor.Payload,
                RemoteEventPayloadValueNormalizer.Normalize(
                    payload.Value));
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
                descriptor.Payload?.DisplayName
                    ?? "Payload",
                descriptor.Payload?.Description
                    ?? string.Empty,
                payloadPresentation.Text,
                payloadPresentation.Status);
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

    public void ApplyMultiHostEventOccurred(RuntimeHostProfileEventOccurredEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        RuntimeHostProfileItemViewModel? selected = SelectedRuntimeHost;
        if (selected is null
            || selected.ProfileId != eventArgs.ProfileId
            || selected.AuthoritativeRuntimeHostId != eventArgs.RuntimeHostId)
            return;
        ApplyEventOccurred(eventArgs.Observation);
    }

    public void ApplyObservationState(
        RemoteObservationState value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        PreserveRequestedBooleanValues();
        PreserveRequestedPropertyValueTexts();
        PreserveRequestedCommandArgumentTexts();
        SetProperty(
            ref currentState,
            value,
            nameof(CurrentState));
        RemoveConfirmedReadsForMissingAttachments(
            value);

        if (HasActiveCommandArgumentEditor()
            || HasActivePropertyValueEditor()
            || (HasActiveDirectCommandInteraction()
                && HaveSameAttachmentKeys(
                    currentState,
                    value)))
        {
            return;
        }

        SetProperty(
            ref endpoints,
            RuntimeHostInventoryProjector.Project(
                value,
                confirmedReads,
                requestedBooleanValues,
                requestedCommandArgumentTexts,
                requestedPropertyValueTexts),
            nameof(Endpoints));
        ReconcileSelectedEndpoint();
        RaisePropertyChanged(
            nameof(EndpointCount));
        RaisePropertyChanged(
            nameof(HasEndpoints));
    }

    private void RemoveConfirmedReadsForMissingAttachments(
        RemoteObservationState state)
    {
        HashSet<RemoteEndpointAttachmentKey> currentAttachments =
            state.Snapshot?.Attachments
                .Select(
                    attachment =>
                        attachment.Key)
                .ToHashSet()
            ?? [];

        foreach (RemotePropertyTarget target
            in confirmedReads.Keys
                .Where(
                    target =>
                        !currentAttachments.Contains(
                            target.Attachment))
                .ToArray())
        {
            confirmedReads.Remove(
                target);
        }
    }

    public async Task ConnectAsync()
    {
        if (configurationFilePicker is null)
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

        await ConnectAsync(
            configurationFilePath);
    }

    public async Task ConnectAsync(
        string configurationFilePath)
    {
        if (sessionController is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        if (string.IsNullOrWhiteSpace(
                configurationFilePath))
        {
            throw new ArgumentException(
                "The client configuration file path must not be empty.",
                nameof(configurationFilePath));
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

        if (sessionController is null && multiHostCoordinator is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        if (!IsOperationHostConnected
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
            RemoteRuntimeHostId? operationHostId = SelectedRuntimeHost?.AuthoritativeRuntimeHostId;
            RemotePropertyOperationResult result = multiHostCoordinator is null
                ? await sessionController!.ReadPropertyAsync(property.Target).ConfigureAwait(true)
                : await multiHostCoordinator.ReadPropertyAsync(
                        new RemoteRuntimeHostPropertyTarget(
                            operationHostId ?? throw new InvalidOperationException("The selected host has no authoritative identity."),
                            property.Target))
                    .ConfigureAwait(
                        true);

            if (multiHostCoordinator is not null
                && SelectedRuntimeHost?.AuthoritativeRuntimeHostId != operationHostId)
                return;

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
                        requestedCommandArgumentTexts,
                        requestedPropertyValueTexts),
                    nameof(Endpoints));
            ReconcileSelectedEndpoint();
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

    public async Task WritePropertyAsync(
        PropertyInventoryItemViewModel property)
    {
        ArgumentNullException.ThrowIfNull(
            property);

        if (sessionController is null && multiHostCoordinator is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        if (!IsOperationHostConnected
            || !property.CanWrite)
        {
            return;
        }

        PropertyInputParseResult inputResult =
            property.InputResult;
        if (!inputResult.IsSuccess)
        {
            PropertyReadMessage =
                $"{property.DisplayName}: {inputResult.Message}";
            return;
        }

        IsBusy =
            true;
        if (property.HasBooleanEditor)
        {
            requestedBooleanValues[property.Target] =
                property.RequestedBooleanValue;
        }
        else
        {
            requestedPropertyValueTexts[property.Target] =
                property.RequestedValueText;
        }
        PropertyReadMessage =
            $"Writing {property.DisplayName}...";

        try
        {
            RemoteRuntimeHostId? operationHostId = SelectedRuntimeHost?.AuthoritativeRuntimeHostId;
            RemoteValue requestedValue = PropertyInputRemoteValueMapper.Map(inputResult.Value!);
            RemotePropertyOperationResult result = multiHostCoordinator is null
                ? await sessionController!.WritePropertyAsync(property.Target, requestedValue).ConfigureAwait(true)
                : await multiHostCoordinator.WritePropertyAsync(
                        new RemoteRuntimeHostPropertyTarget(
                            operationHostId ?? throw new InvalidOperationException("The selected host has no authoritative identity."),
                            property.Target),
                        requestedValue)
                    .ConfigureAwait(
                        true);

            if (multiHostCoordinator is not null
                && SelectedRuntimeHost?.AuthoritativeRuntimeHostId != operationHostId)
                return;

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
                        requestedCommandArgumentTexts,
                        requestedPropertyValueTexts),
                    nameof(Endpoints));
            ReconcileSelectedEndpoint();
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

    public Task WriteBooleanPropertyAsync(
        PropertyInventoryItemViewModel property)
    {
        return WritePropertyAsync(
            property);
    }

    public async Task ExecuteCommandAsync(
        CommandInventoryItemViewModel command)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        if (sessionController is null && multiHostCoordinator is null)
        {
            throw new InvalidOperationException(
                "The main window client services are not configured.");
        }

        if (!IsOperationHostConnected
            || !command.EndpointReady)
        {
            return;
        }

        RemoteValue? argument =
            null;

        if (command.RequiresArgument)
        {
            if (command.IsConfirmedShortCircuitActivation)
            {
                if (command.RequestedBooleanArgument is not true)
                {
                    PropertyReadMessage =
                        $"{command.DisplayName}: explicit Boolean confirmation true is required.";
                    return;
                }

                argument =
                    RemoteValue.FromBoolean(
                        true);
            }
            else if (command.ArgumentDataType != "ByteArray"
                || !Hase.Operator.Input.ByteArrayHexadecimalParser.TryParse(
                    command.RequestedArgumentText,
                    out Hase.Core.Domain.Data.ByteArrayValue?
                        byteArrayArgument))
            {
                PropertyReadMessage =
                    $"{command.DisplayName}: enter valid hexadecimal bytes.";
                return;
            }
            else
            {
                argument =
                    RemoteValue.FromByteArray(
                        byteArrayArgument);
            }
        }

        IsBusy =
            true;
        PropertyReadMessage =
            $"Executing {command.DisplayName}...";

        try
        {
            RemoteRuntimeHostId? operationHostId = SelectedRuntimeHost?.AuthoritativeRuntimeHostId;
            var localRequest = new RemoteCommandExecutionRequest(command.Target, argument);
            RemoteCommandOperationResult result = multiHostCoordinator is null
                ? await sessionController!.ExecuteCommandAsync(localRequest).ConfigureAwait(true)
                : await multiHostCoordinator.ExecuteCommandAsync(
                    new RemoteRuntimeHostCommandExecutionRequest(
                        operationHostId ?? throw new InvalidOperationException("The selected host has no authoritative identity."),
                        localRequest))
                    .ConfigureAwait(
                        true);

            if (multiHostCoordinator is not null
                && SelectedRuntimeHost?.AuthoritativeRuntimeHostId != operationHostId)
                return;

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
            ClearSingleUseCommandConfirmation(
                command);
            IsBusy =
                false;
        }
    }

    private void ClearSingleUseCommandConfirmation(
        CommandInventoryItemViewModel command)
    {
        if (!command.IsConfirmedShortCircuitActivation)
        {
            return;
        }

        requestedCommandArgumentTexts.Remove(
            command.Target);
        command.RequestedBooleanArgument =
            null;

        foreach (CommandInventoryItemViewModel current
            in endpoints
                .SelectMany(endpoint => endpoint.Instruments)
                .SelectMany(instrument => instrument.Commands)
                .Where(current =>
                    current.Target == command.Target
                    && current.IsConfirmedShortCircuitActivation))
        {
            current.RequestedBooleanArgument =
                null;
        }
    }

    private async void ExecuteConnect()
    {
        await ConnectAsync();
    }

    private async void ExecuteConnectSelectedRuntimeHost() => await ConnectSelectedRuntimeHostAsync();
    private async void ExecuteDisconnectSelectedRuntimeHost() => await DisconnectSelectedRuntimeHostAsync();

    private async void ExecuteToggleRuntimeHostConnection(
        RuntimeHostProfileItemViewModel? host)
    {
        if (host is null)
        {
            return;
        }

        if (host.ProfileId != selectedRuntimeHostProfileId)
        {
            SelectRuntimeHost(host.ProfileId);
        }

        if (host.SessionState is
            RuntimeHostClientSessionState.Connected
                or RuntimeHostClientSessionState.Connecting
                or RuntimeHostClientSessionState.Reconnecting)
        {
            await DisconnectSelectedRuntimeHostAsync();
        }
        else
        {
            await ConnectSelectedRuntimeHostAsync();
        }
    }

    private void ReconcileSelectedEndpoint()
    {
        if (selectedEndpointKey is not null
            && endpoints.All(endpoint => endpoint.Key != selectedEndpointKey))
        {
            selectedEndpointKey = null;
        }

        ApplyEndpointSelectionFlags();

        RaisePropertyChanged(nameof(SelectedEndpoint));
    }

    private void ApplyEndpointSelectionFlags()
    {
        foreach (EndpointInventoryItemViewModel endpoint in endpoints)
        {
            endpoint.IsSelected =
                selectedEndpointKey is not null
                && endpoint.Key == selectedEndpointKey;
        }
    }

    private void PreserveRequestedBooleanValues()
    {
        requestedBooleanValues.Clear();

        foreach (KeyValuePair<RemotePropertyTarget, bool> item
            in CaptureRequestedBooleanValues())
        {
            requestedBooleanValues[item.Key] =
                item.Value;
        }
    }

    private Dictionary<RemotePropertyTarget, bool>
        CaptureRequestedBooleanValues()
    {
        return endpoints
            .SelectMany(
                endpoint =>
                    endpoint.Instruments)
            .SelectMany(
                instrument =>
                    instrument.Properties)
            .Where(
                property =>
                    property.SupportsBooleanWrite)
            .ToDictionary(
                property =>
                    property.Target,
                property =>
                    property.RequestedBooleanValue);
    }

    private void PreserveRequestedPropertyValueTexts()
    {
        requestedPropertyValueTexts.Clear();

        foreach (KeyValuePair<RemotePropertyTarget, string> item
            in CaptureRequestedPropertyValueTexts())
        {
            requestedPropertyValueTexts[item.Key] =
                item.Value;
        }
    }

    private Dictionary<RemotePropertyTarget, string>
        CaptureRequestedPropertyValueTexts()
    {
        return endpoints
            .SelectMany(
                endpoint =>
                    endpoint.Instruments)
            .SelectMany(
                instrument =>
                    instrument.Properties)
            .Where(
                property =>
                    property.HasTextEditor)
            .ToDictionary(
                property =>
                    property.Target,
                property =>
                    property.RequestedValueText);
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

    private bool HasActiveCommandArgumentEditor()
    {
        return endpoints
            .SelectMany(
                endpoint =>
                    endpoint.Instruments)
            .SelectMany(
                instrument =>
                    instrument.Commands)
            .Any(
                command =>
                    command.IsEditingArgument);
    }

    private bool HasActiveDirectCommandInteraction()
    {
        return endpoints
            .SelectMany(
                endpoint =>
                    endpoint.Instruments)
            .Any(
                instrument =>
                    instrument.IsInvokingModeCommand
                    || instrument.IsInvokingInputCommand
                    || instrument.IsInvokingShortCircuitCommand);
    }

    private bool HasActivePropertyValueEditor()
    {
        return endpoints
            .SelectMany(
                endpoint =>
                    endpoint.Instruments)
            .SelectMany(
                instrument =>
                    instrument.Properties)
            .Any(
                property =>
                    property.IsEditingRequestedValue);
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

    private async void ExecuteReadPropertyGroup(
        PropertyGroupItemViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        foreach (PropertyInventoryItemViewModel member in group.Members)
        {
            if (member.CanRead)
            {
                await ReadPropertyAsync(
                    member);
            }
        }
    }

    private async void ExecuteWriteProperty(
        PropertyInventoryItemViewModel? property)
    {
        if (property is not null)
        {
            await WritePropertyAsync(
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
        ReadPropertyGroupCommand.RaiseCanExecuteChanged();
        WritePropertyCommand.RaiseCanExecuteChanged();
        ExecuteCommand.RaiseCanExecuteChanged();
        ConnectSelectedRuntimeHostCommand.RaiseCanExecuteChanged();
        DisconnectSelectedRuntimeHostCommand.RaiseCanExecuteChanged();
        ToggleRuntimeHostConnectionCommand.RaiseCanExecuteChanged();
    }
}

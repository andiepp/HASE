using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowSelectedHostProjectionTests
{
    [Fact]
    public void SameGenerationRefresh_ActiveModePress_ShouldDeferEndpointProjection()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("c1d9ef89-267a-4de4-9ed1-04848635e6ab");
        RemoteObservationState firstState = ModeState("host-01", generation, 1);
        RemoteObservationState secondState = ModeState("host-01", generation, 2);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        InstrumentInventoryItemViewModel instrument = SelectedInstrument(viewModel);
        instrument.IsInvokingModeCommand = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, secondState)]));

        Assert.Same(secondState, viewModel.CurrentState);
        Assert.Same(instrument, SelectedInstrument(viewModel));

        instrument.IsInvokingModeCommand = false;
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, secondState)]));

        Assert.NotSame(instrument, SelectedInstrument(viewModel));
    }

    [Fact]
    public void GenerationChange_ActiveModePress_ShouldReplaceImmediately()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        RemoteObservationState firstState = ModeState(
            "host-01",
            Guid.Parse("d1d9ef89-267a-4de4-9ed1-04848635e6ab"),
            1);
        RemoteObservationState replacementState = ModeState(
            "host-01",
            Guid.Parse("e1d9ef89-267a-4de4-9ed1-04848635e6ab"),
            2);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        InstrumentInventoryItemViewModel instrument = SelectedInstrument(viewModel);
        instrument.IsInvokingModeCommand = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, replacementState)]));

        Assert.NotSame(instrument, SelectedInstrument(viewModel));
    }

    [Fact]
    public void SameGenerationRefresh_ActiveInputPress_ShouldDeferEndpointProjection()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("f1d9ef89-267a-4de4-9ed1-04848635e6ab");
        RemoteObservationState firstState = ModeState("host-01", generation, 1);
        RemoteObservationState secondState = ModeState("host-01", generation, 2);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        InstrumentInventoryItemViewModel instrument = SelectedInstrument(viewModel);
        instrument.IsInvokingInputCommand = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, secondState)]));

        Assert.Same(secondState, viewModel.CurrentState);
        Assert.Same(instrument, SelectedInstrument(viewModel));

        instrument.IsInvokingInputCommand = false;
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, secondState)]));

        Assert.NotSame(instrument, SelectedInstrument(viewModel));
    }

    [Fact]
    public void GenerationChange_ActiveInputPress_ShouldReplaceImmediately()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        RemoteObservationState firstState = ModeState(
            "host-01",
            Guid.Parse("a2d9ef89-267a-4de4-9ed1-04848635e6ab"),
            1);
        RemoteObservationState replacementState = ModeState(
            "host-01",
            Guid.Parse("b2d9ef89-267a-4de4-9ed1-04848635e6ab"),
            2);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        InstrumentInventoryItemViewModel instrument = SelectedInstrument(viewModel);
        instrument.IsInvokingInputCommand = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, replacementState)]));

        Assert.NotSame(instrument, SelectedInstrument(viewModel));
    }

    [Fact]
    public void SameGenerationRefresh_ActiveShortCircuitPress_ShouldDeferEndpointProjection()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("c2d9ef89-267a-4de4-9ed1-04848635e6ab");
        RemoteObservationState firstState = ModeState("host-01", generation, 1);
        RemoteObservationState secondState = ModeState("host-01", generation, 2);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        InstrumentInventoryItemViewModel instrument = SelectedInstrument(viewModel);
        instrument.IsInvokingShortCircuitCommand = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, secondState)]));

        Assert.Same(secondState, viewModel.CurrentState);
        Assert.Same(instrument, SelectedInstrument(viewModel));

        instrument.IsInvokingShortCircuitCommand = false;
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, secondState)]));

        Assert.NotSame(instrument, SelectedInstrument(viewModel));
    }

    [Fact]
    public void SameGenerationRefresh_ConfirmedShortCircuit_ShouldRetainConfirmation()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("d2d9ef89-267a-4de4-9ed1-04848635e6ab");
        RemoteObservationState firstState = ModeState("host-01", generation, 1);
        RemoteObservationState secondState = ModeState("host-01", generation, 2);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        ShortCircuitCommand(viewModel).IsShortCircuitActivationConfirmed = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, secondState)]));

        Assert.True(
            ShortCircuitCommand(viewModel).IsShortCircuitActivationConfirmed);
    }

    [Fact]
    public void GenerationChange_ConfirmedShortCircuit_ShouldClearConfirmation()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        RemoteObservationState firstState = ModeState(
            "host-01",
            Guid.Parse("e2d9ef89-267a-4de4-9ed1-04848635e6ab"),
            1);
        RemoteObservationState replacementState = ModeState(
            "host-01",
            Guid.Parse("f2d9ef89-267a-4de4-9ed1-04848635e6ab"),
            2);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        ShortCircuitCommand(viewModel).IsShortCircuitActivationConfirmed = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, replacementState)]));

        Assert.False(
            ShortCircuitCommand(viewModel).IsShortCircuitActivationConfirmed);
    }

    [Fact]
    public void Reconnecting_ConfirmedShortCircuit_ShouldClearConfirmation()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        RemoteObservationState state = ModeState(
            "host-01",
            Guid.Parse("03d9ef89-267a-4de4-9ed1-04848635e6ab"),
            1);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, state));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        ShortCircuitCommand(viewModel).IsShortCircuitActivationConfirmed = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Reconnecting, state)]));

        Assert.False(
            ShortCircuitCommand(viewModel).IsShortCircuitActivationConfirmed);
    }

    [Fact]
    public void HostSelectionChange_ConfirmedShortCircuit_ShouldClearConfirmation()
    {
        RuntimeHostProfile first = Profile("first", "host-01");
        RuntimeHostProfile second = Profile("second", "host-02");
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(
            new RuntimeHostProfileRegistry([first, second]));
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(
                first,
                RuntimeHostClientSessionState.Connected,
                ModeState(
                    "host-01",
                    Guid.Parse("13d9ef89-267a-4de4-9ed1-04848635e6ab"),
                    1)),
            Session(
                second,
                RuntimeHostClientSessionState.Connected,
                ModeState(
                    "host-02",
                    Guid.Parse("23d9ef89-267a-4de4-9ed1-04848635e6ab"),
                    1))]));
        viewModel.SelectRuntimeHost(first.ProfileId);
        ShortCircuitCommand(viewModel).IsShortCircuitActivationConfirmed = true;

        viewModel.SelectRuntimeHost(second.ProfileId);

        Assert.False(
            ShortCircuitCommand(viewModel).IsShortCircuitActivationConfirmed);
    }

    [Fact]
    public void SameGenerationRefresh_ActivePropertyEditor_ShouldRetainInstanceAndText()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("33d9ef89-267a-4de4-9ed1-04848635e6ab");
        RemoteObservationState firstState = PropertyState(
            "host-01",
            generation,
            1,
            0.125);
        RemoteObservationState secondState = PropertyState(
            "host-01",
            generation,
            2,
            0.25);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        PropertyInventoryItemViewModel property = TargetProperty(viewModel);
        property.RequestedValueText = "0.2";
        property.IsEditingRequestedValue = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, secondState)]));

        Assert.Same(secondState, viewModel.CurrentState);
        Assert.Same(property, TargetProperty(viewModel));
        Assert.Equal("0.2", property.RequestedValueText);
    }

    [Fact]
    public void SameGenerationRefresh_CompletedPropertyEdit_ShouldSurviveProjection()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("43d9ef89-267a-4de4-9ed1-04848635e6ab");
        RemoteObservationState firstState = PropertyState(
            "host-01",
            generation,
            1,
            0.125);
        RemoteObservationState secondState = PropertyState(
            "host-01",
            generation,
            2,
            0.25);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        PropertyInventoryItemViewModel previous = TargetProperty(viewModel);
        previous.RequestedValueText = "0.2";

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, secondState)]));

        PropertyInventoryItemViewModel projected = TargetProperty(viewModel);
        Assert.NotSame(previous, projected);
        Assert.Equal("0.2", projected.RequestedValueText);
        Assert.True(projected.CanSubmitWrite);
        Assert.Equal("0.25", projected.Value);
    }

    [Fact]
    public void GenerationChange_ActivePropertyEditor_ShouldDiscardRequestedText()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        RemoteObservationState firstState = PropertyState(
            "host-01",
            Guid.Parse("53d9ef89-267a-4de4-9ed1-04848635e6ab"),
            1,
            0.125);
        RemoteObservationState replacementState = PropertyState(
            "host-01",
            Guid.Parse("63d9ef89-267a-4de4-9ed1-04848635e6ab"),
            2,
            0.25);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, firstState));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        PropertyInventoryItemViewModel previous = TargetProperty(viewModel);
        previous.RequestedValueText = "0.2";
        previous.IsEditingRequestedValue = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, replacementState)]));

        PropertyInventoryItemViewModel projected = TargetProperty(viewModel);
        Assert.NotSame(previous, projected);
        Assert.Equal("0.25", projected.RequestedValueText);
        Assert.False(projected.IsEditingRequestedValue);
    }

    [Fact]
    public void Reconnecting_ActivePropertyEditor_ShouldDiscardRequestedText()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        RemoteObservationState state = PropertyState(
            "host-01",
            Guid.Parse("73d9ef89-267a-4de4-9ed1-04848635e6ab"),
            1,
            0.125);
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, state));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        PropertyInventoryItemViewModel previous = TargetProperty(viewModel);
        previous.RequestedValueText = "0.2";
        previous.IsEditingRequestedValue = true;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Reconnecting, state)]));

        PropertyInventoryItemViewModel projected = TargetProperty(viewModel);
        Assert.NotSame(previous, projected);
        Assert.Equal("0.125", projected.RequestedValueText);
        Assert.Contains(
            "read-only",
            viewModel.PropertyReadMessage);
    }

    [Fact]
    public void HostSelectionChange_ActivePropertyEditor_ShouldDiscardRequestedText()
    {
        RuntimeHostProfile first = Profile("first", "host-01");
        RuntimeHostProfile second = Profile("second", "host-02");
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(
            new RuntimeHostProfileRegistry([first, second]));
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(
                first,
                RuntimeHostClientSessionState.Connected,
                PropertyState(
                    "host-01",
                    Guid.Parse("83d9ef89-267a-4de4-9ed1-04848635e6ab"),
                    1,
                    0.125)),
            Session(
                second,
                RuntimeHostClientSessionState.Connected,
                PropertyState(
                    "host-02",
                    Guid.Parse("93d9ef89-267a-4de4-9ed1-04848635e6ab"),
                    1,
                    0.25))]));
        viewModel.SelectRuntimeHost(first.ProfileId);
        PropertyInventoryItemViewModel previous = TargetProperty(viewModel);
        previous.RequestedValueText = "0.2";
        previous.IsEditingRequestedValue = true;

        viewModel.SelectRuntimeHost(second.ProfileId);

        PropertyInventoryItemViewModel projected = TargetProperty(viewModel);
        Assert.NotSame(previous, projected);
        Assert.Equal("0.25", projected.RequestedValueText);
        Assert.False(projected.IsEditingRequestedValue);
    }

    [Fact]
    public void NoSelection_ShouldExposeEmptyStateAndGuidance()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        MainWindowViewModel viewModel = Create(profile, Session(profile, RuntimeHostClientSessionState.Disconnected));
        Assert.False(viewModel.CurrentState.IsInitialized);
        Assert.Empty(viewModel.Endpoints);
        Assert.Equal("Select a Runtime Host to view its endpoints.", viewModel.PropertyReadMessage);
    }

    [Fact]
    public void NoSelection_ShouldExposeTruthfulEmptySummary()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Disconnected));

        Assert.Equal("No host selected", viewModel.SessionState);
        Assert.Equal("Not connected", viewModel.RuntimeHostId);
        Assert.Equal("Not available", viewModel.ApiVersion);
        Assert.Equal(0, viewModel.EndpointCount);
    }

    [Fact]
    public void ConnectedSelection_ShouldApplyAuthoritativeState()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        MainWindowViewModel viewModel = Create(profile, Session(profile, RuntimeHostClientSessionState.Connected, State("host-01")));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        Assert.True(viewModel.CurrentState.IsInitialized);
        Assert.Null(viewModel.PropertyReadMessage);
    }

    [Fact]
    public void ConnectedSelection_ShouldExposeSelectedHostSummary()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        MainWindowViewModel viewModel = Create(
            profile,
            Session(
                profile,
                RuntimeHostClientSessionState.Connected,
                State("host-01")));

        viewModel.SelectRuntimeHost(profile.ProfileId);

        Assert.Equal("Connected", viewModel.SessionState);
        Assert.Equal("host-01", viewModel.RuntimeHostId);
        Assert.Equal(RuntimeHostClientApiVersion.Current.ToString(), viewModel.ApiVersion);
    }

    [Fact]
    public void SelectedEndpoint_SameAttachmentRefresh_ShouldBeRetained()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("19a37b52-f7c2-47c8-b32c-915f34a9bc21");
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, ModeState("host-01", generation, 1)));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        viewModel.SelectedEndpoint = Assert.Single(viewModel.Endpoints);

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, ModeState("host-01", generation, 2))]));

        Assert.NotNull(viewModel.SelectedEndpoint);
        Assert.Equal(generation.ToString(), viewModel.SelectedEndpoint!.AttachmentGeneration);
    }

    [Fact]
    public void BooleanRequestedValue_SameAttachmentHostSnapshotRefresh_ShouldBeRetained()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("21a37b52-f7c2-47c8-b32c-915f34a9bc21");
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected,
                BooleanPropertyState("host-01", generation, 1)));
        viewModel.SelectRuntimeHost(profile.ProfileId);

        PropertyInventoryItemViewModel property = viewModel.Endpoints
            .SelectMany(endpoint => endpoint.Instruments)
            .SelectMany(instrument => instrument.Properties)
            .Single(candidate => candidate.HasBooleanEditor);
        bool requested = !property.RequestedBooleanValue;
        property.RequestedBooleanValue = requested;

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected,
                BooleanPropertyState("host-01", generation, 2))]));

        PropertyInventoryItemViewModel refreshed = viewModel.Endpoints
            .SelectMany(endpoint => endpoint.Instruments)
            .SelectMany(instrument => instrument.Properties)
            .Single(candidate => candidate.Target == property.Target);

        Assert.Equal(requested, refreshed.RequestedBooleanValue);
    }

    [Fact]
    public void SelectedEndpoint_GenerationChange_ShouldBeCleared()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected,
                ModeState("host-01", Guid.Parse("29a37b52-f7c2-47c8-b32c-915f34a9bc21"), 1)));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        viewModel.SelectedEndpoint = Assert.Single(viewModel.Endpoints);

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected,
                ModeState("host-01", Guid.Parse("39a37b52-f7c2-47c8-b32c-915f34a9bc21"), 2))]));

        Assert.Null(viewModel.SelectedEndpoint);
    }

    [Fact]
    public void ChangingRuntimeHost_ShouldClearSelectedEndpoint()
    {
        RuntimeHostProfile first = Profile("first", "host-01");
        RuntimeHostProfile second = Profile("second", "host-02");
        Guid generation = Guid.Parse("49a37b52-f7c2-47c8-b32c-915f34a9bc21");
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([first, second]));
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(first, RuntimeHostClientSessionState.Connected, ModeState("host-01", generation, 1)),
            Session(second, RuntimeHostClientSessionState.Disconnected)]));
        viewModel.SelectRuntimeHost(first.ProfileId);
        viewModel.SelectedEndpoint = Assert.Single(viewModel.Endpoints);

        viewModel.SelectRuntimeHost(second.ProfileId);

        Assert.Null(viewModel.SelectedEndpoint);
    }

    [Fact]
    public void ReconnectingSelection_ShouldRetainReadOnlyState()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        MainWindowViewModel viewModel = Create(profile, Session(profile, RuntimeHostClientSessionState.Reconnecting, State("host-01")));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        Assert.True(viewModel.CurrentState.IsInitialized);
        Assert.Contains("read-only", viewModel.PropertyReadMessage);
    }

    [Fact]
    public void ChangingToDisconnectedHost_ShouldClearPreviousState()
    {
        RuntimeHostProfile first = Profile("first", "host-01");
        RuntimeHostProfile second = Profile("second", "host-02");
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([first, second]));
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(first, RuntimeHostClientSessionState.Connected, State("host-01")),
            Session(second, RuntimeHostClientSessionState.Disconnected)]));
        viewModel.SelectRuntimeHost(first.ProfileId);
        Assert.True(viewModel.CurrentState.IsInitialized);
        viewModel.SelectRuntimeHost(second.ProfileId);
        Assert.False(viewModel.CurrentState.IsInitialized);
        Assert.Empty(viewModel.Endpoints);
    }

    [Fact]
    public void ChangingSelection_ShouldRefreshSummaryFromSelectedHostOnly()
    {
        RuntimeHostProfile first = Profile("first", "host-01");
        RuntimeHostProfile second = Profile("second", "host-02");
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([first, second]));
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(first, RuntimeHostClientSessionState.Connected, State("host-01")),
            Session(second, RuntimeHostClientSessionState.Disconnected)]));
        viewModel.SelectRuntimeHost(first.ProfileId);

        viewModel.SelectRuntimeHost(second.ProfileId);

        Assert.Equal("Disconnected", viewModel.SessionState);
        Assert.Equal("Not connected", viewModel.RuntimeHostId);
        Assert.Equal("Not available", viewModel.ApiVersion);
        Assert.Equal(0, viewModel.EndpointCount);
    }

    private static MainWindowViewModel Create(RuntimeHostProfile profile, RuntimeHostProfileSessionSnapshot session)
    {
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([profile]));
        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([session]));
        return viewModel;
    }

    private static RuntimeHostProfile Profile(string id, string host) =>
        new(new RuntimeHostProfileId(id), id, new RemoteRuntimeHostId(host));

    private static RuntimeHostProfileSessionSnapshot Session(
        RuntimeHostProfile profile,
        RuntimeHostClientSessionState state,
        RemoteObservationState? currentState = null)
    {
        RuntimeHostClientSessionStatus status = state is RuntimeHostClientSessionState.Connected or RuntimeHostClientSessionState.Reconnecting
            ? new RuntimeHostClientSessionStatus(state, profile.ExpectedRuntimeHostId, RuntimeHostClientApiVersion.Current)
            : new RuntimeHostClientSessionStatus(state);
        return new RuntimeHostProfileSessionSnapshot(profile, status, DateTimeOffset.UtcNow, currentState);
    }

    private static RemoteObservationState State(string host) =>
        new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(new RemoteRuntimeHostId(host), RuntimeHostClientApiVersion.Current, []),
                new RemoteObservationSequence(0)));

    private static InstrumentInventoryItemViewModel SelectedInstrument(
        MainWindowViewModel viewModel) =>
        Assert.Single(
            Assert.Single(
                viewModel.Endpoints)
            .Instruments);

    private static CommandInventoryItemViewModel ShortCircuitCommand(
        MainWindowViewModel viewModel) =>
        Assert.IsType<CommandInventoryItemViewModel>(
            SelectedInstrument(viewModel)
                .ConfirmedShortCircuitActivationCommand);

    private static PropertyInventoryItemViewModel TargetProperty(
        MainWindowViewModel viewModel) =>
        Assert.Single(
            SelectedInstrument(viewModel)
                .Properties);

    private static RemoteObservationState PropertyState(
        string host,
        Guid generation,
        ulong sequence,
        double value)
    {
        var property = new PropertyDescriptor(
            new PropertyId("target-current"),
            DescriptorPath.Parse("Target.Current"),
            "Target current",
            new NumericDataDescriptor(
                Quantities.Current,
                Units.Ampere,
                new ValueRange(
                    0,
                    30)))
        {
            AccessMode = PropertyAccessMode.ReadWrite
        };
        var instrument = new InstrumentDescriptor(
            new InstrumentId("electronic-load-01"),
            "Electronic Load",
            new InstrumentKind("ElectronicLoad"))
        {
            Interface = new InstrumentInterface(properties: [property])
        };
        var attachment = new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(generation),
            new EndpointDescriptor(new EndpointId("kel-01"), [instrument]),
            new RemoteEndpointConnectionStatus(RemoteEndpointConnectionState.Ready));
        var reducer = new RemoteObservationReducer();
        RemoteObservationState state = reducer.Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    new RemoteRuntimeHostId(host),
                    RuntimeHostClientApiVersion.Current,
                    [attachment]),
                new RemoteObservationSequence(0)));
        return reducer.Apply(
            state,
            new RemoteRuntimeHostObservation(
                new RemoteObservationSequence(sequence),
                attachment.Key,
                new RemotePropertyValueChangedObservationPayload(
                    instrument.Id,
                    property.Id,
                    previousValue: null,
                    new RemotePropertyValue(
                        RemoteValue.FromNumeric(value),
                        DateTimeOffset.UnixEpoch,
                        RemotePropertyQuality.Good))));
    }

    private static RemoteObservationState BooleanPropertyState(
        string host,
        Guid generation,
        ulong sequence)
    {
        var property = new PropertyDescriptor(
            new PropertyId("led-enabled"),
            DescriptorPath.Parse("Controller.LedEnabled"),
            "LED Enabled",
            new BooleanDataDescriptor())
        {
            AccessMode = PropertyAccessMode.ReadWrite
        };
        var instrument = new InstrumentDescriptor(
            new InstrumentId("controller-01"),
            "Controller",
            new InstrumentKind("Controller"))
        {
            Interface = new InstrumentInterface(properties: [property])
        };
        var attachment = new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(generation),
            new EndpointDescriptor(new EndpointId("endpoint-03"), [instrument]),
            new RemoteEndpointConnectionStatus(RemoteEndpointConnectionState.Ready));

        return new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    new RemoteRuntimeHostId(host),
                    RuntimeHostClientApiVersion.Current,
                    [attachment]),
                new RemoteObservationSequence(sequence)));
    }

    private static RemoteObservationState ModeState(
        string host,
        Guid generation,
        ulong sequence)
    {
        CommandDescriptor[] commands =
        [
            new CommandDescriptor(DescriptorPath.Parse("Mode.SelectConstantCurrent"), "Select CC"),
            new CommandDescriptor(DescriptorPath.Parse("Mode.SelectConstantVoltage"), "Select CV"),
            new CommandDescriptor(DescriptorPath.Parse("Mode.SelectConstantResistance"), "Select CR"),
            new CommandDescriptor(DescriptorPath.Parse("Mode.SelectConstantPower"), "Select CW"),
            new CommandDescriptor(DescriptorPath.Parse("Mode.SelectShortCircuit"), "Select SHORT"),
            new CommandDescriptor(
                DescriptorPath.Parse("ShortCircuit.Activate"),
                "Activate short circuit",
                new CommandArgumentDescriptor(
                    "Confirmation",
                    new BooleanDataDescriptor()))
        ];
        var instrument = new InstrumentDescriptor(
            new InstrumentId("electronic-load-01"),
            "Electronic Load",
            new InstrumentKind("ElectronicLoad"))
        {
            Interface = new InstrumentInterface(commands: commands)
        };
        var attachment = new RemoteEndpointAttachmentSnapshot(
            new RemoteEndpointAttachmentGeneration(generation),
            new EndpointDescriptor(new EndpointId("kel-01"), [instrument]),
            new RemoteEndpointConnectionStatus(RemoteEndpointConnectionState.Ready));
        return new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    new RemoteRuntimeHostId(host),
                    RuntimeHostClientApiVersion.Current,
                    [attachment]),
                new RemoteObservationSequence(sequence)));
    }

    [Fact]
    public void EndpointItem_Refresh_ShouldNotCompareEqualSoBindingsRefresh()
    {
        // The selected-endpoint detail pane binds the projected item into a
        // content control, and the property system discards an update whose
        // new value compares equal to the current one. If two consecutive
        // projections of the same attachment ever compare equal, the pane
        // freezes at its first projection and values only change on
        // reselection.
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("19a37b52-f7c2-47c8-b32c-915f34a9bc21");
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, ModeState("host-01", generation, 1)));
        viewModel.SelectRuntimeHost(profile.ProfileId);

        EndpointInventoryItemViewModel retained = Assert.Single(viewModel.Endpoints);

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, ModeState("host-01", generation, 2))]));

        EndpointInventoryItemViewModel refreshed = Assert.Single(viewModel.Endpoints);

        Assert.NotSame(retained, refreshed);
        Assert.NotEqual(retained, refreshed);
    }

    [Fact]
    public void EndpointItem_AfterRefresh_ShouldStillCarryTheSelectionFlag()
    {
        // The tile draws its selection from this flag, so it must be re-applied
        // to every rebuilt projection or the indication disappears.
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("19a37b52-f7c2-47c8-b32c-915f34a9bc21");
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, ModeState("host-01", generation, 1)));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        viewModel.SelectedEndpoint = Assert.Single(viewModel.Endpoints);

        Assert.True(Assert.Single(viewModel.Endpoints).IsSelected);

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, ModeState("host-01", generation, 2))]));

        Assert.True(Assert.Single(viewModel.Endpoints).IsSelected);

        viewModel.ApplyMultiHostSnapshot(new MultiHostClientSessionSnapshot([
            Session(profile, RuntimeHostClientSessionState.Connected, ModeState("host-01", generation, 3))]));

        Assert.True(Assert.Single(viewModel.Endpoints).IsSelected);
    }

    [Fact]
    public void EndpointItem_WithoutSelection_ShouldNotCarryTheSelectionFlag()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Guid generation = Guid.Parse("19a37b52-f7c2-47c8-b32c-915f34a9bc21");
        MainWindowViewModel viewModel = Create(
            profile,
            Session(profile, RuntimeHostClientSessionState.Connected, ModeState("host-01", generation, 1)));
        viewModel.SelectRuntimeHost(profile.ProfileId);

        Assert.False(Assert.Single(viewModel.Endpoints).IsSelected);
    }
}

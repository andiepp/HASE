using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Commands;
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
            new CommandDescriptor(DescriptorPath.Parse("Mode.SelectShortCircuit"), "Select SHORT")
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
}

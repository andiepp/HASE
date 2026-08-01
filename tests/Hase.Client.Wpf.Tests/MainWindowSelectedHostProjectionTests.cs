using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowSelectedHostProjectionTests
{
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
    public void ConnectedSelection_ShouldApplyAuthoritativeState()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        MainWindowViewModel viewModel = Create(profile, Session(profile, RuntimeHostClientSessionState.Connected, State("host-01")));
        viewModel.SelectRuntimeHost(profile.ProfileId);
        Assert.True(viewModel.CurrentState.IsInitialized);
        Assert.Null(viewModel.PropertyReadMessage);
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
}

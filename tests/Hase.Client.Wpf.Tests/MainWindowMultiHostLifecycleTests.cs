using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class MainWindowMultiHostLifecycleTests
{
    [Fact]
    public async Task ConnectSelectedHost_ShouldTargetExactProfile()
    {
        RuntimeHostProfile profile = CreateProfile(true);
        var coordinator = new FakeCoordinator(CreateSnapshot(profile));
        var viewModel = CreateViewModel(profile, coordinator);
        viewModel.SelectRuntimeHost(profile.ProfileId);

        await viewModel.ConnectSelectedRuntimeHostAsync();

        Assert.Equal(profile.ProfileId, coordinator.ConnectedProfileId);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task DisconnectSelectedHost_ShouldTargetExactProfile()
    {
        RuntimeHostProfile profile = CreateProfile(true);
        var coordinator = new FakeCoordinator(CreateSnapshot(profile));
        var viewModel = CreateViewModel(profile, coordinator);
        viewModel.SelectRuntimeHost(profile.ProfileId);

        await viewModel.DisconnectSelectedRuntimeHostAsync();

        Assert.Equal(profile.ProfileId, coordinator.DisconnectedProfileId);
    }

    [Fact]
    public void DisabledSelectedHost_ShouldNotAllowConnect()
    {
        RuntimeHostProfile profile = CreateProfile(false);
        var coordinator = new FakeCoordinator(CreateSnapshot(profile));
        var viewModel = CreateViewModel(profile, coordinator);
        viewModel.SelectRuntimeHost(profile.ProfileId);
        Assert.False(viewModel.ConnectSelectedRuntimeHostCommand.CanExecute());
    }

    [Fact]
    public void NoSelection_ShouldDisableLifecycleCommands()
    {
        RuntimeHostProfile profile = CreateProfile(true);
        var viewModel = CreateViewModel(profile, new FakeCoordinator(CreateSnapshot(profile)));
        Assert.False(viewModel.ConnectSelectedRuntimeHostCommand.CanExecute());
        Assert.False(viewModel.DisconnectSelectedRuntimeHostCommand.CanExecute());
    }

    [Fact]
    public void TransientNullSelection_ShouldPreserveActiveHostAndDisconnectCommand()
    {
        RuntimeHostProfile profile = CreateProfile(true);
        var coordinator = new FakeCoordinator(CreateSnapshot(profile));
        var viewModel = CreateViewModel(profile, coordinator);
        viewModel.SelectRuntimeHost(profile.ProfileId);
        viewModel.ApplyMultiHostSnapshot(
            new MultiHostClientSessionSnapshot(
                [new RuntimeHostProfileSessionSnapshot(
                    profile,
                    new RuntimeHostClientSessionStatus(
                        RuntimeHostClientSessionState.Connecting),
                    DateTimeOffset.UtcNow)]));

        viewModel.SelectedRuntimeHost = null;

        Assert.Equal(profile.ProfileId, viewModel.SelectedRuntimeHost!.ProfileId);
        Assert.True(viewModel.DisconnectSelectedRuntimeHostCommand.CanExecute());
    }

    [Fact]
    public void RuntimeHostTileCommand_DisconnectedHost_ShouldSelectAndConnect()
    {
        RuntimeHostProfile profile = CreateProfile(true);
        var coordinator = new FakeCoordinator(CreateSnapshot(profile));
        var viewModel = CreateViewModel(profile, coordinator);
        RuntimeHostProfileItemViewModel host = Assert.Single(viewModel.RuntimeHosts);

        viewModel.ToggleRuntimeHostConnectionCommand.Execute(host);

        Assert.Equal(profile.ProfileId, viewModel.SelectedRuntimeHost!.ProfileId);
        Assert.Equal(profile.ProfileId, coordinator.ConnectedProfileId);
    }

    [Fact]
    public void RuntimeHostTileCommand_ConnectedHost_ShouldSelectAndDisconnect()
    {
        RuntimeHostProfile profile = CreateProfile(true);
        var coordinator = new FakeCoordinator(CreateSnapshot(profile, RuntimeHostClientSessionState.Connected));
        var viewModel = CreateViewModel(profile, coordinator);
        RuntimeHostProfileItemViewModel host = Assert.Single(viewModel.RuntimeHosts);

        viewModel.ToggleRuntimeHostConnectionCommand.Execute(host);

        Assert.Equal(profile.ProfileId, viewModel.SelectedRuntimeHost!.ProfileId);
        Assert.Equal(profile.ProfileId, coordinator.DisconnectedProfileId);
    }

    [Fact]
    public async Task ConnectionFailure_ShouldRestoreBusyAndExposeSafeMessage()
    {
        RuntimeHostProfile profile = CreateProfile(true);
        var coordinator = new FakeCoordinator(CreateSnapshot(profile)) { ConnectFailure = new InvalidOperationException() };
        var viewModel = CreateViewModel(profile, coordinator);
        viewModel.SelectRuntimeHost(profile.ProfileId);
        await viewModel.ConnectSelectedRuntimeHostAsync();
        Assert.False(viewModel.IsBusy);
        Assert.Equal("The selected runtime-host connection could not be started.", viewModel.FailureMessage);
    }

    private static MainWindowViewModel CreateViewModel(RuntimeHostProfile profile, FakeCoordinator coordinator)
    {
        var viewModel = new MainWindowViewModel();
        viewModel.ConfigureRuntimeHosts(new RuntimeHostProfileRegistry([profile]));
        viewModel.ConfigureMultiHostCoordinator(coordinator);
        return viewModel;
    }

    private static RuntimeHostProfile CreateProfile(bool enabled) =>
        new(new RuntimeHostProfileId("first"), "First", new RemoteRuntimeHostId("host-01"), enabled);
    private static MultiHostClientSessionSnapshot CreateSnapshot(
        RuntimeHostProfile profile,
        RuntimeHostClientSessionState state = RuntimeHostClientSessionState.Disconnected) =>
        new([new RuntimeHostProfileSessionSnapshot(profile,
            state == RuntimeHostClientSessionState.Connected
                ? new RuntimeHostClientSessionStatus(
                    state, profile.ExpectedRuntimeHostId, RuntimeHostClientApiVersion.Current)
                : new RuntimeHostClientSessionStatus(state),
            DateTimeOffset.UtcNow,
            state == RuntimeHostClientSessionState.Connected
                ? new RemoteObservationReducer().Initialize(
                    RemoteObservationState.Empty,
                    new RemoteObservationInitialSnapshot(
                        new RemoteRuntimeHostSnapshot(
                            profile.ExpectedRuntimeHostId,
                            RuntimeHostClientApiVersion.Current,
                            []),
                        new RemoteObservationSequence(0)))
                : null)]);

    private sealed class FakeCoordinator(MultiHostClientSessionSnapshot snapshot) : IMultiHostClientSessionCoordinator
    {
        public event EventHandler? SnapshotChanged;
        public event EventHandler<RuntimeHostProfileEventOccurredEventArgs>? EventOccurred;
        public MultiHostClientSessionSnapshot Snapshot { get; } = snapshot;
        public RuntimeHostProfileId? ConnectedProfileId { get; private set; }
        public RuntimeHostProfileId? DisconnectedProfileId { get; private set; }
        public Exception? ConnectFailure { get; init; }
        public Task ConnectAsync(RuntimeHostProfileId profileId, CancellationToken cancellationToken = default)
        { ConnectedProfileId = profileId; return ConnectFailure is null ? Task.CompletedTask : Task.FromException(ConnectFailure); }
        public Task DisconnectAsync(RuntimeHostProfileId profileId)
        { DisconnectedProfileId = profileId; return Task.CompletedTask; }
        public Task<RemotePropertyOperationResult> ReadPropertyAsync(RemoteRuntimeHostPropertyTarget target, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RemotePropertyOperationResult> WritePropertyAsync(RemoteRuntimeHostPropertyTarget target, RemoteValue requestedValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RemoteCommandOperationResult> ExecuteCommandAsync(RemoteRuntimeHostCommandExecutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

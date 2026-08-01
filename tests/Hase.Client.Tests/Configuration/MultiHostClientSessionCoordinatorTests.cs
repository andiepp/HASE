using Hase.Client.Configuration;

namespace Hase.Client.Tests.Configuration;

public sealed class MultiHostClientSessionCoordinatorTests
{
    [Fact]
    public void Constructor_EmptyRegistry_ShouldCreateEmptySnapshot()
    {
        var coordinator = new MultiHostClientSessionCoordinator(
            new RuntimeHostProfileRegistry([]),
            new FakeFactory());

        Assert.Empty(coordinator.Snapshot.Sessions);
    }

    [Fact]
    public void Constructor_ShouldPreserveRegistryOrderAndSkipDisabledControllerCreation()
    {
        RuntimeHostProfile first = CreateProfile("first", "host-01");
        RuntimeHostProfile disabled = CreateProfile("disabled", "host-02", false);
        RuntimeHostProfile third = CreateProfile("third", "host-03");
        var factory = new FakeFactory();

        var coordinator = new MultiHostClientSessionCoordinator(
            new RuntimeHostProfileRegistry([first, disabled, third]),
            factory);

        Assert.Equal(
            new[] { "first", "disabled", "third" },
            coordinator.Snapshot.Sessions.Select(session => session.ProfileId.Value));
        Assert.Equal(new[] { first, third }, factory.RequestedProfiles);
        Assert.Equal(
            RuntimeHostClientSessionState.Disconnected,
            coordinator.Snapshot.Sessions[1].Status.State);
    }

    [Fact]
    public void Constructor_NullFactoryResult_ShouldThrow()
    {
        var factory = new FakeFactory { ReturnNull = true };

        Assert.Throws<InvalidOperationException>(
            () => new MultiHostClientSessionCoordinator(
                new RuntimeHostProfileRegistry([CreateProfile("first", "host-01")]),
                factory));
    }

    [Fact]
    public void Constructor_MismatchedControllerProfile_ShouldThrow()
    {
        var factory = new FakeFactory
        {
            ControllerProfileOverride = CreateProfile("wrong", "host-02")
        };

        Assert.Throws<InvalidOperationException>(
            () => new MultiHostClientSessionCoordinator(
                new RuntimeHostProfileRegistry([CreateProfile("first", "host-01")]),
                factory));
    }

    [Fact]
    public async Task ConnectAsync_ShouldTargetOnlyRequestedProfile()
    {
        var factory = new FakeFactory();
        await using var coordinator = CreateTwoHostCoordinator(factory);

        await coordinator.ConnectAsync(new RuntimeHostProfileId("second"));

        Assert.Equal(0, factory.Controllers["first"].ConnectCount);
        Assert.Equal(1, factory.Controllers["second"].ConnectCount);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldTargetOnlyRequestedProfile()
    {
        var factory = new FakeFactory();
        await using var coordinator = CreateTwoHostCoordinator(factory);

        await coordinator.DisconnectAsync(new RuntimeHostProfileId("first"));

        Assert.Equal(1, factory.Controllers["first"].DisconnectCount);
        Assert.Equal(0, factory.Controllers["second"].DisconnectCount);
    }

    [Fact]
    public async Task UnknownProfileOperation_ShouldThrow()
    {
        await using var coordinator = CreateTwoHostCoordinator(new FakeFactory());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => coordinator.ConnectAsync(new RuntimeHostProfileId("missing")));
    }

    [Fact]
    public async Task DisabledProfileOperation_ShouldThrow()
    {
        var coordinator = new MultiHostClientSessionCoordinator(
            new RuntimeHostProfileRegistry([CreateProfile("disabled", "host-01", false)]),
            new FakeFactory());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.ConnectAsync(new RuntimeHostProfileId("disabled")));
    }

    [Fact]
    public void ChildSnapshotChange_ShouldRebuildAggregateAndNotify()
    {
        var factory = new FakeFactory();
        var coordinator = CreateTwoHostCoordinator(factory);
        int notifications = 0;
        coordinator.SnapshotChanged += (_, _) => notifications++;

        factory.Controllers["second"].PublishFault();

        Assert.Equal(1, notifications);
        Assert.Equal(
            RuntimeHostClientSessionState.Faulted,
            coordinator.Snapshot.Sessions[1].Status.State);
        Assert.Equal(
            RuntimeHostClientSessionState.Disconnected,
            coordinator.Snapshot.Sessions[0].Status.State);
    }

    [Fact]
    public void SeparateChildFaults_ShouldRemainIsolated()
    {
        var factory = new FakeFactory();
        var coordinator = CreateTwoHostCoordinator(factory);

        factory.Controllers["first"].PublishFault();

        Assert.Equal(RuntimeHostClientSessionState.Faulted, coordinator.Snapshot.Sessions[0].Status.State);
        Assert.Equal(RuntimeHostClientSessionState.Disconnected, coordinator.Snapshot.Sessions[1].Status.State);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeEveryController()
    {
        var factory = new FakeFactory();
        var coordinator = CreateTwoHostCoordinator(factory);

        await coordinator.DisposeAsync();

        Assert.All(factory.Controllers.Values, controller => Assert.Equal(1, controller.DisposeCount));
    }

    [Fact]
    public async Task DisposeAsync_OneFailure_ShouldAttemptEveryController()
    {
        var factory = new FakeFactory();
        var coordinator = CreateTwoHostCoordinator(factory);
        factory.Controllers["first"].DisposeFailure = new InvalidOperationException("first failed");

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            async () => await coordinator.DisposeAsync());

        Assert.Single(exception.InnerExceptions);
        Assert.Equal(1, factory.Controllers["second"].DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_RepeatedCall_ShouldBeSafe()
    {
        var factory = new FakeFactory();
        var coordinator = CreateTwoHostCoordinator(factory);

        await coordinator.DisposeAsync();
        await coordinator.DisposeAsync();

        Assert.All(factory.Controllers.Values, controller => Assert.Equal(1, controller.DisposeCount));
    }

    [Fact]
    public async Task OperationAfterDisposal_ShouldThrow()
    {
        var coordinator = CreateTwoHostCoordinator(new FakeFactory());
        await coordinator.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => coordinator.ConnectAsync(new RuntimeHostProfileId("first")));
    }

    private static MultiHostClientSessionCoordinator CreateTwoHostCoordinator(FakeFactory factory) =>
        new(
            new RuntimeHostProfileRegistry(
                [CreateProfile("first", "host-01"), CreateProfile("second", "host-02")]),
            factory);

    private static RuntimeHostProfile CreateProfile(string id, string host, bool enabled = true) =>
        new(new RuntimeHostProfileId(id), id, new RemoteRuntimeHostId(host), enabled);

    private sealed class FakeFactory : IRuntimeHostProfileSessionControllerFactory
    {
        public bool ReturnNull { get; init; }
        public RuntimeHostProfile? ControllerProfileOverride { get; init; }
        public List<RuntimeHostProfile> RequestedProfiles { get; } = [];
        public Dictionary<string, FakeController> Controllers { get; } = [];

        public IRuntimeHostProfileSessionController Create(RuntimeHostProfile profile)
        {
            RequestedProfiles.Add(profile);
            if (ReturnNull) return null!;
            var controller = new FakeController(ControllerProfileOverride ?? profile);
            Controllers.Add(profile.ProfileId.Value, controller);
            return controller;
        }
    }

    private sealed class FakeController : IRuntimeHostProfileSessionController
    {
        private readonly RuntimeHostProfile profile;

        public FakeController(RuntimeHostProfile profile)
        {
            this.profile = profile;
            Snapshot = CreateSnapshot(RuntimeHostClientSessionState.Disconnected);
        }

        public event EventHandler? SnapshotChanged;
        public event EventHandler<RuntimeHostProfileEventOccurredEventArgs>? EventOccurred;
        public RuntimeHostProfileSessionSnapshot Snapshot { get; private set; }
        public int ConnectCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public Exception? DisposeFailure { get; set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        { ConnectCount++; return Task.CompletedTask; }

        public Task DisconnectAsync()
        { DisconnectCount++; return Task.CompletedTask; }

        public Task<RemotePropertyOperationResult> ReadPropertyAsync(RemotePropertyTarget target, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RemotePropertyOperationResult> WritePropertyAsync(RemotePropertyTarget target, RemoteValue requestedValue, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RemoteCommandOperationResult> ExecuteCommandAsync(RemoteCommandExecutionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return DisposeFailure is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(DisposeFailure);
        }

        public void PublishFault()
        {
            Snapshot = new RuntimeHostProfileSessionSnapshot(
                profile,
                new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Faulted),
                DateTimeOffset.UtcNow,
                failure: new RuntimeHostClientFailureSnapshot(
                    RuntimeHostClientFailureCategory.Unknown,
                    "fault"));
            SnapshotChanged?.Invoke(this, EventArgs.Empty);
        }

        private RuntimeHostProfileSessionSnapshot CreateSnapshot(RuntimeHostClientSessionState state) =>
            new(profile, new RuntimeHostClientSessionStatus(state), DateTimeOffset.UtcNow);
    }
}

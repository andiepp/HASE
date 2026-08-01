using Hase.Client.Configuration;

namespace Hase.Client.Tests.Configuration;

public sealed class RuntimeHostProfileSessionSnapshotTests
{
    private static readonly DateTimeOffset ChangedAtUtc =
        new(2026, 8, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_Disconnected_ShouldPreserveProfileIdentity()
    {
        RuntimeHostProfile profile = CreateProfile("laboratory", "host-01");
        var snapshot = new RuntimeHostProfileSessionSnapshot(
            profile,
            new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Disconnected),
            ChangedAtUtc);

        Assert.Same(profile, snapshot.Profile);
        Assert.Same(profile.ProfileId, snapshot.ProfileId);
        Assert.Equal(ChangedAtUtc, snapshot.ChangedAtUtc);
        Assert.Null(snapshot.CurrentState);
        Assert.Null(snapshot.Failure);
    }

    [Fact]
    public void Constructor_ConnectedMatchingState_ShouldSucceed()
    {
        RuntimeHostProfile profile = CreateProfile("laboratory", "host-01");
        RuntimeHostClientApiVersion version = new(1, 0);
        RemoteObservationState state = CreateState("host-01", version);

        var snapshot = new RuntimeHostProfileSessionSnapshot(
            profile,
            new RuntimeHostClientSessionStatus(
                RuntimeHostClientSessionState.Connected,
                profile.ExpectedRuntimeHostId,
                version),
            ChangedAtUtc,
            state);

        Assert.Same(state, snapshot.CurrentState);
    }

    [Fact]
    public void Constructor_ConnectedWithoutInitializedState_ShouldThrow()
    {
        RuntimeHostProfile profile = CreateProfile("laboratory", "host-01");

        Assert.Throws<ArgumentException>(
            "currentState",
            () => new RuntimeHostProfileSessionSnapshot(
                profile,
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Connected,
                    profile.ExpectedRuntimeHostId,
                    new RuntimeHostClientApiVersion(1, 0)),
                ChangedAtUtc,
                RemoteObservationState.Empty));
    }

    [Fact]
    public void Constructor_MismatchedHostState_ShouldThrow()
    {
        RuntimeHostProfile profile = CreateProfile("laboratory", "host-01");

        Assert.Throws<ArgumentException>(
            "currentState",
            () => new RuntimeHostProfileSessionSnapshot(
                profile,
                new RuntimeHostClientSessionStatus(
                    RuntimeHostClientSessionState.Connected,
                    profile.ExpectedRuntimeHostId,
                    new RuntimeHostClientApiVersion(1, 0)),
                ChangedAtUtc,
                CreateState("host-02", new RuntimeHostClientApiVersion(1, 0))));
    }

    [Fact]
    public void Constructor_FaultedWithFailure_ShouldSucceed()
    {
        var failure = new RuntimeHostClientFailureSnapshot(
            RuntimeHostClientFailureCategory.Authentication,
            "rejected");

        var snapshot = new RuntimeHostProfileSessionSnapshot(
            CreateProfile("laboratory", "host-01"),
            new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Faulted),
            ChangedAtUtc,
            failure: failure);

        Assert.Same(failure, snapshot.Failure);
    }

    [Fact]
    public void Constructor_FaultedWithoutFailure_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "failure",
            () => new RuntimeHostProfileSessionSnapshot(
                CreateProfile("laboratory", "host-01"),
                new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Faulted),
                ChangedAtUtc));
    }

    [Fact]
    public void Constructor_NonFaultedWithFailure_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "failure",
            () => new RuntimeHostProfileSessionSnapshot(
                CreateProfile("laboratory", "host-01"),
                new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Disconnected),
                ChangedAtUtc,
                failure: new RuntimeHostClientFailureSnapshot(
                    RuntimeHostClientFailureCategory.Unknown,
                    "failure")));
    }

    [Fact]
    public void Constructor_NonUtcTimestamp_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "changedAtUtc",
            () => new RuntimeHostProfileSessionSnapshot(
                CreateProfile("laboratory", "host-01"),
                new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Disconnected),
                new DateTimeOffset(2026, 8, 1, 20, 0, 0, TimeSpan.FromHours(2))));
    }

    private static RuntimeHostProfile CreateProfile(string profileId, string hostId) =>
        new(
            new RuntimeHostProfileId(profileId),
            profileId,
            new RemoteRuntimeHostId(hostId));

    private static RemoteObservationState CreateState(
        string hostId,
        RuntimeHostClientApiVersion version) =>
        new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(
                    new RemoteRuntimeHostId(hostId),
                    version,
                    []),
                new RemoteObservationSequence(0)));
}

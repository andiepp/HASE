using Hase.Client.Configuration;

namespace Hase.Client.Tests.Configuration;

public sealed class MultiHostClientSessionSnapshotTests
{
    [Fact]
    public void Constructor_Sessions_ShouldPreserveOrderAndSnapshot()
    {
        RuntimeHostProfileSessionSnapshot first = Create("first", "host-01");
        RuntimeHostProfileSessionSnapshot second = Create("second", "host-02");
        var source = new List<RuntimeHostProfileSessionSnapshot> { first, second };

        var snapshot = new MultiHostClientSessionSnapshot(source);
        source.Clear();

        Assert.Equal(new[] { first, second }, snapshot.Sessions);
        Assert.Throws<NotSupportedException>(
            () => ((IList<RuntimeHostProfileSessionSnapshot>)snapshot.Sessions).Add(first));
    }

    [Fact]
    public void Constructor_EmptySessions_ShouldSucceed()
    {
        Assert.Empty(new MultiHostClientSessionSnapshot([]).Sessions);
    }

    [Fact]
    public void Constructor_NullCollectionOrEntry_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "sessions",
            () => new MultiHostClientSessionSnapshot(null!));
        Assert.Throws<ArgumentException>(
            "sessions",
            () => new MultiHostClientSessionSnapshot([null!]));
    }

    [Fact]
    public void Constructor_DuplicateProfileIdentity_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "sessions",
            () => new MultiHostClientSessionSnapshot(
                [Create("same", "host-01"), Create("same", "host-02")]));
    }

    [Fact]
    public void TryGet_ExistingAndMissingProfile_ShouldReturnExpectedResults()
    {
        RuntimeHostProfileSessionSnapshot expected = Create("laboratory", "host-01");
        var snapshot = new MultiHostClientSessionSnapshot([expected]);

        Assert.True(snapshot.TryGet(new RuntimeHostProfileId("laboratory"), out var actual));
        Assert.Same(expected, actual);
        Assert.False(snapshot.TryGet(new RuntimeHostProfileId("missing"), out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void TryGet_NullProfileId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "profileId",
            () => new MultiHostClientSessionSnapshot([]).TryGet(null!, out _));
    }

    private static RuntimeHostProfileSessionSnapshot Create(
        string profileId,
        string hostId) =>
        new(
            new RuntimeHostProfile(
                new RuntimeHostProfileId(profileId),
                profileId,
                new RemoteRuntimeHostId(hostId)),
            new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Disconnected),
            new DateTimeOffset(2026, 8, 1, 18, 0, 0, TimeSpan.Zero));
}

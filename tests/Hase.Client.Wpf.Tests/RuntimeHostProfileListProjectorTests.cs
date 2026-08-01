using Hase.Client.Configuration;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class RuntimeHostProfileListProjectorTests
{
    [Fact]
    public void Project_ShouldPreserveRegistryOrderAndDisabledProfile()
    {
        RuntimeHostProfile first = Profile("first", "host-01");
        RuntimeHostProfile second = Profile("second", "host-02", false);
        var result = new RuntimeHostProfileListProjector().Project(
            new RuntimeHostProfileRegistry([first, second]),
            new MultiHostClientSessionSnapshot([Session(first), Session(second)]));

        Assert.Equal(new[] { "first", "second" }, result.Select(item => item.ProfileId.Value));
        Assert.False(result[1].IsEnabled);
    }

    [Fact]
    public void Project_SelectedProfile_ShouldRemainExplicit()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        var result = new RuntimeHostProfileListProjector().Project(
            new RuntimeHostProfileRegistry([profile]),
            new MultiHostClientSessionSnapshot([Session(profile)]),
            profile.ProfileId);
        Assert.True(Assert.Single(result).IsSelected);
    }

    [Fact]
    public void Project_Fault_ShouldExposeOnlyNormalizedFailure()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        var session = new RuntimeHostProfileSessionSnapshot(
            profile,
            new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Faulted),
            DateTimeOffset.UtcNow,
            failure: new RuntimeHostClientFailureSnapshot(RuntimeHostClientFailureCategory.Authentication, "rejected"));
        RuntimeHostProfileItemViewModel item = Assert.Single(new RuntimeHostProfileListProjector().Project(
            new RuntimeHostProfileRegistry([profile]), new MultiHostClientSessionSnapshot([session])));
        Assert.Equal(RuntimeHostClientFailureCategory.Authentication, item.FailureCategory);
        Assert.Equal("rejected", item.FailureMessage);
    }

    [Fact]
    public void Project_MissingOrExtraSession_ShouldThrow()
    {
        RuntimeHostProfile first = Profile("first", "host-01");
        RuntimeHostProfile extra = Profile("extra", "host-02");
        var projector = new RuntimeHostProfileListProjector();
        Assert.Throws<ArgumentException>("snapshot", () => projector.Project(
            new RuntimeHostProfileRegistry([first]), new MultiHostClientSessionSnapshot([])));
        Assert.Throws<ArgumentException>("snapshot", () => projector.Project(
            new RuntimeHostProfileRegistry([first]), new MultiHostClientSessionSnapshot([Session(extra)])));
    }

    [Fact]
    public void Project_UnknownSelection_ShouldThrow()
    {
        RuntimeHostProfile profile = Profile("first", "host-01");
        Assert.Throws<ArgumentException>("selectedProfileId", () => new RuntimeHostProfileListProjector().Project(
            new RuntimeHostProfileRegistry([profile]),
            new MultiHostClientSessionSnapshot([Session(profile)]),
            new RuntimeHostProfileId("missing")));
    }

    private static RuntimeHostProfile Profile(string id, string host, bool enabled = true) =>
        new(new RuntimeHostProfileId(id), id, new RemoteRuntimeHostId(host), enabled);
    private static RuntimeHostProfileSessionSnapshot Session(RuntimeHostProfile profile) =>
        new(profile, new RuntimeHostClientSessionStatus(RuntimeHostClientSessionState.Disconnected), DateTimeOffset.UtcNow);
}

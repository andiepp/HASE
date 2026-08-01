using Hase.Client.Configuration;

namespace Hase.Client.Tests.Configuration;

public sealed class RuntimeHostProfileTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveImmutableProfile()
    {
        var profileId =
            new RuntimeHostProfileId(
                "laboratory");
        var runtimeHostId =
            new RemoteRuntimeHostId(
                "host-01");

        var profile =
            new RuntimeHostProfile(
                profileId,
                "  Laboratory Desktop  ",
                runtimeHostId,
                isEnabled: false);

        Assert.Same(
            profileId,
            profile.ProfileId);
        Assert.Equal(
            "Laboratory Desktop",
            profile.DisplayName);
        Assert.Same(
            runtimeHostId,
            profile.ExpectedRuntimeHostId);
        Assert.False(
            profile.IsEnabled);
    }

    [Fact]
    public void Constructor_Default_ShouldEnableProfile()
    {
        RuntimeHostProfile profile =
            CreateProfile();

        Assert.True(
            profile.IsEnabled);
    }

    [Fact]
    public void Constructor_NullProfileId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "profileId",
            () => new RuntimeHostProfile(
                null!,
                "Laboratory",
                new RemoteRuntimeHostId(
                    "host-01")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_MissingDisplayName_ShouldThrow(
        string? displayName)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new RuntimeHostProfile(
                new RuntimeHostProfileId(
                    "laboratory"),
                displayName!,
                new RemoteRuntimeHostId(
                    "host-01")));
    }

    [Fact]
    public void Constructor_OverMaximumDisplayNameLength_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "displayName",
            () => new RuntimeHostProfile(
                new RuntimeHostProfileId(
                    "laboratory"),
                new string(
                    'a',
                    RuntimeHostProfile.MaximumDisplayNameLength + 1),
                new RemoteRuntimeHostId(
                    "host-01")));
    }

    [Fact]
    public void Constructor_NullExpectedRuntimeHostId_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "expectedRuntimeHostId",
            () => new RuntimeHostProfile(
                new RuntimeHostProfileId(
                    "laboratory"),
                "Laboratory",
                null!));
    }

    private static RuntimeHostProfile CreateProfile() =>
        new(
            new RuntimeHostProfileId(
                "laboratory"),
            "Laboratory",
            new RemoteRuntimeHostId(
                "host-01"));
}

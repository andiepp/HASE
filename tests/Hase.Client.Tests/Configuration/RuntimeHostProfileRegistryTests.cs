using Hase.Client.Configuration;

namespace Hase.Client.Tests.Configuration;

public sealed class RuntimeHostProfileRegistryTests
{
    [Fact]
    public void Constructor_Profiles_ShouldPreserveOrderAndSnapshot()
    {
        var first =
            CreateProfile(
                "first",
                "host-01");
        var second =
            CreateProfile(
                "second",
                "host-02");
        var source =
            new List<RuntimeHostProfile>
            {
                first,
                second
            };

        var registry =
            new RuntimeHostProfileRegistry(
                source);
        source.Clear();

        Assert.Equal(
            new[]
            {
                first,
                second
            },
            registry.Profiles);
        Assert.Throws<NotSupportedException>(
            () =>
                ((IList<RuntimeHostProfile>)registry.Profiles)
                    .Add(
                        first));
    }

    [Fact]
    public void Constructor_EmptyProfiles_ShouldSucceed()
    {
        var registry =
            new RuntimeHostProfileRegistry(
                []);

        Assert.Empty(
            registry.Profiles);
    }

    [Fact]
    public void Constructor_NullProfiles_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "profiles",
            () => new RuntimeHostProfileRegistry(
                null!));
    }

    [Fact]
    public void Constructor_NullProfile_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "profiles",
            () => new RuntimeHostProfileRegistry(
                [null!]));
    }

    [Fact]
    public void Constructor_DuplicateProfileId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "profiles",
            () => new RuntimeHostProfileRegistry(
                [
                    CreateProfile(
                        "laboratory",
                        "host-01"),
                    CreateProfile(
                        "laboratory",
                        "host-02")
                ]));
    }

    [Fact]
    public void Constructor_DuplicateEnabledRuntimeHostId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "profiles",
            () => new RuntimeHostProfileRegistry(
                [
                    CreateProfile(
                        "first",
                        "host-01"),
                    CreateProfile(
                        "second",
                        "host-01")
                ]));
    }

    [Fact]
    public void Constructor_DuplicateRuntimeHostIdWithDisabledProfile_ShouldSucceed()
    {
        var registry =
            new RuntimeHostProfileRegistry(
                [
                    CreateProfile(
                        "first",
                        "host-01"),
                    CreateProfile(
                        "second",
                        "host-01",
                        isEnabled: false)
                ]);

        Assert.Equal(
            2,
            registry.Profiles.Count);
    }

    [Fact]
    public void Constructor_OverMaximumProfileCount_ShouldThrow()
    {
        RuntimeHostProfile[] profiles =
            Enumerable.Range(
                    0,
                    RuntimeHostProfileRegistry.MaximumProfileCount + 1)
                .Select(
                    index =>
                        CreateProfile(
                            $"profile-{index}",
                            $"host-{index}"))
                .ToArray();

        Assert.Throws<ArgumentException>(
            "profiles",
            () => new RuntimeHostProfileRegistry(
                profiles));
    }

    [Fact]
    public void TryGet_ExistingProfile_ShouldReturnExactProfile()
    {
        RuntimeHostProfile expected =
            CreateProfile(
                "laboratory",
                "host-01");
        var registry =
            new RuntimeHostProfileRegistry(
                [expected]);

        bool found =
            registry.TryGet(
                new RuntimeHostProfileId(
                    "laboratory"),
                out RuntimeHostProfile? actual);

        Assert.True(
            found);
        Assert.Same(
            expected,
            actual);
    }

    [Fact]
    public void TryGet_MissingProfile_ShouldReturnFalse()
    {
        var registry =
            new RuntimeHostProfileRegistry(
                []);

        bool found =
            registry.TryGet(
                new RuntimeHostProfileId(
                    "missing"),
                out RuntimeHostProfile? profile);

        Assert.False(
            found);
        Assert.Null(
            profile);
    }

    [Fact]
    public void TryGet_NullProfileId_ShouldThrow()
    {
        var registry =
            new RuntimeHostProfileRegistry(
                []);

        Assert.Throws<ArgumentNullException>(
            "profileId",
            () => registry.TryGet(
                null!,
                out _));
    }

    private static RuntimeHostProfile CreateProfile(
        string profileId,
        string runtimeHostId,
        bool isEnabled = true) =>
        new(
            new RuntimeHostProfileId(
                profileId),
            profileId,
            new RemoteRuntimeHostId(
                runtimeHostId),
            isEnabled);
}

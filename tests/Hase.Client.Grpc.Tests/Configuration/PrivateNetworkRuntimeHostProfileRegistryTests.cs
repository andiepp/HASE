using Hase.Client;
using Hase.Client.Configuration;
using Hase.Client.Grpc.Configuration;

namespace Hase.Client.Grpc.Tests.Configuration;

public sealed class PrivateNetworkRuntimeHostProfileRegistryTests
{
    [Fact]
    public void Constructor_Profiles_ShouldPreserveOrderAndCoreRegistry()
    {
        PrivateNetworkRuntimeHostProfile first =
            CreateProfile(
                "first",
                "host-01");
        PrivateNetworkRuntimeHostProfile second =
            CreateProfile(
                "second",
                "host-02");

        var registry =
            new PrivateNetworkRuntimeHostProfileRegistry(
                [
                    first,
                    second
                ]);

        Assert.Equal(
            new[]
            {
                first,
                second
            },
            registry.Profiles);
        Assert.Equal(
            new[]
            {
                first.Profile,
                second.Profile
            },
            registry.CoreProfiles.Profiles);
    }

    [Fact]
    public void Constructor_DuplicateProfileId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "profiles",
            () => new PrivateNetworkRuntimeHostProfileRegistry(
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
            () => new PrivateNetworkRuntimeHostProfileRegistry(
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
    public void Constructor_NullProfile_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "profiles",
            () => new PrivateNetworkRuntimeHostProfileRegistry(
                [null!]));
    }

    [Fact]
    public void TryGet_ExistingAndMissingProfiles_ShouldBeExplicit()
    {
        PrivateNetworkRuntimeHostProfile expected =
            CreateProfile(
                "laboratory",
                "host-01");
        var registry =
            new PrivateNetworkRuntimeHostProfileRegistry(
                [expected]);

        Assert.True(
            registry.TryGet(
                new RuntimeHostProfileId(
                    "laboratory"),
                out PrivateNetworkRuntimeHostProfile? actual));
        Assert.Same(
            expected,
            actual);
        Assert.False(
            registry.TryGet(
                new RuntimeHostProfileId(
                    "missing"),
                out PrivateNetworkRuntimeHostProfile? missing));
        Assert.Null(
            missing);
    }

    private static PrivateNetworkRuntimeHostProfile CreateProfile(
        string profileId,
        string runtimeHostId) =>
        new(
            new RuntimeHostProfile(
                new RuntimeHostProfileId(
                    profileId),
                profileId,
                new RemoteRuntimeHostId(
                    runtimeHostId)),
            Path.Combine(
                Path.GetTempPath(),
                $"{profileId}.json"));
}

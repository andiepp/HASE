using Hase.Client;
using Hase.Client.Configuration;
using Hase.Client.Grpc.Configuration;

namespace Hase.Client.Grpc.Tests.Configuration;

public sealed class PrivateNetworkRuntimeHostProfileTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveProfileAndNormalizePath()
    {
        RuntimeHostProfile profile =
            CreateProfile();
        string path =
            Path.Combine(
                Path.GetTempPath(),
                ".",
                "client.json");

        var deployment =
            new PrivateNetworkRuntimeHostProfile(
                profile,
                path);

        Assert.Same(
            profile,
            deployment.Profile);
        Assert.Equal(
            Path.GetFullPath(
                path),
            deployment.PrivateNetworkConfigurationFilePath);
    }

    [Fact]
    public void Constructor_ShouldNotRequireExistingFile()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString(
                    "N"),
                "missing.json");

        var deployment =
            new PrivateNetworkRuntimeHostProfile(
                CreateProfile(),
                path);

        Assert.Equal(
            Path.GetFullPath(
                path),
            deployment.PrivateNetworkConfigurationFilePath);
    }

    [Fact]
    public void Constructor_NullProfile_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "profile",
            () => new PrivateNetworkRuntimeHostProfile(
                null!,
                Path.Combine(
                    Path.GetTempPath(),
                    "client.json")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("client.json")]
    public void Constructor_InvalidPath_ShouldThrow(
        string? path)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new PrivateNetworkRuntimeHostProfile(
                CreateProfile(),
                path!));
    }

    [Fact]
    public void ToString_ShouldNotRevealConfigurationPath()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "private-client.json");
        var deployment =
            new PrivateNetworkRuntimeHostProfile(
                CreateProfile(),
                path);

        string text =
            deployment.ToString();

        Assert.Equal(
            "laboratory",
            text);
        Assert.DoesNotContain(
            path,
            text,
            StringComparison.Ordinal);
    }

    private static RuntimeHostProfile CreateProfile() =>
        new(
            new RuntimeHostProfileId(
                "laboratory"),
            "Laboratory",
            new RemoteRuntimeHostId(
                "host-01"));
}

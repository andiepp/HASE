using System.IO;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostInstallationProfileTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveNormalizedProfile()
    {
        string identityPath =
            Path.Combine(
                Path.GetTempPath(),
                ".",
                "runtime-host.id");
        string configurationPath =
            Path.Combine(
                Path.GetTempPath(),
                ".",
                "desktop-private-network.json");

        var profile =
            new DesktopRuntimeHostInstallationProfile(
                identityPath,
                configurationPath,
                RuntimeDiagnosticLevel.Bytes,
                includeByteBufferSimulation: true);

        Assert.Equal(
            Path.GetFullPath(
                identityPath),
            profile.IdentityFilePath);
        Assert.Equal(
            Path.GetFullPath(
                configurationPath),
            profile.PrivateNetworkConfigurationFilePath);
        Assert.Equal(
            RuntimeDiagnosticLevel.Bytes,
            profile.MaximumDiagnosticLevel);
        Assert.True(
            profile.IncludeByteBufferSimulation);
    }

    [Fact]
    public void Constructor_Defaults_ShouldBeOperationalWithoutSimulation()
    {
        DesktopRuntimeHostInstallationProfile profile =
            CreateProfile();

        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            profile.MaximumDiagnosticLevel);
        Assert.False(
            profile.IncludeByteBufferSimulation);
        Assert.False(profile.RemoteDiagnosticsEnabled);
        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            profile.RemoteDiagnosticsMaximumLevel);
    }

    [Fact]
    public void Constructor_EnabledRemoteDiagnosticsWithinLocalCeiling_ShouldPreserve()
    {
        var profile = new DesktopRuntimeHostInstallationProfile(
            AbsolutePath("runtime-host.id"),
            AbsolutePath("desktop-private-network.json"),
            RuntimeDiagnosticLevel.Bytes,
            includeByteBufferSimulation: false,
            remoteDiagnosticsEnabled: true,
            remoteDiagnosticsMaximumLevel: RuntimeDiagnosticLevel.Protocol,
            authorizationPolicyFilePath:
                AbsolutePath("runtime-host-authorization.json"));

        Assert.True(profile.RemoteDiagnosticsEnabled);
        Assert.Equal(
            RuntimeDiagnosticLevel.Protocol,
            profile.RemoteDiagnosticsMaximumLevel);
        Assert.Equal(
            AbsolutePath("runtime-host-authorization.json"),
            profile.AuthorizationPolicyFilePath);
    }

    [Fact]
    public void Constructor_EnabledRemoteDiagnosticsWithoutPolicy_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "authorizationPolicyFilePath",
            () => new DesktopRuntimeHostInstallationProfile(
                AbsolutePath("runtime-host.id"),
                AbsolutePath("desktop-private-network.json"),
                RuntimeDiagnosticLevel.Bytes,
                remoteDiagnosticsEnabled: true));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("runtime-host-authorization.json")]
    public void Constructor_InvalidAuthorizationPolicyPath_ShouldThrow(
        string path)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new DesktopRuntimeHostInstallationProfile(
                AbsolutePath("runtime-host.id"),
                AbsolutePath("desktop-private-network.json"),
                authorizationPolicyFilePath: path));
    }

    [Fact]
    public void Constructor_DuplicateAuthorizationPolicyPath_ShouldThrow()
    {
        string deploymentPath = AbsolutePath("desktop-private-network.json");

        Assert.Throws<ArgumentException>(
            "authorizationPolicyFilePath",
            () => new DesktopRuntimeHostInstallationProfile(
                AbsolutePath("runtime-host.id"),
                deploymentPath,
                authorizationPolicyFilePath: deploymentPath));
    }

    [Fact]
    public void Constructor_EnabledRemoteCeilingAboveLocal_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "remoteDiagnosticsMaximumLevel",
            () => new DesktopRuntimeHostInstallationProfile(
                AbsolutePath("runtime-host.id"),
                AbsolutePath("desktop-private-network.json"),
                RuntimeDiagnosticLevel.Operational,
                includeByteBufferSimulation: false,
                remoteDiagnosticsEnabled: true,
                remoteDiagnosticsMaximumLevel: RuntimeDiagnosticLevel.Protocol));
    }

    [Fact]
    public void Constructor_UndefinedRemoteDiagnosticLevel_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "remoteDiagnosticsMaximumLevel",
            () => new DesktopRuntimeHostInstallationProfile(
                AbsolutePath("runtime-host.id"),
                AbsolutePath("desktop-private-network.json"),
                RuntimeDiagnosticLevel.Bytes,
                includeByteBufferSimulation: false,
                remoteDiagnosticsEnabled: false,
                remoteDiagnosticsMaximumLevel: (RuntimeDiagnosticLevel)999));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("runtime-host.id")]
    public void Constructor_InvalidIdentityPath_ShouldThrow(
        string? path)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new DesktopRuntimeHostInstallationProfile(
                path!,
                AbsolutePath(
                    "desktop-private-network.json")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("desktop-private-network.json")]
    public void Constructor_InvalidConfigurationPath_ShouldThrow(
        string? path)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new DesktopRuntimeHostInstallationProfile(
                AbsolutePath(
                    "runtime-host.id"),
                path!));
    }

    [Fact]
    public void Constructor_SamePath_ShouldThrow()
    {
        string path =
            AbsolutePath(
                "runtime-host.id");

        Assert.Throws<ArgumentException>(
            "privateNetworkConfigurationFilePath",
            () => new DesktopRuntimeHostInstallationProfile(
                path,
                path));
    }

    [Fact]
    public void Constructor_UndefinedDiagnosticLevel_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "maximumDiagnosticLevel",
            () => new DesktopRuntimeHostInstallationProfile(
                AbsolutePath(
                    "runtime-host.id"),
                AbsolutePath(
                    "desktop-private-network.json"),
                (RuntimeDiagnosticLevel)999));
    }

    [Fact]
    public void Constructor_ShouldNotRequireExistingFiles()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString(
                    "N"));

        var profile =
            new DesktopRuntimeHostInstallationProfile(
                Path.Combine(
                    directory,
                    "runtime-host.id"),
                Path.Combine(
                    directory,
                    "desktop-private-network.json"));

        Assert.Equal(
            Path.Combine(
                directory,
                "runtime-host.id"),
            profile.IdentityFilePath);
    }

    [Fact]
    public void ToString_ShouldNotRevealPaths()
    {
        var profile = new DesktopRuntimeHostInstallationProfile(
            AbsolutePath("runtime-host.id"),
            AbsolutePath("desktop-private-network.json"),
            authorizationPolicyFilePath:
                AbsolutePath("runtime-host-authorization.json"));

        string text =
            profile.ToString();

        Assert.Equal(
            "Desktop Runtime Host installation profile",
            text);
        Assert.DoesNotContain(
            profile.IdentityFilePath,
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            profile.PrivateNetworkConfigurationFilePath,
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            profile.AuthorizationPolicyFilePath!,
            text,
            StringComparison.Ordinal);
    }

    private static DesktopRuntimeHostInstallationProfile CreateProfile() =>
        new(
            AbsolutePath(
                "runtime-host.id"),
            AbsolutePath(
                "desktop-private-network.json"));

    private static string AbsolutePath(
        string fileName) =>
        Path.Combine(
            Path.GetTempPath(),
            fileName);
}

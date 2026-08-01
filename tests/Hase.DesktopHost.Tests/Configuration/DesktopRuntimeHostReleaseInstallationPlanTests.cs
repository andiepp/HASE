using System.IO;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostReleaseInstallationPlanTests
{
    [Fact]
    public void Constructor_ValidExternalDirectory_ShouldCreateSeparatedPlan()
    {
        string repositoryRoot = AbsolutePath("repository");
        string installationDirectory = AbsolutePath("installation");

        var plan = new DesktopRuntimeHostReleaseInstallationPlan(
            repositoryRoot,
            installationDirectory);

        Assert.Equal(Path.Combine(installationDirectory, "Application"), plan.ApplicationDirectory);
        Assert.Equal(Path.Combine(installationDirectory, "Configuration"), plan.ConfigurationDirectory);
        Assert.Equal(Path.Combine(installationDirectory, "Identity"), plan.IdentityDirectory);
        Assert.Equal(
            Path.Combine(installationDirectory, "Application", "Hase.DesktopHost.App.exe"),
            plan.ExecutableFilePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative-installation")]
    public void Constructor_InvalidInstallationDirectory_ShouldReject(string? installationDirectory)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new DesktopRuntimeHostReleaseInstallationPlan(
                AbsolutePath("repository"),
                installationDirectory!));
    }

    [Fact]
    public void Constructor_FilesystemRoot_ShouldReject()
    {
        string root = Path.GetPathRoot(Path.GetTempPath())!;

        Assert.Throws<ArgumentException>(
            "installationDirectory",
            () => new DesktopRuntimeHostReleaseInstallationPlan(
                AbsolutePath("repository"),
                root));
    }

    [Fact]
    public void Constructor_RepositoryRoot_ShouldReject()
    {
        string repositoryRoot = AbsolutePath("repository");

        Assert.Throws<ArgumentException>(
            "installationDirectory",
            () => new DesktopRuntimeHostReleaseInstallationPlan(
                repositoryRoot,
                repositoryRoot));
    }

    [Fact]
    public void Constructor_DirectoryInsideRepository_ShouldReject()
    {
        string repositoryRoot = AbsolutePath("repository");

        Assert.Throws<ArgumentException>(
            "installationDirectory",
            () => new DesktopRuntimeHostReleaseInstallationPlan(
                repositoryRoot,
                Path.Combine(repositoryRoot, "artifacts", "runtime-host")));
    }

    [Fact]
    public void ToString_ShouldNotRevealPaths()
    {
        var plan = new DesktopRuntimeHostReleaseInstallationPlan(
            AbsolutePath("repository"),
            AbsolutePath("installation"));

        Assert.Equal("Desktop Runtime Host Release installation plan", plan.ToString());
        Assert.DoesNotContain(plan.InstallationDirectory, plan.ToString(), StringComparison.Ordinal);
    }

    private static string AbsolutePath(string name) =>
        Path.Combine(Path.GetTempPath(), "hase-43b3", name);
}

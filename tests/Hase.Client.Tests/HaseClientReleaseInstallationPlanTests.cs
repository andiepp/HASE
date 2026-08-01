using System.IO;
using Hase.Client.Deployment;

namespace Hase.Client.Tests;

public sealed class HaseClientReleaseInstallationPlanTests
{
    [Fact]
    public void Constructor_ValidExternalDirectory_ShouldCreateSeparatedPlan()
    {
        string repositoryRoot = AbsolutePath("repository");
        string installationDirectory = AbsolutePath("installation");

        var plan = new HaseClientReleaseInstallationPlan(
            repositoryRoot,
            installationDirectory);

        Assert.Equal(Path.Combine(installationDirectory, "Application"), plan.ApplicationDirectory);
        Assert.Equal(Path.Combine(installationDirectory, "Configuration"), plan.ConfigurationDirectory);
        Assert.Equal(
            Path.Combine(installationDirectory, "Application", "Hase.Client.Wpf.App.exe"),
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
            () => new HaseClientReleaseInstallationPlan(
                AbsolutePath("repository"),
                installationDirectory!));
    }

    [Fact]
    public void Constructor_FilesystemRoot_ShouldReject()
    {
        string root = Path.GetPathRoot(Path.GetTempPath())!;

        Assert.Throws<ArgumentException>(
            "installationDirectory",
            () => new HaseClientReleaseInstallationPlan(
                AbsolutePath("repository"),
                root));
    }

    [Fact]
    public void Constructor_RepositoryRoot_ShouldReject()
    {
        string repositoryRoot = AbsolutePath("repository");

        Assert.Throws<ArgumentException>(
            "installationDirectory",
            () => new HaseClientReleaseInstallationPlan(
                repositoryRoot,
                repositoryRoot));
    }

    [Fact]
    public void Constructor_DirectoryInsideRepository_ShouldReject()
    {
        string repositoryRoot = AbsolutePath("repository");

        Assert.Throws<ArgumentException>(
            "installationDirectory",
            () => new HaseClientReleaseInstallationPlan(
                repositoryRoot,
                Path.Combine(repositoryRoot, "artifacts", "client")));
    }

    [Fact]
    public void ToString_ShouldNotRevealPaths()
    {
        var plan = new HaseClientReleaseInstallationPlan(
            AbsolutePath("repository"),
            AbsolutePath("installation"));

        Assert.Equal("HASE Client Release installation plan", plan.ToString());
        Assert.DoesNotContain(plan.InstallationDirectory, plan.ToString(), StringComparison.Ordinal);
    }

    private static string AbsolutePath(string name) =>
        Path.Combine(Path.GetTempPath(), "hase-43c1", name);
}

using System.IO;
using Hase.Client.Deployment;

namespace Hase.Client.Tests;

public sealed class HaseClientGuidedInstallationPlanTests
{
    [Fact]
    public void Constructor_ShouldCreateConfigurationCustodyAndShortcut()
    {
        string installation = AbsolutePath("installation");
        string desktop = AbsolutePath("desktop");
        var plan = new HaseClientGuidedInstallationPlan(
            installation,
            AbsolutePath("source-client.json"),
            desktop);

        Assert.Equal(Path.Combine(installation, "Application"), plan.ApplicationDirectory);
        Assert.Equal(Path.Combine(installation, "Configuration"), plan.ConfigurationDirectory);
        Assert.Equal(
            Path.Combine(installation, "Configuration", "laptop-private-network.json"),
            plan.ConfigurationFilePath);
        Assert.Equal(
            Path.Combine(installation, "Configuration", "client-runtime-hosts.json"),
            plan.RuntimeHostRegistryFilePath);
        Assert.Equal(Path.Combine(desktop, "HASE Client.lnk"), plan.ShortcutFilePath);
    }

    [Fact]
    public void Constructor_ShouldCreateExactSingleArgumentShortcut()
    {
        HaseClientGuidedInstallationPlan plan = CreatePlan();

        Assert.Equal("HASE Client", plan.Shortcut.Name);
        Assert.Equal(plan.ExecutableFilePath, plan.Shortcut.TargetFilePath);
        Assert.Equal($"\"{plan.RuntimeHostRegistryFilePath}\"", plan.Shortcut.Arguments);
        Assert.Equal(plan.ApplicationDirectory, plan.Shortcut.WorkingDirectory);
    }

    [Fact]
    public void RegistryAndPrivateNetworkConfiguration_ShouldHaveDistinctCustodyPaths()
    {
        HaseClientGuidedInstallationPlan plan = CreatePlan();

        Assert.NotEqual(plan.ConfigurationFilePath, plan.RuntimeHostRegistryFilePath);
        Assert.Equal(
            plan.ConfigurationDirectory,
            Path.GetDirectoryName(plan.RuntimeHostRegistryFilePath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative-client.json")]
    public void Constructor_InvalidConfigurationSource_ShouldReject(string? path)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new HaseClientGuidedInstallationPlan(
                AbsolutePath("installation"),
                path!,
                AbsolutePath("desktop")));
    }

    [Fact]
    public void ToString_ShouldNotRevealConfigurationSource()
    {
        HaseClientGuidedInstallationPlan plan = CreatePlan();

        string text = plan.ToString();

        Assert.Equal("Guided HASE Client installation plan", text);
        Assert.DoesNotContain(plan.ConfigurationSourceFilePath, text, StringComparison.Ordinal);
        Assert.DoesNotContain(plan.ConfigurationFilePath, text, StringComparison.Ordinal);
        Assert.DoesNotContain(plan.RuntimeHostRegistryFilePath, text, StringComparison.Ordinal);
    }

    private static HaseClientGuidedInstallationPlan CreatePlan() =>
        new(
            AbsolutePath("installation"),
            AbsolutePath("source-client.json"),
            AbsolutePath("desktop"));

    private static string AbsolutePath(string name) =>
        Path.Combine(Path.GetTempPath(), "hase-43c2", name);
}

using System.IO;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostUpdatePlanTests
{
    [Fact]
    public void Constructor_GuidedInstallation_ShouldPlanApplicationOnlyUpdate()
    {
        string installation = AbsolutePath("installation");
        string desktop = AbsolutePath("desktop");
        var plan = new DesktopRuntimeHostUpdatePlan(installation, desktop);

        Assert.Equal(Path.Combine(installation, "Application"), plan.ApplicationDirectory);
        Assert.Equal(
            Path.Combine(installation, "Application", "Hase.DesktopHost.App.exe"),
            plan.ExecutableFilePath);
        Assert.DoesNotContain(plan.ApplicationDirectory, plan.PreservedFilePaths);
        Assert.Contains(plan.ApplicationProfileFilePath, plan.PreservedFilePaths);
        Assert.Contains(plan.EndpointCompositionFilePath, plan.PreservedFilePaths);
        Assert.Contains(plan.PrivateNetworkConfigurationFilePath, plan.PreservedFilePaths);
        Assert.Contains(plan.MediaConfigurationFilePath, plan.PreservedFilePaths);
        Assert.Contains(plan.ShortcutFilePath, plan.PreservedFilePaths);
    }

    [Fact]
    public void Constructor_ShouldExpectSingleProfileShortcut()
    {
        var plan = CreatePlan();

        Assert.Equal(plan.ExecutableFilePath, plan.ExpectedShortcut.TargetFilePath);
        Assert.Equal($"\"{plan.ApplicationProfileFilePath}\"", plan.ExpectedShortcut.Arguments);
        Assert.Equal(plan.ApplicationDirectory, plan.ExpectedShortcut.WorkingDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative")]
    public void Constructor_InvalidInstallationDirectory_ShouldReject(string? path)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new DesktopRuntimeHostUpdatePlan(path!, AbsolutePath("desktop")));
    }

    [Fact]
    public void ToString_ShouldNotRevealInstallationPaths()
    {
        DesktopRuntimeHostUpdatePlan plan = CreatePlan();

        string text = plan.ToString();

        Assert.Equal("Desktop Runtime Host application update plan", text);
        Assert.DoesNotContain(plan.InstallationDirectory, text, StringComparison.Ordinal);
        Assert.DoesNotContain(plan.ApplicationProfileFilePath, text, StringComparison.Ordinal);
    }

    private static DesktopRuntimeHostUpdatePlan CreatePlan() =>
        new(AbsolutePath("installation"), AbsolutePath("desktop"));

    private static string AbsolutePath(string name) =>
        Path.Combine(Path.GetTempPath(), "hase-43b4b", name);
}

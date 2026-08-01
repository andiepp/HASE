using System.IO;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostGuidedInstallationPlanTests
{
    [Fact]
    public void Constructor_DefaultPhysicalInstallation_ShouldCreateCompletePlan()
    {
        string installationDirectory = AbsolutePath("installation");
        var plan = new DesktopRuntimeHostGuidedInstallationPlan(
            installationDirectory,
            AbsolutePath("source-private-network.json"),
            "device.local");

        Assert.Equal(Path.Combine(installationDirectory, "Application"), plan.ApplicationDirectory);
        Assert.Equal(Path.Combine(installationDirectory, "Configuration"), plan.ConfigurationDirectory);
        Assert.Equal(Path.Combine(installationDirectory, "Identity"), plan.IdentityDirectory);
        Assert.Equal(RuntimeDiagnosticLevel.Bytes, plan.InstallationProfile.MaximumDiagnosticLevel);
        Assert.False(plan.InstallationProfile.IncludeByteBufferSimulation);

        DesktopRuntimeHostNativeNetworkEndpointProfile native =
            Assert.Single(plan.EndpointComposition.NativeNetworkEndpoints);
        Assert.Equal("doit-esp32-devkitc-v4-01", native.ExpectedEndpointId);
        Assert.Equal("device.local", native.Host);
        Assert.Equal(5000, native.Port);

        DesktopRuntimeHostCompactSerialEndpointProfile compact =
            Assert.Single(plan.EndpointComposition.CompactSerialEndpoints);
        Assert.Equal("arduino-uno-01", compact.ExpectedEndpointId);
        Assert.Equal((ushort)0x2341, compact.VendorId);
        Assert.Equal((ushort)0x0043, compact.ProductId);
        Assert.Equal(115200, compact.BaudRate);
        Assert.Equal(TimeSpan.FromSeconds(3), compact.VerificationTimeout);
    }

    [Fact]
    public void Constructor_ShouldCreateSingleProfileShortcut()
    {
        var plan = CreatePlan();

        Assert.Equal("HASE Runtime Host", plan.Shortcut.Name);
        Assert.Equal(plan.ExecutableFilePath, plan.Shortcut.TargetFilePath);
        Assert.Equal($"\"{plan.ApplicationProfileFilePath}\"", plan.Shortcut.Arguments);
        Assert.Equal(plan.ApplicationDirectory, plan.Shortcut.WorkingDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_InvalidNativeHost_ShouldReject(string? host)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new DesktopRuntimeHostGuidedInstallationPlan(
                AbsolutePath("installation"),
                AbsolutePath("source-private-network.json"),
                host!));
    }

    [Fact]
    public void ToString_ShouldNotRevealPrivateValues()
    {
        var plan = CreatePlan();

        string text = plan.ToString();

        Assert.Equal("Guided Desktop Runtime Host installation plan", text);
        Assert.DoesNotContain(plan.NativeEndpointHost, text, StringComparison.Ordinal);
        Assert.DoesNotContain(plan.PrivateNetworkConfigurationSourceFilePath, text, StringComparison.Ordinal);
    }

    private static DesktopRuntimeHostGuidedInstallationPlan CreatePlan() =>
        new(
            AbsolutePath("installation"),
            AbsolutePath("source-private-network.json"),
            "private-device-host");

    private static string AbsolutePath(string name) =>
        Path.Combine(Path.GetTempPath(), "hase-43b4a", name);
}

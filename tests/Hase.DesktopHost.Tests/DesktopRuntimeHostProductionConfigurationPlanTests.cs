using System.IO;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostProductionConfigurationPlanTests
{
    [Fact]
    public void Create_SingleProfile_ShouldUseInstallationIdentityAndConfiguredEndpoints()
    {
        string identityPath = AbsolutePath("installation-runtime-host.id");
        var installation = new DesktopRuntimeHostInstallationProfile(
            identityPath,
            AbsolutePath("desktop-private-network.json"),
            AbsolutePath("desktop-runtime-endpoints.json"));
        var endpoints = new DesktopRuntimeHostEndpointCompositionProfile(
            [new DesktopRuntimeHostNativeNetworkEndpointProfile("native-02", "configured.local", 6000)],
            [new DesktopRuntimeHostCompactSerialEndpointProfile(
                "compact-02", 0x1234, 0x5678, 57600, TimeSpan.FromSeconds(7))]);
        var startup = new DesktopRuntimeHostStartupConfiguration(
            installation.PrivateNetworkConfigurationFilePath,
            "ignored.local",
            null!)
        {
            InstallationProfile = installation,
            EndpointCompositionProfile = endpoints
        };

        DesktopRuntimeHostProductionConfigurationPlan plan =
            DesktopRuntimeHostProductionConfigurationPlan.Create(
                startup,
                AbsolutePath("legacy.id"),
                new RuntimeHostId("legacy-host"));

        Assert.Equal(identityPath, plan.IdentityFilePath);
        Assert.Null(plan.ConfiguredRuntimeHostId);
        Assert.Same(endpoints, plan.EndpointComposition);
        Assert.Equal(2, plan.ExpectedPublishedEndpointCount);
    }

    [Fact]
    public void Create_SimulationEnabled_ShouldIncludeSimulationInExpectedCount()
    {
        var installation = new DesktopRuntimeHostInstallationProfile(
            AbsolutePath("installation.id"),
            AbsolutePath("desktop-private-network.json"),
            AbsolutePath("desktop-runtime-endpoints.json"));
        var endpoints = new DesktopRuntimeHostEndpointCompositionProfile(
            [new DesktopRuntimeHostNativeNetworkEndpointProfile("native-01", "configured.local", 5000)],
            []);
        var startup = new DesktopRuntimeHostStartupConfiguration(
            installation.PrivateNetworkConfigurationFilePath,
            "ignored.local",
            null!,
            IncludeByteBufferSimulation: true)
        {
            InstallationProfile = installation,
            EndpointCompositionProfile = endpoints
        };

        DesktopRuntimeHostProductionConfigurationPlan plan =
            DesktopRuntimeHostProductionConfigurationPlan.Create(
                startup,
                AbsolutePath("legacy.id"),
                new RuntimeHostId("legacy-host"));

        Assert.Equal(2, plan.ExpectedPublishedEndpointCount);
    }

    [Fact]
    public void Create_Kel103Endpoint_ShouldIncludeItInExpectedCount()
    {
        var installation = new DesktopRuntimeHostInstallationProfile(
            AbsolutePath("installation.id"),
            AbsolutePath("desktop-private-network.json"),
            AbsolutePath("desktop-runtime-endpoints.json"));
        var endpoints = new DesktopRuntimeHostEndpointCompositionProfile(
            [],
            [],
            [new DesktopRuntimeHostKel103SerialEndpointProfile(
                "kel-01", "kel103-identity", 2, "external-target", 115200)]);
        var startup = new DesktopRuntimeHostStartupConfiguration(
            installation.PrivateNetworkConfigurationFilePath,
            "ignored.local",
            null!)
        {
            InstallationProfile = installation,
            EndpointCompositionProfile = endpoints
        };

        DesktopRuntimeHostProductionConfigurationPlan plan =
            DesktopRuntimeHostProductionConfigurationPlan.Create(
                startup,
                AbsolutePath("legacy.id"),
                new RuntimeHostId("legacy-host"));

        Assert.Equal(1, plan.ExpectedPublishedEndpointCount);
    }

    [Fact]
    public void Create_LegacyStartup_ShouldPreserveHistoricalPhysicalDefaults()
    {
        string legacyIdentityPath = AbsolutePath("legacy.id");
        var legacyRuntimeHostId = new RuntimeHostId("legacy-host");
        var startup = new DesktopRuntimeHostStartupConfiguration(
            "configuration.json",
            "legacy-esp32.local",
            null!);

        DesktopRuntimeHostProductionConfigurationPlan plan =
            DesktopRuntimeHostProductionConfigurationPlan.Create(
                startup,
                legacyIdentityPath,
                legacyRuntimeHostId);

        Assert.Equal(legacyIdentityPath, plan.IdentityFilePath);
        Assert.Equal(legacyRuntimeHostId, plan.ConfiguredRuntimeHostId);
        Assert.NotNull(plan.EndpointComposition);
        DesktopRuntimeHostNativeNetworkEndpointProfile native =
            Assert.Single(plan.EndpointComposition.NativeNetworkEndpoints);
        Assert.Equal("legacy-esp32.local", native.Host);
        Assert.Equal(5000, native.Port);
        DesktopRuntimeHostCompactSerialEndpointProfile compact =
            Assert.Single(plan.EndpointComposition.CompactSerialEndpoints);
        Assert.Equal((ushort)0x2341, compact.VendorId);
        Assert.Equal((ushort)0x0043, compact.ProductId);
        Assert.Equal(115200, compact.BaudRate);
        Assert.Equal(TimeSpan.FromSeconds(3), compact.VerificationTimeout);
    }

    private static string AbsolutePath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "hase-43b2", fileName);
}

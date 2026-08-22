using System.IO;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost.Tests;

public sealed class SerialOnlyDesktopRuntimeHostProductionConfigurationPlanTests
{
    [Fact]
    public void Create_SerialOnlyInstallationProfile_ShouldExpectOnePublishedEndpoint()
    {
        var installation = new DesktopRuntimeHostInstallationProfile(
            AbsolutePath("runtime-host.id"),
            AbsolutePath("private-network.json"),
            AbsolutePath("runtime-endpoints.json"));
        var endpoints = new DesktopRuntimeHostEndpointCompositionProfile(
            [],
            [
                new DesktopRuntimeHostCompactSerialEndpointProfile(
                    "arduino-uno-01",
                    0x2341,
                    0x0043,
                    115200,
                    TimeSpan.FromSeconds(3))
            ]);
        var startup = new DesktopRuntimeHostStartupConfiguration(
            installation.PrivateNetworkConfigurationFilePath,
            Esp32Host: null,
            DeploymentOptions: null!)
        {
            InstallationProfile = installation,
            EndpointCompositionProfile = endpoints
        };

        DesktopRuntimeHostProductionConfigurationPlan plan =
            DesktopRuntimeHostProductionConfigurationPlan.Create(
                startup,
                AbsolutePath("legacy.id"),
                new RuntimeHostId("legacy-host"));

        Assert.Same(endpoints, plan.EndpointComposition);
        Assert.NotNull(plan.EndpointComposition);
        Assert.Empty(plan.EndpointComposition.NativeNetworkEndpoints);
        Assert.Single(plan.EndpointComposition.CompactSerialEndpoints);
        Assert.Equal(1, plan.ExpectedPublishedEndpointCount);
    }

    [Fact]
    public void Create_LegacyStartupWithoutEsp32Host_ShouldReject()
    {
        var startup = new DesktopRuntimeHostStartupConfiguration(
            "configuration.json",
            Esp32Host: null,
            DeploymentOptions: null!);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => DesktopRuntimeHostProductionConfigurationPlan.Create(
                startup,
                AbsolutePath("legacy.id"),
                new RuntimeHostId("legacy-host")));

        Assert.Equal("Legacy startup requires an ESP32 host.", exception.Message);
    }

    private static string AbsolutePath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "hase-43g4c2c4a", fileName);
}

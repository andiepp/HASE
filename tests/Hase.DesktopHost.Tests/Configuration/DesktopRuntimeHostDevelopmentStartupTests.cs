using System.IO;
using System.Text.Json;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostDevelopmentStartupTests
{
    [Fact]
    public async Task StartupParse_DevelopmentSwitch_ShouldProduceDevelopmentConfiguration()
    {
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                $$"""
                {
                  "formatVersion": 1,
                  "profileKind": "development-loopback",
                  "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
                  "loopbackAddress": "127.0.0.1",
                  "port": 52110,
                  "includeByteBufferSimulation": true
                }
                """);

            DesktopRuntimeHostStartupConfiguration configuration =
                DesktopRuntimeHostStartupConfiguration.Parse(
                    ["Hase.DesktopHost.App.exe", "--development", filePath]);

            Assert.NotNull(configuration.DevelopmentProfile);
            Assert.Null(configuration.DeploymentOptions);
            Assert.Null(configuration.InstallationProfile);
            Assert.Null(configuration.EndpointCompositionProfile);
            Assert.True(configuration.IncludeByteBufferSimulation);
            Assert.False(configuration.RemoteDiagnosticsEnabled);
            Assert.Equal(
                "None - development loopback profile active",
                configuration.PrivateNetworkBindingDisplay);
            Assert.Throws<InvalidOperationException>(
                () => configuration.RequiredDeploymentOptions);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void StartupParse_DevelopmentSwitchWithRelativePath_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                [
                    "Hase.DesktopHost.App.exe",
                    "--development",
                    "development-profile.json"
                ]));
    }

    [Fact]
    public async Task ConfigurationPlan_DevelopmentProfile_ShouldUseDevelopmentIdentityAndSimulationOnly()
    {
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                $$"""
                {
                  "formatVersion": 1,
                  "profileKind": "development-loopback",
                  "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
                  "loopbackAddress": "127.0.0.1",
                  "port": 52110,
                  "includeByteBufferSimulation": true
                }
                """);

            DesktopRuntimeHostStartupConfiguration configuration =
                DesktopRuntimeHostStartupConfiguration.Parse(
                    ["Hase.DesktopHost.App.exe", "--development", filePath]);

            DesktopRuntimeHostProductionConfigurationPlan plan =
                DesktopRuntimeHostProductionConfigurationPlan.Create(
                    configuration,
                    AbsolutePath("legacy-runtime-host.id"),
                    ProductionPrivateNetworkRuntimeHostBackend.RuntimeHostId);

            Assert.Equal(
                AbsolutePath("runtime-host.id"),
                plan.IdentityFilePath);
            Assert.Null(plan.ConfiguredRuntimeHostId);
            Assert.Null(plan.EndpointComposition);
            Assert.Equal(1, plan.ExpectedPublishedEndpointCount);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string AbsolutePath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "hase-60c1", fileName);

    private static string TemporaryFilePath() =>
        Path.Combine(Path.GetTempPath(), $"hase-60c1-{Guid.NewGuid():N}.json");
}

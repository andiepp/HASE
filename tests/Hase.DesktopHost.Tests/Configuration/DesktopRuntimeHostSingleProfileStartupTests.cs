using System.IO;
using System.Text.Json;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostSingleProfileStartupTests
{
    [Fact]
    public void InstallationProfile_ExplicitEndpointCompositionPath_ShouldPreserveNormalizedPath()
    {
        var profile = new DesktopRuntimeHostInstallationProfile(
            AbsolutePath("runtime-host.id"),
            AbsolutePath("desktop-private-network.json"),
            AbsolutePath("desktop-runtime-endpoints.json"));

        Assert.Equal(
            AbsolutePath("desktop-runtime-endpoints.json"),
            profile.EndpointCompositionFilePath);
    }

    [Fact]
    public void InstallationProfile_DuplicateEndpointCompositionPath_ShouldReject()
    {
        string deploymentPath = AbsolutePath("desktop-private-network.json");

        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostInstallationProfile(
                AbsolutePath("runtime-host.id"),
                deploymentPath,
                deploymentPath));
    }

    [Fact]
    public async Task InstallationProfileFile_ExplicitEndpointCompositionPath_ShouldLoad()
    {
        string filePath = TemporaryFilePath();
        string endpointPath = AbsolutePath("desktop-runtime-endpoints.json");

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                $$"""
                {
                  "formatVersion": 1,
                  "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
                  "privateNetworkConfigurationFilePath": {{JsonSerializer.Serialize(AbsolutePath("desktop-private-network.json"))}},
                  "endpointCompositionFilePath": {{JsonSerializer.Serialize(endpointPath)}}
                }
                """);

            DesktopRuntimeHostInstallationProfile profile =
                await DesktopRuntimeHostInstallationProfileFile.LoadAsync(filePath);

            Assert.Equal(endpointPath, profile.EndpointCompositionFilePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task StartupParse_SingleProfile_ShouldLoadReferencedEndpointCompositionFirst()
    {
        string filePath = TemporaryFilePath();
        string missingEndpointPath = Path.Combine(
            Path.GetTempPath(),
            $"missing-endpoints-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                $$"""
                {
                  "formatVersion": 1,
                  "identityFilePath": {{JsonSerializer.Serialize(AbsolutePath("runtime-host.id"))}},
                  "privateNetworkConfigurationFilePath": {{JsonSerializer.Serialize(AbsolutePath("missing-private-network.json"))}},
                  "endpointCompositionFilePath": {{JsonSerializer.Serialize(missingEndpointPath)}}
                }
                """);

            Assert.Throws<FileNotFoundException>(
                () => DesktopRuntimeHostStartupConfiguration.Parse(
                    ["Hase.DesktopHost.App.exe", filePath]));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string AbsolutePath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "hase-43b1", fileName);

    private static string TemporaryFilePath() =>
        Path.Combine(Path.GetTempPath(), $"hase-43b1-{Guid.NewGuid():N}.json");
}

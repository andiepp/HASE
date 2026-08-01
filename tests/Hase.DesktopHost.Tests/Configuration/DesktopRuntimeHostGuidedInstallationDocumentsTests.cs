using System.IO;
using System.Text;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostGuidedInstallationDocumentsTests
{
    [Fact]
    public async Task CreateApplicationProfile_StrictReader_ShouldLoadExactPlan()
    {
        DesktopRuntimeHostGuidedInstallationPlan plan = CreatePlan();
        string document = DesktopRuntimeHostGuidedInstallationDocuments.CreateApplicationProfile(plan);
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(filePath, document, new UTF8Encoding(false));
            DesktopRuntimeHostInstallationProfile profile =
                await DesktopRuntimeHostInstallationProfileFile.LoadAsync(filePath);

            Assert.Equal(plan.IdentityFilePath, profile.IdentityFilePath);
            Assert.Equal(plan.PrivateNetworkConfigurationFilePath, profile.PrivateNetworkConfigurationFilePath);
            Assert.Equal(plan.EndpointCompositionFilePath, profile.EndpointCompositionFilePath);
            Assert.Equal(RuntimeDiagnosticLevel.Bytes, profile.MaximumDiagnosticLevel);
            Assert.False(profile.IncludeByteBufferSimulation);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task CreateEndpointComposition_StrictReader_ShouldLoadPhysicalDefaults()
    {
        DesktopRuntimeHostGuidedInstallationPlan plan = CreatePlan();
        string document = DesktopRuntimeHostGuidedInstallationDocuments.CreateEndpointComposition(plan);
        string filePath = TemporaryFilePath();

        try
        {
            await File.WriteAllTextAsync(filePath, document, new UTF8Encoding(false));
            DesktopRuntimeHostEndpointCompositionProfile profile =
                await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(filePath);

            DesktopRuntimeHostNativeNetworkEndpointProfile native =
                Assert.Single(profile.NativeNetworkEndpoints);
            Assert.Equal("private-device-host", native.Host);
            Assert.Equal(5000, native.Port);
            DesktopRuntimeHostCompactSerialEndpointProfile compact =
                Assert.Single(profile.CompactSerialEndpoints);
            Assert.Equal((ushort)0x2341, compact.VendorId);
            Assert.Equal((ushort)0x0043, compact.ProductId);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GeneratedDocuments_ShouldNotContainPrivateNetworkConfigurationContents()
    {
        DesktopRuntimeHostGuidedInstallationPlan plan = CreatePlan();

        string application = DesktopRuntimeHostGuidedInstallationDocuments.CreateApplicationProfile(plan);
        string endpoints = DesktopRuntimeHostGuidedInstallationDocuments.CreateEndpointComposition(plan);

        Assert.DoesNotContain("certificateThumbprint", application, StringComparison.Ordinal);
        Assert.DoesNotContain("certificateThumbprint", endpoints, StringComparison.Ordinal);
    }

    private static DesktopRuntimeHostGuidedInstallationPlan CreatePlan() =>
        new(
            Path.Combine(Path.GetTempPath(), "hase-43b4a", "installation"),
            Path.Combine(Path.GetTempPath(), "hase-43b4a", "source-private-network.json"),
            "private-device-host");

    private static string TemporaryFilePath() =>
        Path.Combine(Path.GetTempPath(), $"hase-43b4a-{Guid.NewGuid():N}.json");
}

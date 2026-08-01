using System.IO;
using System.Text;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostEndpointCompositionProfileFileTests
{
    [Fact]
    public async Task LoadAsync_CurrentPhysicalComposition_ShouldLoadStrictProfile()
    {
        DesktopRuntimeHostEndpointCompositionProfile profile = await LoadDocumentAsync(
            """
            {
              "formatVersion": 1,
              "endpoints": [
                {
                  "kind": "NativeNetwork",
                  "expectedEndpointId": "native-01",
                  "host": "device.local",
                  "port": 5000
                },
                {
                  "kind": "CompactSerial",
                  "expectedEndpointId": "compact-01",
                  "vendorId": 9025,
                  "productId": 67,
                  "baudRate": 115200,
                  "verificationTimeoutMilliseconds": 3000
                }
              ]
            }
            """);

        DesktopRuntimeHostNativeNetworkEndpointProfile native =
            Assert.Single(profile.NativeNetworkEndpoints);
        Assert.Equal("device.local", native.Host);
        Assert.Equal(5000, native.Port);

        DesktopRuntimeHostCompactSerialEndpointProfile compact =
            Assert.Single(profile.CompactSerialEndpoints);
        Assert.Equal((ushort)0x2341, compact.VendorId);
        Assert.Equal((ushort)0x0043, compact.ProductId);
        Assert.Equal(115200, compact.BaudRate);
        Assert.Equal(TimeSpan.FromSeconds(3), compact.VerificationTimeout);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    public async Task LoadAsync_UnsupportedEndpointKind_ShouldReject(string kind)
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                $$"""
                {
                  "formatVersion": 1,
                  "endpoints": [
                    {
                      "kind": "{{kind}}",
                      "expectedEndpointId": "endpoint-01"
                    }
                  ]
                }
                """));
    }

    [Fact]
    public async Task LoadAsync_KindSpecificForeignProperty_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                {
                  "formatVersion": 1,
                  "endpoints": [
                    {
                      "kind": "NativeNetwork",
                      "expectedEndpointId": "native-01",
                      "host": "device.local",
                      "port": 5000,
                      "baudRate": 115200
                    }
                  ]
                }
                """));
    }

    [Fact]
    public async Task LoadAsync_UnknownProperty_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                {
                  "formatVersion": 1,
                  "endpoints": [],
                  "automaticAttachment": true
                }
                """));
    }

    [Fact]
    public async Task LoadAsync_DuplicateIdentity_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                {
                  "formatVersion": 1,
                  "endpoints": [
                    { "kind": "NativeNetwork", "expectedEndpointId": "same-01", "host": "device.local", "port": 5000 },
                    { "kind": "CompactSerial", "expectedEndpointId": "same-01", "vendorId": 9025, "productId": 67, "baudRate": 115200, "verificationTimeoutMilliseconds": 3000 }
                  ]
                }
                """));
    }

    [Fact]
    public async Task LoadAsync_UnsupportedVersion_ShouldReject()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync("""{ "formatVersion": 2, "endpoints": [] }"""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("endpoint-composition.json")]
    public async Task LoadAsync_InvalidPath_ShouldReject(string? filePath)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(filePath!));
    }

    private static async Task<DesktopRuntimeHostEndpointCompositionProfile> LoadDocumentAsync(string document)
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"hase-43a4-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(filePath, document, new UTF8Encoding(false));
            return await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(filePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}

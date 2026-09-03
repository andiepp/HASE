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
    [InlineData("NativeNetwork", "\"definitionId\": \"korad-kel103\",")]
    [InlineData("CompactSerial", "\"serialPort\": \"external-target\",")]
    public async Task LoadAsync_CrossFamilyProperty_ShouldReject(string kind, string foreignProperty)
    {
        string familyProperties = kind switch
        {
            "NativeNetwork" => "\"host\": \"device.local\", \"port\": 5000",
            _ => "\"vendorId\": 9025, \"productId\": 67, \"baudRate\": 115200, \"verificationTimeoutMilliseconds\": 3000",
        };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                $$"""
                {
                  "formatVersion": 1,
                  "endpoints": [
                    { "kind": "{{kind}}", "expectedEndpointId": "endpoint-01", {{foreignProperty}} {{familyProperties}} }
                  ]
                }
                """));
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

    [Fact]
    public async Task LoadAsync_Version1KindOfAFamilyThisLibraryDoesNotShip_IsRejectedNamingFormat2()
    {
        // Version 1 named every kind it could carry. A family this library
        // does not ship cannot be read from a version 1 file; the way
        // forward is the provider-keyed format, and the rejection says so.
        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                {
                  "formatVersion": 1,
                  "endpoints": [
                    {
                      "kind": "SomeoneElsesSerial",
                      "expectedEndpointId": "foreign-01"
                    }
                  ]
                }
                """));

        Assert.Contains("format version 2", exception.Message, StringComparison.Ordinal);
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

    private static string RemoveProperty(string document, string propertyName)
    {
        string[] lines = document.Split('\n');
        return string.Join('\n', lines.Where(line => !line.Contains($"\"{propertyName}\"", StringComparison.Ordinal)));
    }
}

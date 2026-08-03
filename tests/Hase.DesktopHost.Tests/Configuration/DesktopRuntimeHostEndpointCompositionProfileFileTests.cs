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

    [Fact]
    public async Task LoadAsync_Kel103OnlyComposition_ShouldLoadStrictProfile()
    {
        DesktopRuntimeHostEndpointCompositionProfile profile = await LoadDocumentAsync(
            """
            {
              "formatVersion": 1,
              "endpoints": [
                {
                  "kind": "Kel103Serial",
                  "expectedEndpointId": "kel-01",
                  "definitionId": "korad-kel103",
                  "definitionVersion": 2,
                  "serialPort": "external-target",
                  "baudRate": 115200
                }
              ]
            }
            """);

        DesktopRuntimeHostKel103SerialEndpointProfile endpoint =
            Assert.Single(profile.Kel103SerialEndpoints);
        Assert.Equal("kel-01", endpoint.ExpectedEndpointId);
        Assert.Equal("korad-kel103", endpoint.DefinitionReference.Id.Value);
        Assert.Equal((ushort)2, endpoint.DefinitionReference.Version);
        Assert.Equal("external-target", endpoint.SerialPort);
        Assert.Equal(115200, endpoint.BaudRate);
        Assert.Empty(profile.NativeNetworkEndpoints);
        Assert.Empty(profile.CompactSerialEndpoints);
    }

    [Fact]
    public async Task LoadAsync_ThreeFamilyComposition_ShouldKeepEndpointsGloballyScoped()
    {
        DesktopRuntimeHostEndpointCompositionProfile profile = await LoadDocumentAsync(
            """
            {
              "formatVersion": 1,
              "endpoints": [
                { "kind": "NativeNetwork", "expectedEndpointId": "native-01", "host": "device.local", "port": 5000 },
                { "kind": "CompactSerial", "expectedEndpointId": "compact-01", "vendorId": 9025, "productId": 67, "baudRate": 115200, "verificationTimeoutMilliseconds": 3000 },
                { "kind": "Kel103Serial", "expectedEndpointId": "kel-01", "definitionId": "korad-kel103", "definitionVersion": 2, "serialPort": "external-target", "baudRate": 115200 }
              ]
            }
            """);

        Assert.Equal("native-01", Assert.Single(profile.NativeNetworkEndpoints).ExpectedEndpointId);
        Assert.Equal("compact-01", Assert.Single(profile.CompactSerialEndpoints).ExpectedEndpointId);
        Assert.Equal("kel-01", Assert.Single(profile.Kel103SerialEndpoints).ExpectedEndpointId);
    }

    [Theory]
    [InlineData("definitionId")]
    [InlineData("definitionVersion")]
    [InlineData("serialPort")]
    [InlineData("baudRate")]
    public async Task LoadAsync_Kel103MissingRequiredProperty_ShouldReject(string omittedProperty)
    {
        string document = """
            {
              "formatVersion": 1,
              "endpoints": [
                {
                  "kind": "Kel103Serial",
                  "expectedEndpointId": "kel-01",
                  "definitionId": "korad-kel103",
                  "definitionVersion": 2,
                  "serialPort": "external-target",
                  "baudRate": 115200
                }
              ]
            }
            """;
        document = RemoveProperty(document, omittedProperty);

        await Assert.ThrowsAsync<InvalidDataException>(() => LoadDocumentAsync(document));
    }

    [Fact]
    public async Task LoadAsync_Kel103UnsupportedBaudRate_ShouldRejectWithoutLeakingSerialTarget()
    {
        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                {
                  "formatVersion": 1,
                  "endpoints": [
                    {
                      "kind": "Kel103Serial",
                      "expectedEndpointId": "kel-01",
                      "definitionId": "korad-kel103",
                      "definitionVersion": 2,
                      "serialPort": "sensitive-external-target",
                      "baudRate": 9600
                    }
                  ]
                }
                """));

        Assert.DoesNotContain("sensitive-external-target", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NativeNetwork", "\"definitionId\": \"korad-kel103\",")]
    [InlineData("CompactSerial", "\"serialPort\": \"external-target\",")]
    [InlineData("Kel103Serial", "\"host\": \"device.local\",")]
    public async Task LoadAsync_CrossFamilyProperty_ShouldReject(string kind, string foreignProperty)
    {
        string familyProperties = kind switch
        {
            "NativeNetwork" => "\"host\": \"device.local\", \"port\": 5000",
            "CompactSerial" => "\"vendorId\": 9025, \"productId\": 67, \"baudRate\": 115200, \"verificationTimeoutMilliseconds\": 3000",
            _ => "\"definitionId\": \"korad-kel103\", \"definitionVersion\": 2, \"serialPort\": \"external-target\", \"baudRate\": 115200"
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

using System.IO;
using System.Text.Json;
using Hase.DesktopHost.App.Hosting;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostStartupConfigurationTests
{
    [Fact]
    public void Parse_WithWrongArgumentCount_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                ["Hase.DesktopHost.App.exe"]));
    }

    [Fact]
    public void Parse_WithUnsupportedOptionalArgument_ShouldRejectBeforeFileLoad()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                [
                    "Hase.DesktopHost.App.exe",
                    "desktop-private-network.json",
                    "esp32.local",
                    "--unsupported"
                ]));
    }

    [Fact]
    public void DefaultsToOperationalDiagnostics()
    {
        DesktopRuntimeHostStartupConfiguration configuration =
            new(
                "configuration.json",
                "esp32.local",
                null!);

        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            configuration.MaximumDiagnosticLevel);
        Assert.False(configuration.RemoteDiagnosticsEnabled);
        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            configuration.RemoteDiagnosticsMaximumLevel);
    }

    [Fact]
    public void Parse_WithDuplicateDiagnosticLevel_ShouldRejectBeforeFileLoad()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                [
                    "Hase.DesktopHost.App.exe",
                    "desktop-private-network.json",
                    "esp32.local",
                    "--diagnostics=protocol",
                    "--diagnostics=bytes"
                ]));
    }

    [Fact]
    public void Parse_WithEmptyConfigurationPath_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                [
                    "Hase.DesktopHost.App.exe",
                    " ",
                    "esp32.local"
                ]));
    }

    [Fact]
    public void Parse_WithEmptyEsp32Host_ShouldRejectBeforeFileLoad()
    {
        Assert.Throws<ArgumentException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                [
                    "Hase.DesktopHost.App.exe",
                    "desktop-private-network.json",
                    " "
                ]));
    }

    [Fact]
    public void Parse_WithSerialOnlyApplicationProfile_ShouldAcceptWithoutLegacyEsp32Host()
    {
        using var files = new ApplicationProfileFiles(
            [CompactEndpoint("arduino-uno-01")]);

        DesktopRuntimeHostStartupConfiguration configuration =
            DesktopRuntimeHostStartupConfiguration.Parse(
                ["Hase.DesktopHost.App.exe", files.ApplicationProfilePath]);

        Assert.Null(configuration.Esp32Host);
        Assert.Empty(configuration.EndpointCompositionProfile!.NativeNetworkEndpoints);
        Assert.Single(configuration.EndpointCompositionProfile.CompactSerialEndpoints);
        Assert.True(configuration.RemoteDiagnosticsEnabled);
        Assert.Equal(
            RuntimeDiagnosticLevel.Protocol,
            configuration.RemoteDiagnosticsMaximumLevel);
        Assert.NotNull(configuration.InstallationProfile!.AuthorizationPolicyFilePath);
    }

    [Fact]
    public void Parse_WithNativeAndSerialApplicationProfile_ShouldPreserveNativeHost()
    {
        using var files = new ApplicationProfileFiles(
            [
                NativeEndpoint("native-01", "esp32.local"),
                CompactEndpoint("arduino-uno-01")
            ]);

        DesktopRuntimeHostStartupConfiguration configuration =
            DesktopRuntimeHostStartupConfiguration.Parse(
                ["Hase.DesktopHost.App.exe", files.ApplicationProfilePath]);

        Assert.Equal("esp32.local", configuration.Esp32Host);
        Assert.Single(configuration.EndpointCompositionProfile!.NativeNetworkEndpoints);
        Assert.Single(configuration.EndpointCompositionProfile.CompactSerialEndpoints);
    }

    [Fact]
    public void Parse_WithMultipleNativeEndpoints_ShouldReject()
    {
        using var files = new ApplicationProfileFiles(
            [
                NativeEndpoint("native-01", "first.local"),
                NativeEndpoint("native-02", "second.local")
            ]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                ["Hase.DesktopHost.App.exe", files.ApplicationProfilePath]));

        Assert.Contains("at most one native network endpoint", exception.Message);
    }

    [Fact]
    public void Parse_WithEmptyEndpointComposition_ShouldReject()
    {
        using var files = new ApplicationProfileFiles([]);

        Assert.Throws<InvalidDataException>(
            () => DesktopRuntimeHostStartupConfiguration.Parse(
                ["Hase.DesktopHost.App.exe", files.ApplicationProfilePath]));
    }

    private static object NativeEndpoint(string endpointId, string host) =>
        new
        {
            kind = "NativeNetwork",
            expectedEndpointId = endpointId,
            host,
            port = 5000
        };

    private static object CompactEndpoint(string endpointId) =>
        new
        {
            kind = "CompactSerial",
            expectedEndpointId = endpointId,
            vendorId = 0x2341,
            productId = 0x0043,
            baudRate = 115200,
            verificationTimeoutMilliseconds = 3000
        };

    private sealed class ApplicationProfileFiles : IDisposable
    {
        private readonly string directory;

        public ApplicationProfileFiles(object[] endpoints)
        {
            directory = Path.Combine(
                Path.GetTempPath(),
                "hase-43g4c2c4a",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            string identityPath = Path.Combine(directory, "runtime-host-identity.json");
            string privateNetworkPath = Path.Combine(directory, "private-network.json");
            string endpointCompositionPath = Path.Combine(directory, "runtime-endpoints.json");
            string enrollmentPath = Path.Combine(directory, "client-enrollment.json");
            ApplicationProfilePath = Path.Combine(directory, "runtime-host.json");

            WriteJson(
                privateNetworkPath,
                new
                {
                    formatVersion = 1,
                    binding = new { address = "192.0.2.1", port = 8443 },
                    serverCertificate = new
                    {
                        storeName = "My",
                        storeLocation = "CurrentUser",
                        thumbprint = "00112233445566778899AABBCCDDEEFF00112233"
                    },
                    clientEnrollmentFilePath = enrollmentPath
                });
            WriteJson(
                endpointCompositionPath,
                new
                {
                    formatVersion = 1,
                    endpoints
                });
            WriteJson(
                ApplicationProfilePath,
                new
                {
                    formatVersion = 1,
                    identityFilePath = identityPath,
                    privateNetworkConfigurationFilePath = privateNetworkPath,
                    endpointCompositionFilePath = endpointCompositionPath,
                    maximumDiagnosticLevel = "Bytes",
                    includeByteBufferSimulation = false,
                    remoteDiagnosticsEnabled = true,
                    remoteDiagnosticsMaximumLevel = "Protocol",
                    authorizationPolicyFilePath = Path.Combine(
                        directory,
                        "runtime-host-authorization.json")
                });
        }

        public string ApplicationProfilePath { get; }

        public void Dispose()
        {
            Directory.Delete(directory, recursive: true);
        }

        private static void WriteJson(string path, object value)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(value));
        }
    }
}

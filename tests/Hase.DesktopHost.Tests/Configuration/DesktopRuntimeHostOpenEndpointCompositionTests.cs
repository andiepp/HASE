using System.IO;
using System.Text;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

/// <summary>
/// Covers the endpoint composition as an open collection keyed by provider:
/// the entry model and the reader that accepts both shapes. Which shape is
/// written, and the migration between them, is covered by
/// <see cref="DesktopRuntimeHostEndpointCompositionFormatMigrationTests"/>.
/// </summary>
public sealed class DesktopRuntimeHostOpenEndpointCompositionTests
{
    [Fact]
    public void Entry_ReadsItsOwnSettings()
    {
        var entry = new DesktopRuntimeHostEndpointEntry(
            "native-network",
            "native-01",
            [
                new("host", "device.local"),
                new("port", "5000"),
                new("vendorId", "9025")
            ]);

        Assert.Equal("device.local", entry.RequireString("host"));
        Assert.Equal(5000, entry.RequireInt32("port"));
        Assert.Equal((ushort)9025, entry.RequireUInt16("vendorId"));
        Assert.True(entry.HasSetting("host"));
        Assert.False(entry.HasSetting("serialPort"));
    }

    [Fact]
    public void Entry_RejectsAnAbsentOrUnparsableSetting()
    {
        var entry = new DesktopRuntimeHostEndpointEntry(
            "native-network",
            "native-01",
            [new("port", "not-a-number")]);

        Assert.Throws<InvalidDataException>(() => entry.RequireString("host"));
        Assert.Throws<InvalidDataException>(() => entry.RequireInt32("port"));
        Assert.Contains(
            "native-01",
            Assert.Throws<InvalidDataException>(
                () => entry.RequireString("host")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Entry_RejectsADuplicateSetting()
    {
        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostEndpointEntry(
                "native-network",
                "native-01",
                [new("host", "first"), new("host", "second")]));
    }

    [Fact]
    public void Profile_KeepsAnEndpointFromAProviderItDoesNotKnow()
    {
        var profile = new DesktopRuntimeHostEndpointCompositionProfile(
            [
                new DesktopRuntimeHostEndpointEntry(
                    "someone-elses-instrument",
                    "foreign-01",
                    [new("whateverItNeeds", "42")])
            ]);

        DesktopRuntimeHostEndpointEntry entry = Assert.Single(profile.Endpoints);

        Assert.Equal("someone-elses-instrument", entry.ProviderId);
        Assert.Equal("42", entry.RequireString("whateverItNeeds"));
        Assert.Empty(profile.NativeNetworkEndpoints);
        Assert.Empty(profile.CompactSerialEndpoints);
        Assert.Empty(profile.Kel103SerialEndpoints);
        Assert.Empty(profile.RfLabSerialEndpoints);
    }

    [Fact]
    public void Profile_ReturnsTheEndpointsOfOneProviderInCompositionOrder()
    {
        var profile = new DesktopRuntimeHostEndpointCompositionProfile(
            [
                Native("native-01"),
                new DesktopRuntimeHostEndpointEntry("other", "other-01"),
                Native("native-02")
            ]);

        Assert.Equal(
            ["native-01", "native-02"],
            profile.ForProvider("native-network")
                .Select(endpoint => endpoint.ExpectedEndpointId));
        Assert.Empty(profile.ForProvider("nobody"));
    }

    [Fact]
    public void Profile_StillRejectsADuplicateIdentityAcrossProviders()
    {
        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostEndpointCompositionProfile(
                [
                    Native("endpoint-01"),
                    new DesktopRuntimeHostEndpointEntry("other", "endpoint-01")
                ]));
    }

    [Fact]
    public void TheProviderIdentifiersAgreeWithTheProvidersThemselves()
    {
        var profile = new DesktopRuntimeHostEndpointCompositionProfile(
            [
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "native-01", "device.local", 5000)
            ],
            [
                new DesktopRuntimeHostCompactSerialEndpointProfile(
                    "compact-01", 0x2341, 0x0043, 115200, TimeSpan.FromSeconds(3))
            ],
            [
                new DesktopRuntimeHostKel103SerialEndpointProfile(
                    "kel-01", "korad-kel103", 2, "external-target", 115200)
            ],
            [
                new DesktopRuntimeHostRfLabSerialEndpointProfile(
                    "rflab-01", "rflab-signal-lab", 1, "external-target", 115200)
            ]);

        Assert.Equal(
            [
                DesktopRuntimeHostNativeNetworkEndpointProvider.Id,
                DesktopRuntimeHostCompactSerialEndpointProvider.Id,
                "kel-103-serial",
                "rf-lab-serial"
            ],
            profile.Endpoints.Select(endpoint => endpoint.ProviderId));
    }

    [Fact]
    public async Task LoadAsync_OpenShape_AcceptsAProviderThisLibraryNeverHeardOf()
    {
        DesktopRuntimeHostEndpointCompositionProfile profile = await LoadDocumentAsync(
            """
            {
              "formatVersion": 2,
              "endpoints": [
                {
                  "providerId": "someone-elses-instrument",
                  "expectedEndpointId": "foreign-01",
                  "settings": {
                    "serialPort": "external-target",
                    "baudRate": 115200,
                    "assertDataTerminalReady": true
                  }
                }
              ]
            }
            """);

        DesktopRuntimeHostEndpointEntry entry = Assert.Single(profile.Endpoints);

        Assert.Equal("someone-elses-instrument", entry.ProviderId);
        Assert.Equal("foreign-01", entry.ExpectedEndpointId);
        Assert.Equal("external-target", entry.RequireString("serialPort"));
        Assert.Equal(115200, entry.RequireInt32("baudRate"));
        Assert.Equal(
            bool.TrueString,
            entry.RequireString("assertDataTerminalReady"));
    }

    [Fact]
    public async Task LoadAsync_OpenShape_ReachesTheSameProvidersAsTheClosedShape()
    {
        DesktopRuntimeHostEndpointCompositionProfile profile = await LoadDocumentAsync(
            """
            {
              "formatVersion": 2,
              "endpoints": [
                {
                  "providerId": "kel-103-serial",
                  "expectedEndpointId": "kel-01",
                  "settings": {
                    "definitionId": "korad-kel103",
                    "definitionVersion": 2,
                    "serialPort": "external-target",
                    "baudRate": 115200
                  }
                }
              ]
            }
            """);

        DesktopRuntimeHostKel103SerialEndpointProfile kel103 =
            Assert.Single(profile.Kel103SerialEndpoints);

        Assert.Equal("kel-01", kel103.ExpectedEndpointId);
        Assert.Equal("external-target", kel103.SerialPort);
        Assert.Equal(115200, kel103.BaudRate);
    }

    [Fact]
    public async Task LoadAsync_OpenShape_RejectsASettingThatIsNotAScalar()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                {
                  "formatVersion": 2,
                  "endpoints": [
                    {
                      "providerId": "someone-elses-instrument",
                      "expectedEndpointId": "foreign-01",
                      "settings": { "nested": { "not": "allowed" } }
                    }
                  ]
                }
                """));
    }

    [Fact]
    public async Task LoadAsync_RejectsAnUnsupportedOrAbsentFormatVersion()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                { "formatVersion": 3, "endpoints": [] }
                """));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadDocumentAsync(
                """
                { "endpoints": [] }
                """));
    }

    private static DesktopRuntimeHostEndpointEntry Native(string endpointId) =>
        new(
            "native-network",
            endpointId,
            [new("host", "device.local"), new("port", "5000")]);

    private static async Task<DesktopRuntimeHostEndpointCompositionProfile>
        LoadDocumentAsync(string document)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"hase-68c-{Guid.NewGuid():N}.json");

        try
        {
            await File.WriteAllTextAsync(filePath, document, new UTF8Encoding(false));
            return await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                filePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}

using System.IO;
using System.Text;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

/// <summary>
/// Covers the migration that opens an installed composition: the preflight
/// that reports what would change, the migration itself, and what an edit
/// writes on either side of it.
/// </summary>
public sealed class DesktopRuntimeHostEndpointCompositionFormatMigrationTests
{
    private const string ClosedComposition =
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
        """;

    private const string OpenCompositionWithForeignProvider =
        """
        {
          "formatVersion": 2,
          "endpoints": [
            {
              "providerId": "someone-elses-instrument",
              "expectedEndpointId": "foreign-01",
              "settings": { "serialPort": "external-target" }
            }
          ]
        }
        """;

    [Fact]
    public async Task Preflight_ReportsWhatMigrationWouldChange()
    {
        using var files = new CompositionFiles(ClosedComposition);

        DesktopRuntimeHostEndpointCompositionFormatAssessment assessment =
            await DesktopRuntimeHostEndpointCompositionFormatPreflight
                .InspectAsync(files.ProfilePath);

        Assert.Equal(1, assessment.FormatVersion);
        Assert.True(assessment.MigrationRequired);
        Assert.True(assessment.ExpressibleInLegacyFormat);
        Assert.Equal(2, assessment.EndpointCount);
        Assert.Equal(
            ["native-network", "compact-serial"],
            assessment.Endpoints.Select(endpoint => endpoint.ProviderId));
        Assert.Equal(
            ["native-01", "compact-01"],
            assessment.Endpoints.Select(endpoint => endpoint.ExpectedEndpointId));
        Assert.Equal([2, 4], assessment.Endpoints.Select(endpoint => endpoint.SettingCount));
    }

    [Fact]
    public async Task Preflight_DoesNotReportSettingValues()
    {
        using var files = new CompositionFiles(ClosedComposition);

        DesktopRuntimeHostEndpointCompositionFormatAssessment assessment =
            await DesktopRuntimeHostEndpointCompositionFormatPreflight
                .InspectAsync(files.ProfilePath);

        Assert.DoesNotContain(
            "sensitive-external-target",
            assessment.ToString(),
            StringComparison.Ordinal);
        Assert.All(
            assessment.Endpoints,
            endpoint => Assert.DoesNotContain(
                "sensitive-external-target",
                endpoint.ToString(),
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Preflight_ChangesNothingOnDisk()
    {
        using var files = new CompositionFiles(ClosedComposition);
        byte[] before = await File.ReadAllBytesAsync(files.ProfilePath);

        _ = await DesktopRuntimeHostEndpointCompositionFormatPreflight
            .InspectAsync(files.ProfilePath);

        Assert.Equal(before, await File.ReadAllBytesAsync(files.ProfilePath));
        Assert.Equal(
            [Path.GetFileName(files.ProfilePath)],
            Directory.GetFiles(files.Directory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Migration_OpensTheCompositionAndRetainsThePreviousFile()
    {
        using var files = new CompositionFiles(ClosedComposition);
        byte[] before = await File.ReadAllBytesAsync(files.ProfilePath);
        var editor = new DesktopRuntimeHostEndpointCompositionProfileEditor();

        await editor.MigrateToOpenFormatAsync(files.ProfilePath, files.BackupPath);

        DesktopRuntimeHostEndpointCompositionProfile migrated =
            await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                files.ProfilePath);

        Assert.Equal(
            DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion,
            migrated.FormatVersion);
        Assert.Equal(before, await File.ReadAllBytesAsync(files.BackupPath));
    }

    [Fact]
    public async Task Migration_PreservesEveryEndpointExactly()
    {
        using var files = new CompositionFiles(ClosedComposition);
        DesktopRuntimeHostEndpointCompositionProfile before =
            await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                files.ProfilePath);

        await new DesktopRuntimeHostEndpointCompositionProfileEditor()
            .MigrateToOpenFormatAsync(files.ProfilePath, files.BackupPath);

        DesktopRuntimeHostEndpointCompositionProfile after =
            await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                files.ProfilePath);

        Assert.Equal(before.Endpoints.Count, after.Endpoints.Count);
        foreach ((DesktopRuntimeHostEndpointEntry original,
            DesktopRuntimeHostEndpointEntry migrated)
            in before.Endpoints.Zip(after.Endpoints))
        {
            Assert.Equal(original.ProviderId, migrated.ProviderId);
            Assert.Equal(original.ExpectedEndpointId, migrated.ExpectedEndpointId);
            Assert.Equal(
                original.Settings.OrderBy(setting => setting.Key, StringComparer.Ordinal),
                migrated.Settings.OrderBy(setting => setting.Key, StringComparer.Ordinal));
        }

        Assert.Equal(
            before.CompactSerialEndpoints,
            after.CompactSerialEndpoints);
        Assert.Equal(
            before.NativeNetworkEndpoints,
            after.NativeNetworkEndpoints);
    }

    [Fact]
    public async Task Migration_RefusesACompositionAlreadyOpen()
    {
        using var files = new CompositionFiles(OpenCompositionWithForeignProvider);
        byte[] before = await File.ReadAllBytesAsync(files.ProfilePath);
        var editor = new DesktopRuntimeHostEndpointCompositionProfileEditor();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => editor.MigrateToOpenFormatAsync(files.ProfilePath, files.BackupPath));

        Assert.Equal(before, await File.ReadAllBytesAsync(files.ProfilePath));
        Assert.False(File.Exists(files.BackupPath));
    }

    [Fact]
    public async Task AnEditBeforeMigration_StillWritesTheClosedFormat()
    {
        using var files = new CompositionFiles(ClosedComposition);

        await new DesktopRuntimeHostEndpointCompositionProfileEditor()
            .AddNativeAsync(
                files.ProfilePath,
                files.BackupPath,
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "native-02", "second.local", 5001));

        DesktopRuntimeHostEndpointCompositionProfile edited =
            await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                files.ProfilePath);

        Assert.Equal(
            DesktopRuntimeHostEndpointCompositionProfile.LegacyFormatVersion,
            edited.FormatVersion);
        Assert.Contains(
            "\"kind\"",
            await File.ReadAllTextAsync(files.ProfilePath),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEditAfterMigration_KeepsTheOpenFormat()
    {
        using var files = new CompositionFiles(ClosedComposition);
        var editor = new DesktopRuntimeHostEndpointCompositionProfileEditor();

        await editor.MigrateToOpenFormatAsync(files.ProfilePath, files.BackupPath);
        await editor.AddNativeAsync(
            files.ProfilePath,
            files.SecondBackupPath,
            new DesktopRuntimeHostNativeNetworkEndpointProfile(
                "native-02", "second.local", 5001));

        DesktopRuntimeHostEndpointCompositionProfile edited =
            await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                files.ProfilePath);

        Assert.Equal(
            DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion,
            edited.FormatVersion);
        Assert.Equal(
            ["native-01", "native-02", "compact-01"],
            edited.Endpoints.Select(endpoint => endpoint.ExpectedEndpointId));
    }

    [Fact]
    public async Task AfterMigration_AForeignProviderNoLongerBlocksAnEdit()
    {
        using var files = new CompositionFiles(OpenCompositionWithForeignProvider);

        await new DesktopRuntimeHostEndpointCompositionProfileEditor()
            .AddNativeAsync(
                files.ProfilePath,
                files.BackupPath,
                new DesktopRuntimeHostNativeNetworkEndpointProfile(
                    "native-01", "device.local", 5000));

        DesktopRuntimeHostEndpointCompositionProfile edited =
            await DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                files.ProfilePath);

        Assert.Equal(
            ["someone-elses-instrument", "native-network"],
            edited.Endpoints.Select(endpoint => endpoint.ProviderId));
        Assert.Equal(
            "external-target",
            edited.Endpoints[0].RequireString("serialPort"));
    }

    private sealed class CompositionFiles : IDisposable
    {
        public CompositionFiles(string document)
        {
            Directory = Path.Combine(
                Path.GetTempPath(),
                $"hase-68d-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(Directory);
            ProfilePath = Path.Combine(Directory, "desktop-runtime-endpoints.json");
            BackupPath = Path.Combine(Directory, "desktop-runtime-endpoints.backup");
            SecondBackupPath = Path.Combine(
                Directory,
                "desktop-runtime-endpoints.second.backup");
            File.WriteAllText(ProfilePath, document, new UTF8Encoding(false));
        }

        public string Directory { get; }

        public string ProfilePath { get; }

        public string BackupPath { get; }

        public string SecondBackupPath { get; }

        public void Dispose() =>
            System.IO.Directory.Delete(Directory, recursive: true);
    }
}

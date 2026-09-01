using System.IO;
using System.Text.Json;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostEndpointCompositionProfileEditorTests
{
    /// <summary>
    /// A definition reference of the family the editor migrates. The editor
    /// takes references rather than definitions, so these tests need no
    /// instrument project to exercise it.
    /// </summary>
    private static DescriptorReference SerialInstrumentDefinition(ushort version) =>
        new(new DescriptorId("kel103-identity"), version);

    [Fact]
    public async Task MigrateKel103DefinitionAsync_VersionFourToFivePreservesIdentityTransportAndBackup()
    {
        using Files files = new();
        files.Write(
            Native("native"),
            Compact("compact"),
            Kel103Version("preserved", 4, "external-target-preserved"),
            Kel103Version("selected", 4, "external-target-selected"));

        await new DesktopRuntimeHostEndpointCompositionProfileEditor()
            .MigrateKel103DefinitionAsync(
                files.Profile,
                files.Backup,
                "selected",
                SerialInstrumentDefinition(4),
                SerialInstrumentDefinition(5));

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        DesktopRuntimeHostKel103SerialEndpointProfile selected =
            active.Kel103SerialEndpoints.Single(endpoint =>
                endpoint.ExpectedEndpointId == "selected");
        Assert.Equal("selected", selected.ExpectedEndpointId);
        Assert.Equal(
            SerialInstrumentDefinition(5),
            selected.DefinitionReference);
        Assert.Equal("external-target-selected", selected.SerialPort);
        Assert.Equal(115200, selected.BaudRate);
        Assert.Equal(
            SerialInstrumentDefinition(4),
            active.Kel103SerialEndpoints.Single(endpoint =>
                endpoint.ExpectedEndpointId == "preserved").DefinitionReference);
        Assert.Single(active.NativeNetworkEndpoints);
        Assert.Single(active.CompactSerialEndpoints);

        DesktopRuntimeHostEndpointCompositionProfile backup =
            await files.Load(files.Backup);
        Assert.All(
            backup.Kel103SerialEndpoints,
            endpoint => Assert.Equal(
                SerialInstrumentDefinition(4),
                endpoint.DefinitionReference));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task MigrateKel103DefinitionAsync_VersionFiveMigrationRejectsWrongCurrentVersion(
        ushort version)
    {
        using Files files = new();
        files.Write(Kel103Version("selected", version, "external-target"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .MigrateKel103DefinitionAsync(
                    files.Profile,
                    files.Backup,
                    "selected",
                    SerialInstrumentDefinition(4),
                    SerialInstrumentDefinition(5)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task MigrateKel103DefinitionAsync_ChangesOnlyExactDefinitionAndRetainsBackup()
    {
        using Files files = new();
        files.Write(
            Native("native"),
            Compact("compact"),
            Kel103Version("first", 2, "external-target-first"),
            Kel103Version("selected", 2, "external-target-selected"));

        await new DesktopRuntimeHostEndpointCompositionProfileEditor()
            .MigrateKel103DefinitionAsync(
                files.Profile,
                files.Backup,
                "selected",
                SerialInstrumentDefinition(2),
                SerialInstrumentDefinition(4));

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        DesktopRuntimeHostKel103SerialEndpointProfile selected = active.Kel103SerialEndpoints
            .Single(endpoint => endpoint.ExpectedEndpointId == "selected");
        Assert.Equal(SerialInstrumentDefinition(4), selected.DefinitionReference);
        Assert.Equal("external-target-selected", selected.SerialPort);
        Assert.Equal(115200, selected.BaudRate);
        Assert.Equal(
            SerialInstrumentDefinition(2),
            active.Kel103SerialEndpoints.Single(endpoint =>
                endpoint.ExpectedEndpointId == "first").DefinitionReference);
        Assert.Single(active.NativeNetworkEndpoints);
        Assert.Single(active.CompactSerialEndpoints);

        DesktopRuntimeHostEndpointCompositionProfile backup = await files.Load(files.Backup);
        Assert.All(
            backup.Kel103SerialEndpoints,
            endpoint => Assert.Equal(
                SerialInstrumentDefinition(2),
                endpoint.DefinitionReference));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task MigrateKel103DefinitionAsync_WrongCurrentVersionPreservesActive(
        ushort version)
    {
        using Files files = new();
        files.Write(Kel103Version("selected", version, "external-target"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .MigrateKel103DefinitionAsync(
                    files.Profile,
                    files.Backup,
                    "selected",
                    SerialInstrumentDefinition(2),
                    SerialInstrumentDefinition(4)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task MigrateKel103DefinitionAsync_MissingEndpointPreservesActive()
    {
        using Files files = new();
        files.Write(Kel103Version("other", 2, "external-target"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .MigrateKel103DefinitionAsync(
                    files.Profile,
                    files.Backup,
                    "missing",
                    SerialInstrumentDefinition(2),
                    SerialInstrumentDefinition(4)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task MigrateKel103DefinitionAsync_WrongFamilyPreservesActive()
    {
        using Files files = new();
        files.Write(Native("selected"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .MigrateKel103DefinitionAsync(
                    files.Profile,
                    files.Backup,
                    "selected",
                    SerialInstrumentDefinition(2),
                    SerialInstrumentDefinition(4)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task MigrateKel103DefinitionAsync_ExistingBackupIsNeverOverwritten()
    {
        using Files files = new();
        files.Write(Kel103Version("selected", 2, "external-target"));
        File.WriteAllText(files.Backup, "preserved-backup");
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<IOException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .MigrateKel103DefinitionAsync(
                    files.Profile,
                    files.Backup,
                    "selected",
                    SerialInstrumentDefinition(2),
                    SerialInstrumentDefinition(4)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.Equal("preserved-backup", File.ReadAllText(files.Backup));
    }

    [Fact]
    public async Task MigrateKel103DefinitionAsync_PreCancellationPreservesActive()
    {
        using Files files = new();
        files.Write(Kel103Version("selected", 2, "external-target"));
        string before = File.ReadAllText(files.Profile);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .MigrateKel103DefinitionAsync(
                    files.Profile,
                    files.Backup,
                    "selected",
                    SerialInstrumentDefinition(2),
                    SerialInstrumentDefinition(4),
                    cancellation.Token));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task MigrateKel103DefinitionAsync_MalformedProfilePreservesActive()
    {
        using Files files = new();
        File.WriteAllText(files.Profile, "{ malformed");
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .MigrateKel103DefinitionAsync(
                    files.Profile,
                    files.Backup,
                    "selected",
                    SerialInstrumentDefinition(2),
                    SerialInstrumentDefinition(4)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task MigrateKel103DefinitionAsync_EqualDefinitionsRejectBeforeFileAccess()
    {
        var editor = new DesktopRuntimeHostEndpointCompositionProfileEditor();

        await Assert.ThrowsAsync<ArgumentException>(() => editor.MigrateKel103DefinitionAsync(
            "unused-profile",
            "unused-backup",
            "selected",
            SerialInstrumentDefinition(2),
            SerialInstrumentDefinition(2)));
    }

    [Fact]
    public async Task AddCompactAsync_ShouldAppendAndBackup()
    {
        using Files files = new(); files.Write(Native("first"), Kel103("preserved"));
        await new DesktopRuntimeHostEndpointCompositionProfileEditor().AddCompactAsync(
            files.Profile, files.Backup,
            new DesktopRuntimeHostCompactSerialEndpointProfile(
                "second", 0x1234, 0x5678, 9600, TimeSpan.FromSeconds(2)));
        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        Assert.Equal("first", Assert.Single(active.NativeNetworkEndpoints).ExpectedEndpointId);
        Assert.Equal("second", Assert.Single(active.CompactSerialEndpoints).ExpectedEndpointId);
        Assert.Equal("preserved", Assert.Single(active.Kel103SerialEndpoints).ExpectedEndpointId);
        Assert.Single((await files.Load(files.Backup)).NativeNetworkEndpoints);
    }

    [Fact]
    public async Task AddCompactAsync_DuplicateAcrossKinds_ShouldPreserveActive()
    {
        using Files files = new(); files.Write(Native("same"));
        string before = File.ReadAllText(files.Profile);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().AddCompactAsync(
                files.Profile, files.Backup,
                new DesktopRuntimeHostCompactSerialEndpointProfile(
                    "same", 0x1234, 0x5678, 9600, TimeSpan.FromSeconds(2))));
        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task RemoveCompactAsync_ShouldRemoveExactAndBackup()
    {
        using Files files = new(); files.Write(Native("native"), Compact("compact"), Kel103("preserved"));
        await new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveCompactAsync(
            files.Profile, files.Backup, "compact");
        Assert.Empty((await files.Load()).CompactSerialEndpoints);
        Assert.Single((await files.Load()).NativeNetworkEndpoints);
        Assert.Equal("preserved", Assert.Single((await files.Load()).Kel103SerialEndpoints).ExpectedEndpointId);
        Assert.Single((await files.Load(files.Backup)).CompactSerialEndpoints);
    }

    [Fact]
    public async Task RemoveCompactAsync_WrongKind_ShouldReject()
    {
        using Files files = new(); files.Write(Native("native"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveCompactAsync(
                files.Profile, files.Backup, "native"));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public void UsbIdentifierParser_ExactHex_ShouldParse()
    {
        Assert.Equal((ushort)0x2341,
            CompactSerialUsbIdentifierParser.ParseExactHex16("0x2341", "USB vendor ID"));
        Assert.Equal((ushort)0xABcd,
            CompactSerialUsbIdentifierParser.ParseExactHex16("0xABcd", "USB product ID"));
    }

    [Fact]
    public void UsbIdentifierParser_MalformedValues_ShouldReject()
    {
        foreach (string value in new[] { "2341", "0X2341", "0x123", "0x12345", "0xZZZZ" })
        {
            Assert.Throws<ArgumentException>(() =>
                CompactSerialUsbIdentifierParser.ParseExactHex16(value, "USB vendor ID"));
        }
    }

    [Fact]
    public async Task AddNativeAsync_ShouldAppendAndBackup()
    {
        using Files files = new(); files.Write(Native("first"), Kel103("preserved"));
        await new DesktopRuntimeHostEndpointCompositionProfileEditor().AddNativeAsync(
            files.Profile, files.Backup,
            new DesktopRuntimeHostNativeNetworkEndpointProfile("second", "192.0.2.2", 5001));
        Assert.Equal(new[] { "first", "second" },
            (await files.Load()).NativeNetworkEndpoints.Select(x => x.ExpectedEndpointId));
        Assert.Equal("preserved", Assert.Single((await files.Load()).Kel103SerialEndpoints).ExpectedEndpointId);
        Assert.Single((await files.Load(files.Backup)).NativeNetworkEndpoints);
    }

    [Fact]
    public async Task AddNativeAsync_DuplicateAcrossKinds_ShouldPreserveActive()
    {
        using Files files = new(); files.Write(Compact("same"));
        string before = File.ReadAllText(files.Profile);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().AddNativeAsync(
                files.Profile, files.Backup,
                new DesktopRuntimeHostNativeNetworkEndpointProfile("same", "192.0.2.2", 5001)));
        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task RemoveNativeAsync_ShouldRemoveExactAndBackup()
    {
        using Files files = new(); files.Write(Native("first"), Native("second"), Kel103("preserved"));
        await new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveNativeAsync(
            files.Profile, files.Backup, "second");
        Assert.Equal("first", Assert.Single((await files.Load()).NativeNetworkEndpoints).ExpectedEndpointId);
        Assert.Equal("preserved", Assert.Single((await files.Load()).Kel103SerialEndpoints).ExpectedEndpointId);
        Assert.Equal(2, (await files.Load(files.Backup)).NativeNetworkEndpoints.Count);
    }

    [Fact]
    public async Task RemoveNativeAsync_Unknown_ShouldReject()
    {
        using Files files = new(); files.Write(Native("first"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveNativeAsync(
                files.Profile, files.Backup, "missing"));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task ExistingBackup_ShouldNeverBeOverwritten()
    {
        using Files files = new(); files.Write(Native("first"));
        File.WriteAllText(files.Backup, "preserved");
        await Assert.ThrowsAsync<IOException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().AddNativeAsync(
                files.Profile, files.Backup,
                new DesktopRuntimeHostNativeNetworkEndpointProfile("second", "192.0.2.2", 5001)));
        Assert.Equal("preserved", File.ReadAllText(files.Backup));
    }

    [Fact]
    public async Task AddKel103Async_ShouldAppendExactProfileAndBackup()
    {
        using Files files = new(); files.Write(Native("native"), Compact("compact"));
        var endpoint = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "kel", "korad-kel103", 2, "external-target", 115200);

        await new DesktopRuntimeHostEndpointCompositionProfileEditor().AddKel103Async(
            files.Profile, files.Backup, endpoint);

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        DesktopRuntimeHostKel103SerialEndpointProfile added =
            Assert.Single(active.Kel103SerialEndpoints);
        Assert.Equal("kel", added.ExpectedEndpointId);
        Assert.Equal("korad-kel103", added.DefinitionReference.Id.Value);
        Assert.Equal((ushort)2, added.DefinitionReference.Version);
        Assert.Equal("external-target", added.SerialPort);
        Assert.Equal(115200, added.BaudRate);
        Assert.Single(active.NativeNetworkEndpoints);
        Assert.Single(active.CompactSerialEndpoints);
        Assert.Empty((await files.Load(files.Backup)).Kel103SerialEndpoints);
    }

    [Fact]
    public async Task RemoveKel103Async_ShouldRemoveExactAndPreserveOtherFamilies()
    {
        using Files files = new();
        files.Write(Native("native"), Compact("compact"), Kel103("first"), Kel103("second"));

        await new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveKel103Async(
            files.Profile, files.Backup, "second");

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        Assert.Equal("first", Assert.Single(active.Kel103SerialEndpoints).ExpectedEndpointId);
        Assert.Single(active.NativeNetworkEndpoints);
        Assert.Single(active.CompactSerialEndpoints);
        Assert.Equal(2, (await files.Load(files.Backup)).Kel103SerialEndpoints.Count);
    }

    [Fact]
    public async Task AddKel103Async_DuplicateAcrossKinds_ShouldPreserveActiveWithoutTargetLeak()
    {
        using Files files = new(); files.Write(Native("same"));
        string before = File.ReadAllText(files.Profile);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().AddKel103Async(
                files.Profile, files.Backup,
                new DesktopRuntimeHostKel103SerialEndpointProfile(
                    "same", "korad-kel103", 2, "sensitive-external-target", 115200)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
        Assert.DoesNotContain("sensitive-external-target", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveKel103Async_Unknown_ShouldPreserveActive()
    {
        using Files files = new(); files.Write(Kel103("first"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveKel103Async(
                files.Profile, files.Backup, "missing"));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task RemoveKel103Async_WrongKind_ShouldPreserveActive()
    {
        using Files files = new(); files.Write(Compact("compact"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveKel103Async(
                files.Profile, files.Backup, "compact"));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task AddKel103Async_InvalidTarget_ShouldPreserveActiveWithoutTargetLeak()
    {
        using Files files = new(); files.Write(Native("native"));
        string before = File.ReadAllText(files.Profile);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().AddKel103Async(
                files.Profile, files.Backup,
                new DesktopRuntimeHostKel103SerialEndpointProfile(
                    "kel", "korad-kel103", 2, "   ", 115200)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
        Assert.Equal("serialPort", exception.ParamName);
        Assert.DoesNotContain("   ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddRfLabAsync_ShouldAppendExactProfileAndBackup()
    {
        using Files files = new(); files.Write(Native("native"), Kel103("kel"));
        var endpoint = new DesktopRuntimeHostRfLabSerialEndpointProfile(
            "rflab", "rflab-signal-lab", 2, "external-target", 115200);

        await new DesktopRuntimeHostEndpointCompositionProfileEditor().AddRfLabAsync(
            files.Profile, files.Backup, endpoint);

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        DesktopRuntimeHostRfLabSerialEndpointProfile added =
            Assert.Single(active.RfLabSerialEndpoints);
        Assert.Equal("rflab", added.ExpectedEndpointId);
        Assert.Equal("rflab-signal-lab", added.DefinitionReference.Id.Value);
        Assert.Equal((ushort)2, added.DefinitionReference.Version);
        Assert.Equal("external-target", added.SerialPort);
        Assert.Equal(115200, added.BaudRate);
        Assert.Single(active.NativeNetworkEndpoints);
        Assert.Single(active.Kel103SerialEndpoints);
        Assert.Empty((await files.Load(files.Backup)).RfLabSerialEndpoints);
    }

    [Fact]
    public async Task RemoveRfLabAsync_ShouldRemoveExactAndPreserveOtherFamilies()
    {
        using Files files = new();
        files.Write(Native("native"), Kel103("kel"), RfLab("first"), RfLab("second"));

        await new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveRfLabAsync(
            files.Profile, files.Backup, "second");

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        Assert.Equal("first", Assert.Single(active.RfLabSerialEndpoints).ExpectedEndpointId);
        Assert.Single(active.NativeNetworkEndpoints);
        Assert.Single(active.Kel103SerialEndpoints);
        Assert.Equal(2, (await files.Load(files.Backup)).RfLabSerialEndpoints.Count);
    }

    [Fact]
    public async Task AddRfLabAsync_DuplicateAcrossKinds_ShouldPreserveActiveWithoutTargetLeak()
    {
        using Files files = new(); files.Write(Kel103("same"));
        string before = File.ReadAllText(files.Profile);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().AddRfLabAsync(
                files.Profile, files.Backup,
                new DesktopRuntimeHostRfLabSerialEndpointProfile(
                    "same", "rflab-signal-lab", 1, "sensitive-external-target", 115200)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
        Assert.DoesNotContain("sensitive-external-target", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveRfLabAsync_WrongKind_ShouldPreserveActive()
    {
        using Files files = new(); files.Write(Kel103("kel"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveRfLabAsync(
                files.Profile, files.Backup, "kel"));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task Kel103Edits_ShouldPreserveRfLabEndpoints()
    {
        using Files files = new();
        files.Write(RfLab("rflab"), Kel103("kel"));

        await new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveKel103Async(
            files.Profile, files.Backup, "kel");

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        Assert.Empty(active.Kel103SerialEndpoints);
        Assert.Equal("rflab", Assert.Single(active.RfLabSerialEndpoints).ExpectedEndpointId);
    }

    private static object RfLab(string id) => new { kind = "RfLabSerial", expectedEndpointId = id, definitionId = "rflab-signal-lab", definitionVersion = 1, serialPort = $"external-target-{id}", baudRate = 115200 };

    private static object Native(string id) => new { kind = "NativeNetwork", expectedEndpointId = id, host = "192.0.2.1", port = 5000 };
    private static object Compact(string id) => new { kind = "CompactSerial", expectedEndpointId = id, vendorId = 0x2341, productId = 0x0043, baudRate = 115200, verificationTimeoutMilliseconds = 3000 };
    private static object Kel103(string id) => new { kind = "Kel103Serial", expectedEndpointId = id, definitionId = "korad-kel103", definitionVersion = 2, serialPort = $"external-target-{id}", baudRate = 115200 };
    private static object Kel103Version(string id, ushort version, string serialTarget) => new
    {
        kind = "Kel103Serial",
        expectedEndpointId = id,
        definitionId = "kel103-identity",
        definitionVersion = version,
        serialPort = serialTarget,
        baudRate = 115200
    };

    private sealed class Files : IDisposable
    {
        public Files() { DirectoryPath = Path.Combine(Path.GetTempPath(), "hase-43g4a", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(DirectoryPath); Profile = Path.Combine(DirectoryPath, "endpoints.json"); Backup = Path.Combine(DirectoryPath, "backup.json"); }
        public string DirectoryPath { get; } public string Profile { get; } public string Backup { get; }
        public void Write(params object[] endpoints) => File.WriteAllText(Profile, JsonSerializer.Serialize(new { formatVersion = 1, endpoints }));
        public Task<DesktopRuntimeHostEndpointCompositionProfile> Load(string? path = null) => DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(path ?? Profile);
        public void Dispose() => Directory.Delete(DirectoryPath, true);
    }
}

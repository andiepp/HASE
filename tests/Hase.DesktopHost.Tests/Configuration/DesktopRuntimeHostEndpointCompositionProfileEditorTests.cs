using System.IO;
using System.Text.Json;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostEndpointCompositionProfileEditorTests
{
    [Fact]
    public async Task AddCompactAsync_ShouldAppendAndBackup()
    {
        using Files files = new(); files.Write(Native("first"));
        await new DesktopRuntimeHostEndpointCompositionProfileEditor().AddCompactAsync(
            files.Profile, files.Backup,
            new DesktopRuntimeHostCompactSerialEndpointProfile(
                "second", 0x1234, 0x5678, 9600, TimeSpan.FromSeconds(2)));
        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        Assert.Equal("first", Assert.Single(active.NativeNetworkEndpoints).ExpectedEndpointId);
        Assert.Equal("second", Assert.Single(active.CompactSerialEndpoints).ExpectedEndpointId);
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
        using Files files = new(); files.Write(Native("native"), Compact("compact"));
        await new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveCompactAsync(
            files.Profile, files.Backup, "compact");
        Assert.Empty((await files.Load()).CompactSerialEndpoints);
        Assert.Single((await files.Load()).NativeNetworkEndpoints);
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
        using Files files = new(); files.Write(Native("first"));
        await new DesktopRuntimeHostEndpointCompositionProfileEditor().AddNativeAsync(
            files.Profile, files.Backup,
            new DesktopRuntimeHostNativeNetworkEndpointProfile("second", "192.0.2.2", 5001));
        Assert.Equal(new[] { "first", "second" },
            (await files.Load()).NativeNetworkEndpoints.Select(x => x.ExpectedEndpointId));
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
        using Files files = new(); files.Write(Native("first"), Native("second"));
        await new DesktopRuntimeHostEndpointCompositionProfileEditor().RemoveNativeAsync(
            files.Profile, files.Backup, "second");
        Assert.Equal("first", Assert.Single((await files.Load()).NativeNetworkEndpoints).ExpectedEndpointId);
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

    private static object Native(string id) => new { kind = "NativeNetwork", expectedEndpointId = id, host = "192.0.2.1", port = 5000 };
    private static object Compact(string id) => new { kind = "CompactSerial", expectedEndpointId = id, vendorId = 0x2341, productId = 0x0043, baudRate = 115200, verificationTimeoutMilliseconds = 3000 };

    private sealed class Files : IDisposable
    {
        public Files() { DirectoryPath = Path.Combine(Path.GetTempPath(), "hase-43g4a", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(DirectoryPath); Profile = Path.Combine(DirectoryPath, "endpoints.json"); Backup = Path.Combine(DirectoryPath, "backup.json"); }
        public string DirectoryPath { get; } public string Profile { get; } public string Backup { get; }
        public void Write(params object[] endpoints) => File.WriteAllText(Profile, JsonSerializer.Serialize(new { formatVersion = 1, endpoints }));
        public Task<DesktopRuntimeHostEndpointCompositionProfile> Load(string? path = null) => DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(path ?? Profile);
        public void Dispose() => Directory.Delete(DirectoryPath, true);
    }
}

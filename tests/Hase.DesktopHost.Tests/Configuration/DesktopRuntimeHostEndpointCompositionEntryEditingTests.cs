using System.IO;
using System.Text.Json;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

/// <summary>
/// Pins the provider-agnostic editing seam: an endpoint of any provider can
/// be added, removed and replaced through the editor without the editor
/// knowing the provider.
/// </summary>
/// <remarks>
/// Every provider here is one the base has never heard of. That is the
/// point: an add-on's tooling edits the composition through these three
/// operations, and nothing about its family needs to exist in the base for
/// them to work. The composition's own rules, one identity per endpoint and
/// a backup that is never overwritten, hold unchanged.
/// </remarks>
public sealed class DesktopRuntimeHostEndpointCompositionEntryEditingTests
{
    private const string Provider = "example-provider";
    private const string OtherProvider = "other-provider";

    [Fact]
    public async Task AddEntryAsync_InsertsAfterTheLastOfItsProviderAndBacksUp()
    {
        using Files files = new();
        files.Write(
            Entry(Provider, "first", ("port", "COM-A")),
            Entry(OtherProvider, "other"),
            Entry(Provider, "second", ("port", "COM-B")));

        await new DesktopRuntimeHostEndpointCompositionProfileEditor()
            .AddEntryAsync(
                files.Profile,
                files.Backup,
                new DesktopRuntimeHostEndpointEntry(
                    Provider,
                    "third",
                    [new("port", "COM-C"), new("baudRate", "9600")]));

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        Assert.Equal(
            ["first", "other", "second", "third"],
            active.Endpoints.Select(endpoint => endpoint.ExpectedEndpointId));
        DesktopRuntimeHostEndpointEntry added =
            active.ForProvider(Provider).Single(endpoint =>
                endpoint.ExpectedEndpointId == "third");
        Assert.Equal("COM-C", added.RequireString("port"));
        Assert.Equal(9600, added.RequireInt32("baudRate"));
        Assert.Equal(
            DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion,
            active.FormatVersion);

        DesktopRuntimeHostEndpointCompositionProfile backup =
            await files.Load(files.Backup);
        Assert.Equal(3, backup.Endpoints.Count);
    }

    [Fact]
    public async Task AddEntryAsync_NewProvider_AppendsAtTheEnd()
    {
        using Files files = new();
        files.Write(Entry(Provider, "first"), Entry(OtherProvider, "other"));

        await new DesktopRuntimeHostEndpointCompositionProfileEditor()
            .AddEntryAsync(
                files.Profile,
                files.Backup,
                new DesktopRuntimeHostEndpointEntry("third-provider", "newcomer"));

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        Assert.Equal(
            ["first", "other", "newcomer"],
            active.Endpoints.Select(endpoint => endpoint.ExpectedEndpointId));
    }

    [Fact]
    public async Task AddEntryAsync_DuplicateIdentityAcrossProviders_PreservesActive()
    {
        using Files files = new();
        files.Write(Entry(Provider, "shared"), Entry(OtherProvider, "other"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .AddEntryAsync(
                    files.Profile,
                    files.Backup,
                    new DesktopRuntimeHostEndpointEntry(OtherProvider, "shared")));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task RemoveEntryAsync_RemovesExactAndPreservesOtherProviders()
    {
        using Files files = new();
        files.Write(
            Entry(Provider, "first"),
            Entry(OtherProvider, "other"),
            Entry(Provider, "second"));

        await new DesktopRuntimeHostEndpointCompositionProfileEditor()
            .RemoveEntryAsync(files.Profile, files.Backup, Provider, "first");

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        Assert.Equal(
            ["other", "second"],
            active.Endpoints.Select(endpoint => endpoint.ExpectedEndpointId));

        DesktopRuntimeHostEndpointCompositionProfile backup =
            await files.Load(files.Backup);
        Assert.Contains(
            backup.Endpoints,
            endpoint => endpoint.ExpectedEndpointId == "first");
    }

    [Theory]
    [InlineData(Provider, "unknown")]
    [InlineData(OtherProvider, "first")]
    public async Task RemoveEntryAsync_AbsentOrWrongProvider_PreservesActive(
        string providerId,
        string expectedEndpointId)
    {
        using Files files = new();
        files.Write(Entry(Provider, "first"), Entry(OtherProvider, "other"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .RemoveEntryAsync(
                    files.Profile,
                    files.Backup,
                    providerId,
                    expectedEndpointId));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task ReplaceEntryAsync_ReplacesSettingsInPlaceAndBacksUp()
    {
        using Files files = new();
        files.Write(
            Entry(Provider, "first", ("definitionVersion", "4"), ("port", "COM-A")),
            Entry(OtherProvider, "other"),
            Entry(Provider, "second", ("definitionVersion", "4")));

        await new DesktopRuntimeHostEndpointCompositionProfileEditor()
            .ReplaceEntryAsync(
                files.Profile,
                files.Backup,
                Provider,
                "first",
                new DesktopRuntimeHostEndpointEntry(
                    Provider,
                    "first",
                    [new("definitionVersion", "5"), new("port", "COM-A")]));

        DesktopRuntimeHostEndpointCompositionProfile active = await files.Load();
        Assert.Equal(
            ["first", "other", "second"],
            active.Endpoints.Select(endpoint => endpoint.ExpectedEndpointId));
        Assert.Equal((ushort)5, active.Endpoints[0].RequireUInt16("definitionVersion"));
        Assert.Equal("COM-A", active.Endpoints[0].RequireString("port"));
        Assert.Equal((ushort)4, active.Endpoints[2].RequireUInt16("definitionVersion"));

        DesktopRuntimeHostEndpointCompositionProfile backup =
            await files.Load(files.Backup);
        Assert.Equal((ushort)4, backup.Endpoints[0].RequireUInt16("definitionVersion"));
    }

    [Fact]
    public async Task ReplaceEntryAsync_Absent_PreservesActive()
    {
        using Files files = new();
        files.Write(Entry(Provider, "first"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .ReplaceEntryAsync(
                    files.Profile,
                    files.Backup,
                    Provider,
                    "unknown",
                    new DesktopRuntimeHostEndpointEntry(Provider, "unknown")));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Theory]
    [InlineData(OtherProvider, "first")]
    [InlineData(Provider, "renamed")]
    public async Task ReplaceEntryAsync_ChangedIdentity_IsRejectedBeforeFileAccess(
        string replacementProvider,
        string replacementEndpointId)
    {
        using Files files = new();
        files.Write(Entry(Provider, "first"));
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .ReplaceEntryAsync(
                    files.Profile,
                    files.Backup,
                    Provider,
                    "first",
                    new DesktopRuntimeHostEndpointEntry(
                        replacementProvider,
                        replacementEndpointId)));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.False(File.Exists(files.Backup));
    }

    [Fact]
    public async Task EntryEdits_NeverOverwriteAnExistingBackup()
    {
        using Files files = new();
        files.Write(Entry(Provider, "first"));
        File.WriteAllText(files.Backup, "retained");
        string before = File.ReadAllText(files.Profile);

        await Assert.ThrowsAsync<IOException>(() =>
            new DesktopRuntimeHostEndpointCompositionProfileEditor()
                .AddEntryAsync(
                    files.Profile,
                    files.Backup,
                    new DesktopRuntimeHostEndpointEntry(Provider, "second")));

        Assert.Equal(before, File.ReadAllText(files.Profile));
        Assert.Equal("retained", File.ReadAllText(files.Backup));
    }

    private static object Entry(
        string providerId,
        string expectedEndpointId,
        params (string Name, string Value)[] settings) =>
        new
        {
            providerId,
            expectedEndpointId,
            settings = settings.ToDictionary(
                setting => setting.Name,
                setting => setting.Value,
                StringComparer.Ordinal)
        };

    private sealed class Files : IDisposable
    {
        public Files()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "hase-68i2a",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            Profile = Path.Combine(DirectoryPath, "desktop-runtime-endpoints.json");
            Backup = Path.Combine(DirectoryPath, "desktop-runtime-endpoints.backup.json");
        }

        public string DirectoryPath { get; }

        public string Profile { get; }

        public string Backup { get; }

        public void Write(params object[] endpoints) =>
            File.WriteAllText(
                Profile,
                JsonSerializer.Serialize(new
                {
                    formatVersion =
                        DesktopRuntimeHostEndpointCompositionProfile.OpenFormatVersion,
                    endpoints
                }));

        public Task<DesktopRuntimeHostEndpointCompositionProfile> Load(
            string? path = null) =>
            DesktopRuntimeHostEndpointCompositionProfileFile.LoadAsync(
                path ?? Profile,
                CancellationToken.None);

        public void Dispose() => Directory.Delete(DirectoryPath, true);
    }
}

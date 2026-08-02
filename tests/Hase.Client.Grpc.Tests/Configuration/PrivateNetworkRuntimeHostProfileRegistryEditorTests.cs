using System.IO;
using System.Text.Json;
using Hase.Client.Configuration;
using Hase.Client.Grpc.Configuration;
using Hase.Runtime.Northbound;

namespace Hase.Client.Grpc.Tests.Configuration;

public sealed class PrivateNetworkRuntimeHostProfileRegistryEditorTests
{
    [Fact]
    public async Task AddFromHandoffAsync_ShouldAddDisabledProfileAndBackupPriorRegistry()
    {
        using TestFiles files = new();
        files.WriteRegistry(Host("first", "host-01", files.Configuration("first"), true));
        string handoff = await files.HandoffAsync("runtime-host-02");

        RuntimeHostId imported = await new PrivateNetworkRuntimeHostProfileRegistryEditor()
            .AddFromHandoffAsync(
                files.RegistryPath, files.BackupPath, handoff,
                new RuntimeHostProfileId("second"), "Second Host",
                files.Configuration("second"));

        PrivateNetworkRuntimeHostProfile second = (await files.LoadAsync()).Profiles[1];
        Assert.Equal("runtime-host-02", imported.Value);
        Assert.Equal("runtime-host-02", second.Profile.ExpectedRuntimeHostId.Value);
        Assert.False(second.Profile.IsEnabled);
        Assert.Single((await files.LoadAsync(files.BackupPath)).Profiles);
    }

    [Fact]
    public async Task AddFromHandoffAsync_Enabled_ShouldAddEnabledProfile()
    {
        using TestFiles files = new();
        files.WriteRegistry(Host("first", "host-01", files.Configuration("first"), true));
        string handoff = await files.HandoffAsync("runtime-host-02");

        await new PrivateNetworkRuntimeHostProfileRegistryEditor()
            .AddEnabledFromHandoffAsync(
                files.RegistryPath, files.BackupPath, handoff,
                new RuntimeHostProfileId("second"), "Second Host",
                files.Configuration("second"));

        PrivateNetworkRuntimeHostProfileRegistry registry = await files.LoadAsync();
        Assert.Equal(2, registry.Profiles.Count);
        Assert.All(registry.Profiles, profile => Assert.True(profile.Profile.IsEnabled));
    }

    [Fact]
    public async Task AddFromHandoffAsync_EnabledDuplicateIdentity_ShouldPreserveRegistry()
    {
        using TestFiles files = new();
        files.WriteRegistry(Host("first", "runtime-host-01", files.Configuration("first"), true));
        string original = File.ReadAllText(files.RegistryPath);
        string handoff = await files.HandoffAsync("runtime-host-01");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new PrivateNetworkRuntimeHostProfileRegistryEditor().AddEnabledFromHandoffAsync(
                files.RegistryPath, files.BackupPath, handoff,
                new RuntimeHostProfileId("second"), "Second",
                files.Configuration("second")));

        Assert.Equal(original, File.ReadAllText(files.RegistryPath));
        Assert.False(File.Exists(files.BackupPath));
    }

    [Fact]
    public async Task AddFromHandoffAsync_InvalidHandoff_ShouldPreserveRegistry()
    {
        using TestFiles files = new();
        files.WriteRegistry();
        string original = File.ReadAllText(files.RegistryPath);
        string handoff = Path.Combine(files.DirectoryPath, "handoff.json");
        File.WriteAllText(handoff, "{}");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new PrivateNetworkRuntimeHostProfileRegistryEditor().AddFromHandoffAsync(
                files.RegistryPath, files.BackupPath, handoff,
                new RuntimeHostProfileId("second"), "Second",
                files.Configuration("second")));

        Assert.Equal(original, File.ReadAllText(files.RegistryPath));
        Assert.False(File.Exists(files.BackupPath));
    }

    [Fact]
    public async Task AddFromHandoffAsync_DuplicateProfile_ShouldReject()
    {
        using TestFiles files = new();
        files.WriteRegistry(Host("first", "host-01", files.Configuration("first"), true));
        string handoff = await files.HandoffAsync("runtime-host-02");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new PrivateNetworkRuntimeHostProfileRegistryEditor().AddFromHandoffAsync(
                files.RegistryPath, files.BackupPath, handoff,
                new RuntimeHostProfileId("first"), "Duplicate",
                files.Configuration("duplicate")));
    }

    [Fact]
    public async Task AddFromHandoffAsync_MissingPrivateConfiguration_ShouldReject()
    {
        using TestFiles files = new();
        files.WriteRegistry();
        string handoff = await files.HandoffAsync("runtime-host-02");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            new PrivateNetworkRuntimeHostProfileRegistryEditor().AddFromHandoffAsync(
                files.RegistryPath, files.BackupPath, handoff,
                new RuntimeHostProfileId("second"), "Second",
                Path.Combine(files.DirectoryPath, "missing.json")));
    }

    [Fact]
    public async Task AddFromHandoffAsync_ShouldNotModifyHandoffOrPrivateConfiguration()
    {
        using TestFiles files = new();
        files.WriteRegistry();
        string handoff = await files.HandoffAsync("runtime-host-02");
        string configuration = files.Configuration("second");
        string handoffBefore = File.ReadAllText(handoff);
        string configurationBefore = File.ReadAllText(configuration);

        await new PrivateNetworkRuntimeHostProfileRegistryEditor().AddFromHandoffAsync(
            files.RegistryPath, files.BackupPath, handoff,
            new RuntimeHostProfileId("second"), "Second", configuration);

        Assert.Equal(handoffBefore, File.ReadAllText(handoff));
        Assert.Equal(configurationBefore, File.ReadAllText(configuration));
    }

    [Fact]
    public async Task AddAsync_ShouldPreserveOrderAndRetainPreviousRegistryBackup()
    {
        using TestFiles files = new();
        files.WriteRegistry(Host("first", "host-01", files.Configuration("first"), true));
        PrivateNetworkRuntimeHostProfile second = Profile(
            "second", "host-02", files.Configuration("second"), true);

        await new PrivateNetworkRuntimeHostProfileRegistryEditor().AddAsync(
            files.RegistryPath, files.BackupPath, second);

        PrivateNetworkRuntimeHostProfileRegistry registry = await files.LoadAsync();
        Assert.Equal(new[] { "first", "second" }, registry.Profiles.Select(ProfileId));
        Assert.True(File.Exists(files.BackupPath));
        Assert.Equal(new[] { "first" }, (await files.LoadAsync(files.BackupPath)).Profiles.Select(ProfileId));
    }

    [Fact]
    public async Task AddAsync_DuplicateProfileId_ShouldLeaveActiveRegistryUnchanged()
    {
        using TestFiles files = new();
        string configuration = files.Configuration("first");
        files.WriteRegistry(Host("first", "host-01", configuration, true));
        string original = File.ReadAllText(files.RegistryPath);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new PrivateNetworkRuntimeHostProfileRegistryEditor().AddAsync(
                files.RegistryPath,
                files.BackupPath,
                Profile("first", "host-02", files.Configuration("duplicate"), true)));

        Assert.Equal(original, File.ReadAllText(files.RegistryPath));
        Assert.False(File.Exists(files.BackupPath));
    }

    [Fact]
    public async Task AddAsync_MissingPrivateConfiguration_ShouldRejectBeforeReplacement()
    {
        using TestFiles files = new();
        files.WriteRegistry();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            new PrivateNetworkRuntimeHostProfileRegistryEditor().AddAsync(
                files.RegistryPath,
                files.BackupPath,
                Profile("missing", "host-01", Path.Combine(files.DirectoryPath, "missing.json"), true)));

        Assert.False(File.Exists(files.BackupPath));
    }

    [Fact]
    public async Task SetEnabledAsync_Disable_ShouldPreserveIdentityAndConfigurationReference()
    {
        using TestFiles files = new();
        string configuration = files.Configuration("first");
        files.WriteRegistry(Host("first", "host-01", configuration, true));

        await new PrivateNetworkRuntimeHostProfileRegistryEditor().SetEnabledAsync(
            files.RegistryPath, files.BackupPath, new RuntimeHostProfileId("first"), false);

        PrivateNetworkRuntimeHostProfile profile = Assert.Single((await files.LoadAsync()).Profiles);
        Assert.False(profile.Profile.IsEnabled);
        Assert.Equal("host-01", profile.Profile.ExpectedRuntimeHostId.Value);
        Assert.Equal(configuration, profile.PrivateNetworkConfigurationFilePath);
    }

    [Fact]
    public async Task SetEnabledAsync_ConflictingHostIdentity_ShouldRejectAndPreserveActiveRegistry()
    {
        using TestFiles files = new();
        files.WriteRegistry(
            Host("first", "host-01", files.Configuration("first"), true),
            Host("second", "host-01", files.Configuration("second"), false));
        string original = File.ReadAllText(files.RegistryPath);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new PrivateNetworkRuntimeHostProfileRegistryEditor().SetEnabledAsync(
                files.RegistryPath, files.BackupPath, new RuntimeHostProfileId("second"), true));

        Assert.Equal(original, File.ReadAllText(files.RegistryPath));
        Assert.False(File.Exists(files.BackupPath));
    }

    [Fact]
    public async Task RemoveAsync_ShouldNotDeleteReferencedPrivateConfiguration()
    {
        using TestFiles files = new();
        string configuration = files.Configuration("first");
        files.WriteRegistry(Host("first", "host-01", configuration, true));

        await new PrivateNetworkRuntimeHostProfileRegistryEditor().RemoveAsync(
            files.RegistryPath, files.BackupPath, new RuntimeHostProfileId("first"));

        Assert.Empty((await files.LoadAsync()).Profiles);
        Assert.True(File.Exists(configuration));
    }

    [Fact]
    public async Task RemoveAsync_UnknownProfile_ShouldRejectWithoutBackup()
    {
        using TestFiles files = new();
        files.WriteRegistry();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new PrivateNetworkRuntimeHostProfileRegistryEditor().RemoveAsync(
                files.RegistryPath, files.BackupPath, new RuntimeHostProfileId("missing")));

        Assert.False(File.Exists(files.BackupPath));
    }

    [Fact]
    public async Task ExistingBackup_ShouldRejectWithoutChangingActiveRegistry()
    {
        using TestFiles files = new();
        files.WriteRegistry();
        File.WriteAllText(files.BackupPath, "reserved");
        string original = File.ReadAllText(files.RegistryPath);

        await Assert.ThrowsAsync<IOException>(() =>
            new PrivateNetworkRuntimeHostProfileRegistryEditor().RemoveAsync(
                files.RegistryPath, files.BackupPath, new RuntimeHostProfileId("missing")));

        Assert.Equal(original, File.ReadAllText(files.RegistryPath));
        Assert.Equal("reserved", File.ReadAllText(files.BackupPath));
    }

    private static PrivateNetworkRuntimeHostProfile Profile(
        string id,
        string host,
        string configuration,
        bool enabled) =>
        new(
            new RuntimeHostProfile(
                new RuntimeHostProfileId(id),
                id,
                new RemoteRuntimeHostId(host),
                enabled),
            configuration);

    private static object Host(string id, string host, string configuration, bool enabled) => new
    {
        profileId = id,
        displayName = id,
        expectedRuntimeHostId = host,
        privateNetworkConfigurationFilePath = configuration,
        enabled
    };

    private static string ProfileId(PrivateNetworkRuntimeHostProfile profile) =>
        profile.Profile.ProfileId.Value;

    private sealed class TestFiles : IDisposable
    {
        public TestFiles()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "hase-43f2", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            RegistryPath = Path.Combine(DirectoryPath, "registry.json");
            BackupPath = Path.Combine(DirectoryPath, "registry.backup.json");
        }

        public string DirectoryPath { get; }
        public string RegistryPath { get; }
        public string BackupPath { get; }

        public string Configuration(string name)
        {
            string path = Path.Combine(DirectoryPath, name + ".json");
            File.WriteAllText(path, "{}");
            return path;
        }

        public void WriteRegistry(params object[] hosts) =>
            File.WriteAllText(
                RegistryPath,
                JsonSerializer.Serialize(
                    new { formatVersion = 1, hosts },
                    new JsonSerializerOptions { WriteIndented = true }));

        public Task<PrivateNetworkRuntimeHostProfileRegistry> LoadAsync(string? path = null) =>
            PrivateNetworkRuntimeHostProfileRegistryFile.LoadAsync(path ?? RegistryPath);

        public async Task<string> HandoffAsync(string runtimeHostId)
        {
            string path = Path.Combine(DirectoryPath, "handoff.json");
            await RuntimeHostOnboardingHandoffFile.CreateAsync(
                path, new RuntimeHostId(runtimeHostId));
            return path;
        }

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}

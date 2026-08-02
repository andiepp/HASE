using System.Text.Json;
using Hase.Client.Configuration;
using Hase.Client.Grpc.Configuration;

namespace Hase.Client.Grpc.Tests.Configuration;

public sealed class PrivateNetworkRuntimeHostProfileRegistryEditorTests
{
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

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}

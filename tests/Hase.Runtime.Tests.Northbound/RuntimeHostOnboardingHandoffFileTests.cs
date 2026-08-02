using System.IO;
using System.Text.Json;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostOnboardingHandoffFileTests
{
    [Fact]
    public async Task CreateAsync_ShouldRoundTripAuthoritativeIdentity()
    {
        using TestDirectory directory = new();
        string path = directory.File("handoff.json");

        await RuntimeHostOnboardingHandoffFile.CreateAsync(
            path, new RuntimeHostId("runtime-host-handoff-01"));

        RuntimeHostOnboardingHandoff handoff =
            await RuntimeHostOnboardingHandoffFile.LoadAsync(path);
        Assert.Equal("runtime-host-handoff-01", handoff.RuntimeHostId.Value);
    }

    [Fact]
    public async Task CreateAsync_ShouldContainOnlyVersionAndIdentity()
    {
        using TestDirectory directory = new();
        string path = directory.File("handoff.json");
        await RuntimeHostOnboardingHandoffFile.CreateAsync(
            path, new RuntimeHostId("runtime-host-handoff-01"));

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        string[] names = document.RootElement.EnumerateObject()
            .Select(property => property.Name).ToArray();
        Assert.Equal(new[] { "formatVersion", "runtimeHostId" }, names);
    }

    [Fact]
    public async Task CreateAsync_ExistingDestination_ShouldNotOverwrite()
    {
        using TestDirectory directory = new();
        string path = directory.File("handoff.json");
        File.WriteAllText(path, "preserved");

        await Assert.ThrowsAsync<IOException>(() =>
            RuntimeHostOnboardingHandoffFile.CreateAsync(
                path, new RuntimeHostId("runtime-host-handoff-01")));
        Assert.Equal("preserved", File.ReadAllText(path));
    }

    [Fact]
    public async Task LoadAsync_UnknownProperty_ShouldReject()
    {
        using TestDirectory directory = new();
        string path = directory.Write(
            "{\"formatVersion\":1,\"runtimeHostId\":\"runtime-host-01\",\"address\":\"hidden\"}");
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RuntimeHostOnboardingHandoffFile.LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_UnsupportedVersion_ShouldReject()
    {
        using TestDirectory directory = new();
        string path = directory.Write(
            "{\"formatVersion\":2,\"runtimeHostId\":\"runtime-host-01\"}");
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RuntimeHostOnboardingHandoffFile.LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_RelativePath_ShouldReject()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            RuntimeHostOnboardingHandoffFile.LoadAsync("handoff.json"));
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "hase-43g2", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public string File(string name) => System.IO.Path.Combine(Path, name);
        public string Write(string content)
        {
            string path = File("handoff.json");
            System.IO.File.WriteAllText(path, content);
            return path;
        }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

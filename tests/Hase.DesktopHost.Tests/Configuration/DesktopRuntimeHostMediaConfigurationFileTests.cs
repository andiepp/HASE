using System.IO;
using System.Text;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostMediaConfigurationFileTests
{
    [Fact]
    public async Task LoadAsync_ExactSource_ShouldKeepDeviceIdentitiesLocal()
    {
        using var document = await TemporaryDocument.CreateAsync(
            """
            {
              "formatVersion": 1,
              "sources": [
                {
                  "mediaSourceId": "primary-camera",
                  "mediaSourceGeneration": "generation-01",
                  "displayName": "Primary camera",
                  "videoDeviceId": "windows-video-device",
                  "audioDeviceId": "windows-audio-device"
                }
              ]
            }
            """);

        DesktopRuntimeHostMediaConfiguration configuration =
            await DesktopRuntimeHostMediaConfigurationFile.LoadAsync(
                document.FilePath);

        var source = Assert.Single(configuration.Sources);
        Assert.Equal("primary-camera", source.Target.MediaSourceId);
        Assert.Equal("generation-01", source.Target.MediaSourceGeneration);
        Assert.Equal("Primary camera", source.DisplayName);
        Assert.Equal("windows-video-device", source.VideoDeviceId);
        Assert.Equal("windows-audio-device", source.AudioDeviceId);
        Assert.True(source.SupportsAudio);
        Assert.DoesNotContain("windows-video-device", configuration.ToString());
    }

    [Fact]
    public async Task LoadAsync_OmittedAudio_ShouldCreateVideoOnlySource()
    {
        using var document = await TemporaryDocument.CreateAsync(
            ValidSource("camera", "generation"));

        DesktopRuntimeHostMediaConfiguration configuration =
            await DesktopRuntimeHostMediaConfigurationFile.LoadAsync(
                document.FilePath);

        Assert.False(Assert.Single(configuration.Sources).SupportsAudio);
    }

    [Theory]
    [InlineData("{\"formatVersion\":1,\"sources\":[]}")]
    [InlineData("{\"formatVersion\":2,\"sources\":[]}")]
    [InlineData("{\"formatVersion\":1,\"sources\":null}")]
    [InlineData("{\"formatVersion\":1,\"sources\":[],\"unexpected\":true}")]
    [InlineData("not-json")]
    public async Task LoadAsync_InvalidDocument_ShouldReject(string contents)
    {
        using var document = await TemporaryDocument.CreateAsync(contents);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DesktopRuntimeHostMediaConfigurationFile.LoadAsync(
                document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_DuplicateLogicalIdentity_ShouldReject()
    {
        string first = Source("camera", "first");
        string second = Source("camera", "second");
        using var document = await TemporaryDocument.CreateAsync(
            $"{{\"formatVersion\":1,\"sources\":[{first},{second}]}}");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DesktopRuntimeHostMediaConfigurationFile.LoadAsync(
                document.FilePath));
    }

    [Fact]
    public async Task LoadAsync_ToString_ShouldRedactAllDeviceIdentities()
    {
        using var document = await TemporaryDocument.CreateAsync(
            ValidSource("camera", "generation"));
        DesktopRuntimeHostMediaConfiguration configuration =
            await DesktopRuntimeHostMediaConfigurationFile.LoadAsync(
                document.FilePath);

        string text = configuration.ToString();
        Assert.DoesNotContain("video-device", text, StringComparison.Ordinal);
        Assert.DoesNotContain("audio-device", text, StringComparison.Ordinal);
    }

    private static string ValidSource(string id, string generation) =>
        $"{{\"formatVersion\":1,\"sources\":[{Source(id, generation)}]}}";

    private static string Source(string id, string generation) =>
        $"{{\"mediaSourceId\":\"{id}\","
        + $"\"mediaSourceGeneration\":\"{generation}\","
        + "\"displayName\":\"Camera\","
        + "\"videoDeviceId\":\"video-device\"}";

    private sealed class TemporaryDocument : IDisposable
    {
        private TemporaryDocument(string directoryPath, string filePath)
        {
            DirectoryPath = directoryPath;
            FilePath = filePath;
        }

        public string DirectoryPath { get; }
        public string FilePath { get; }

        public static async Task<TemporaryDocument> CreateAsync(string contents)
        {
            string directory = Path.Combine(Path.GetTempPath(),
                $"hase-media-configuration-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string filePath = Path.Combine(directory, "desktop-runtime-media.json");
            await File.WriteAllTextAsync(filePath, contents,
                new UTF8Encoding(false));
            return new TemporaryDocument(directory, filePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }
}

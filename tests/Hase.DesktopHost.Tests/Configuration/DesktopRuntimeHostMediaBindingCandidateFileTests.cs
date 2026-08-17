using System.IO;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostMediaBindingCandidateFileTests
{
    [Fact]
    public async Task WriteNewAsync_ShouldCreateLoadableSingleSourceDocument()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "candidate.json");
        var candidate = new DesktopRuntimeHostMediaBindingCandidate(
            "camera-01",
            "generation-01",
            "Primary camera",
            "opaque-video-id",
            "opaque-audio-id");

        await DesktopRuntimeHostMediaBindingCandidateFile.WriteNewAsync(
            path,
            candidate);
        DesktopRuntimeHostMediaConfiguration configuration =
            await DesktopRuntimeHostMediaConfigurationFile.LoadAsync(path);

        var source = Assert.Single(configuration.Sources);
        Assert.Equal("camera-01", source.Target.MediaSourceId);
        Assert.Equal("opaque-video-id", source.VideoDeviceId);
        Assert.Equal("opaque-audio-id", source.AudioDeviceId);
    }

    [Fact]
    public async Task WriteNewAsync_MultipleSources_ShouldPreserveLogicalOrder()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "candidate.json");
        var candidates = new[]
        {
            new DesktopRuntimeHostMediaBindingCandidate(
                "camera-01", "generation-01", "Camera 1", "video-1", null),
            new DesktopRuntimeHostMediaBindingCandidate(
                "camera-02", "generation-02", "Camera 2", "video-2", "audio")
        };

        await DesktopRuntimeHostMediaBindingCandidateFile.WriteNewAsync(
            path,
            candidates);
        DesktopRuntimeHostMediaConfiguration configuration =
            await DesktopRuntimeHostMediaConfigurationFile.LoadAsync(path);

        Assert.Collection(
            configuration.Sources,
            source =>
            {
                Assert.Equal("camera-01", source.Target.MediaSourceId);
                Assert.Equal("Camera 1", source.DisplayName);
                Assert.Null(source.AudioDeviceId);
            },
            source =>
            {
                Assert.Equal("camera-02", source.Target.MediaSourceId);
                Assert.Equal("Camera 2", source.DisplayName);
                Assert.Equal("audio", source.AudioDeviceId);
            });
    }

    [Fact]
    public async Task WriteNewAsync_MoreThanSixteenSources_ShouldReject()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "candidate.json");
        DesktopRuntimeHostMediaBindingCandidate[] candidates =
            Enumerable.Range(1, 17)
                .Select(index => new DesktopRuntimeHostMediaBindingCandidate(
                    $"camera-{index:D2}",
                    $"generation-{index:D2}",
                    $"Camera {index}",
                    $"video-{index}",
                    null))
                .ToArray();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            DesktopRuntimeHostMediaBindingCandidateFile.WriteNewAsync(
                path,
                candidates));
    }

    [Fact]
    public async Task WriteNewAsync_DuplicateVideoDevice_ShouldReject()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "candidate.json");
        var candidates = new[]
        {
            new DesktopRuntimeHostMediaBindingCandidate(
                "camera-01", "generation-01", "Camera 1", "same", null),
            new DesktopRuntimeHostMediaBindingCandidate(
                "camera-02", "generation-02", "Camera 2", "same", null)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            DesktopRuntimeHostMediaBindingCandidateFile.WriteNewAsync(
                path,
                candidates));
    }

    [Fact]
    public async Task WriteNewAsync_ExistingFile_ShouldNotOverwrite()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "candidate.json");
        await File.WriteAllTextAsync(path, "retained");
        var candidate = new DesktopRuntimeHostMediaBindingCandidate(
            "camera-01", "generation-01", "Camera", "video", null);

        await Assert.ThrowsAsync<IOException>(() =>
            DesktopRuntimeHostMediaBindingCandidateFile.WriteNewAsync(
                path,
                candidate));
        Assert.Equal("retained", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Candidate_MissingVideoDevice_ShouldReject(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new DesktopRuntimeHostMediaBindingCandidate(
                "camera-01",
                "generation-01",
                "Camera",
                value!,
                null));
    }

    [Fact]
    public void ToString_ShouldNotRevealDeviceIdentifiers()
    {
        var candidate = new DesktopRuntimeHostMediaBindingCandidate(
            "camera-01",
            "generation-01",
            "Camera",
            "secret-video-id",
            "secret-audio-id");

        Assert.DoesNotContain("secret-video-id", candidate.ToString());
        Assert.DoesNotContain("secret-audio-id", candidate.ToString());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"hase-media-binding-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}

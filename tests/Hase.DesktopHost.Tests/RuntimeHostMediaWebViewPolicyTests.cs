using Hase.DesktopHost.App.Media;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaWebViewPolicyTests
{
    private readonly RuntimeHostMediaWebViewPolicy policy = new();

    [Theory]
    [InlineData("https://hase-media.local/index.html")]
    [InlineData("https://hase-media.local/media.js")]
    [InlineData("https://HASE-MEDIA.LOCAL/media.css")]
    public void RepositoryOriginResourcesAreAllowed(string uri)
    {
        Assert.True(policy.IsAllowedResource(uri));
    }

    [Theory]
    [InlineData("http://hase-media.local/index.html")]
    [InlineData("https://example.test/media.js")]
    [InlineData("https://hase-media.local.example.test/media.js")]
    [InlineData("file:///C:/media/index.html")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public void ExternalOrActiveContentResourcesAreDenied(string uri)
    {
        Assert.False(policy.IsAllowedResource(uri));
    }

    [Fact]
    public void PermissionsAreDeniedBeforeExplicitCapture()
    {
        Assert.False(policy.IsPermissionAllowed(
            "https://hase-media.local/",
            RuntimeHostMediaPermissionKind.Camera));
        Assert.False(policy.IsPermissionAllowed(
            "https://hase-media.local/",
            RuntimeHostMediaPermissionKind.Microphone));
    }

    [Fact]
    public void VideoOnlyCaptureAllowsCameraButNotMicrophone()
    {
        policy.BeginCapture(includeAudio: false);

        Assert.True(policy.IsPermissionAllowed(
            "https://hase-media.local/",
            RuntimeHostMediaPermissionKind.Camera));
        Assert.False(policy.IsPermissionAllowed(
            "https://hase-media.local/",
            RuntimeHostMediaPermissionKind.Microphone));
    }

    [Fact]
    public void AudioCaptureAllowsBothOnlyAtFixedOrigin()
    {
        policy.BeginCapture(includeAudio: true);

        Assert.True(policy.IsPermissionAllowed(
            "https://hase-media.local/",
            RuntimeHostMediaPermissionKind.Camera));
        Assert.True(policy.IsPermissionAllowed(
            "https://hase-media.local/",
            RuntimeHostMediaPermissionKind.Microphone));
        Assert.False(policy.IsPermissionAllowed(
            "https://external.test/",
            RuntimeHostMediaPermissionKind.Camera));
        Assert.False(policy.IsPermissionAllowed(
            "https://hase-media.local/",
            RuntimeHostMediaPermissionKind.Other));
    }

    [Fact]
    public void EndCaptureRevokesAllPermissions()
    {
        policy.BeginCapture(includeAudio: true);
        policy.EndCapture();

        Assert.False(policy.IsPermissionAllowed(
            "https://hase-media.local/",
            RuntimeHostMediaPermissionKind.Camera));
        Assert.False(policy.IsPermissionAllowed(
            "https://hase-media.local/",
            RuntimeHostMediaPermissionKind.Microphone));
    }
}

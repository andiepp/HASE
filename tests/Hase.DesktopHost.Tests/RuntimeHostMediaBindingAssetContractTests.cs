using System.IO;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaBindingAssetContractTests
{
    private static readonly string Script = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "binding.js"));
    private static readonly string Html = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "binding.html"));
    private static readonly string ProductionScript = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "media.js"));

    [Fact]
    public void BindingPage_ShouldUseExplicitDiscoveryAndReleaseTracks()
    {
        Assert.Contains("discovery-requested", Script);
        Assert.Contains("discovery-authorized", Script);
        Assert.Contains("navigator.mediaDevices.enumerateDevices()", Script);
        Assert.Contains("navigator.mediaDevices.getUserMedia", Script);
        Assert.Contains("track.stop()", Script);
        Assert.Contains("selection-confirmed", Script);
        Assert.DoesNotContain("RTCPeerConnection", Script);
        Assert.DoesNotContain("fetch(", Script);
    }

    [Fact]
    public void BindingPage_ShouldShowMutedLocalPreviewAndReleaseItBeforeTerminalActions()
    {
        Assert.Contains("<video id=\"preview\" autoplay muted playsinline>", Html);
        Assert.Contains("preview.srcObject = stream", Script);
        Assert.Contains("preview.srcObject = null", Script);
        Assert.Contains("preview.pause()", Script);
        Assert.Contains("video: { deviceId: { exact: selectedVideoDeviceId } }", Script);
        Assert.Contains("audio: false", Script);

        int saveHandler = Script.IndexOf(
            "save.addEventListener",
            StringComparison.Ordinal);
        int cancelHandler = Script.IndexOf(
            "cancel.addEventListener",
            StringComparison.Ordinal);
        Assert.True(saveHandler >= 0);
        Assert.True(cancelHandler > saveHandler);
        Assert.Contains("stopTracks()", Script[saveHandler..cancelHandler]);
        Assert.Contains("stopTracks()", Script[cancelHandler..]);
    }

    [Fact]
    public void BindingPage_ShouldRetainClosedContentSecurityPolicy()
    {
        Assert.Contains("default-src 'none'", Html);
        Assert.Contains("connect-src 'none'", Html);
        Assert.Contains("media-src 'none'", Html);
        Assert.Contains("form-action 'none'", Html);
        Assert.DoesNotContain("http:", Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionCapturePage_ShouldStillNotEnumerateDevices()
    {
        Assert.DoesNotContain("enumerateDevices", ProductionScript);
        Assert.DoesNotContain("binding.js", ProductionScript);
    }
}

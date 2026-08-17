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
        Assert.Contains("selectedVideoDeviceIds = new Set()", Script);
        Assert.Contains("selectedCameras.map", Script);
        Assert.DoesNotContain("selectedOptions", Script);
        Assert.DoesNotContain("multiple size=", Html);
        Assert.DoesNotContain("RTCPeerConnection", Script);
        Assert.DoesNotContain("fetch(", Script);
    }

    [Fact]
    public void BindingPage_ShouldUseExplicitCameraCheckboxesAndAudioOptIn()
    {
        Assert.Contains("id=\"cameraChoices\"", Html);
        Assert.Contains("id=\"selectedCameraCount\"", Html);
        Assert.Contains("Selected cameras: 0", Html);
        Assert.Contains("checkbox.type = \"checkbox\"", Script);
        Assert.Contains("Selected cameras: ${count}", Script);
        Assert.Contains("selectedVideoDeviceIds.add(deviceId)", Script);
        Assert.Contains("selectedVideoDeviceIds.delete(deviceId)", Script);
        Assert.Contains("save.disabled = count < 1 || count > 16", Script);
        Assert.Contains(
            "<input id=\"discoverAudio\" type=\"checkbox\">",
            Html);
        Assert.DoesNotContain(
            "<input id=\"discoverAudio\" type=\"checkbox\" checked>",
            Html);
        Assert.Contains("audio.value = \"\"", Script);
        Assert.Contains("audio.disabled = !discoverAudio.checked", Script);
    }

    [Fact]
    public void BindingPage_ShouldShowMutedLocalPreviewAndReleaseItBeforeTerminalActions()
    {
        Assert.Contains("<video id=\"preview\" autoplay muted playsinline>", Html);
        Assert.Contains("preview.srcObject = stream", Script);
        Assert.Contains("preview.srcObject = null", Script);
        Assert.Contains("preview.pause()", Script);
        Assert.Contains("video: { deviceId: { exact: deviceId } }", Script);
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

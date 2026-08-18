using System.IO;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientWebRtcAssetContractTests
{
    private static readonly string Script = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "media.js"));
    private static readonly string Markup = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "index.html"));
    private static readonly string Styles = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "media.css"));

    [Fact]
    public void ClientIsDirectEncryptedReceiveOnlyAnswerer()
    {
        Assert.Contains("new RTCPeerConnection(peerConfiguration)", Script);
        Assert.Contains("iceServers: []", Script);
        Assert.Contains("rtcpMuxPolicy: \"require\"", Script);
        Assert.Contains("direction = \"recvonly\"", Script);
        Assert.Contains("RTCRtpReceiver.getCapabilities", Script);
        Assert.Contains("createAnswer()", Script);
        Assert.Contains("fingerprint:sha-256", Script);
        Assert.DoesNotContain("createDataChannel", Script);
        Assert.DoesNotContain("stun:", Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("turn:", Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClientNeverCapturesOrCreatesOutgoingTracks()
    {
        Assert.DoesNotContain("getUserMedia", Script);
        Assert.DoesNotContain("enumerateDevices", Script);
        Assert.DoesNotContain("peer.addTrack", Script);
        Assert.Contains("apply-negotiation", Script);
        Assert.Contains("ice-candidate", Script);
        Assert.Contains("ice-complete", Script);
    }

    [Fact]
    public void EveryPresentationIsMutedBeforePeerAndPlaybackCreation()
    {
        int audioSelection = Script.IndexOf(
            "includeAudio = message.includeAudio",
            StringComparison.Ordinal);
        int defaultMute = Script.IndexOf(
            "video.defaultMuted = true",
            audioSelection,
            StringComparison.Ordinal);
        int activeMute = Script.IndexOf(
            "video.muted = true",
            defaultMute,
            StringComparison.Ordinal);
        int peerCreation = Script.IndexOf(
            "new RTCPeerConnection(peerConfiguration)",
            StringComparison.Ordinal);
        int playback = Script.IndexOf(
            "video.play()",
            peerCreation,
            StringComparison.Ordinal);

        Assert.True(audioSelection >= 0);
        Assert.True(defaultMute > audioSelection);
        Assert.True(activeMute > defaultMute);
        Assert.True(peerCreation > activeMute);
        Assert.True(playback > peerCreation);
    }

    [Fact]
    public void RequestedAudioRequiresExplicitInPreviewActivation()
    {
        int click = Script.IndexOf(
            "enableAudio.addEventListener(\"click\"",
            StringComparison.Ordinal);
        int unmute = Script.IndexOf(
            "video.muted = false",
            click,
            StringComparison.Ordinal);
        int playback = Script.IndexOf(
            "video.play()",
            unmute,
            StringComparison.Ordinal);

        Assert.Contains("id=\"audio-activation-panel\"", Markup);
        Assert.Contains("id=\"enable-audio\"", Markup);
        Assert.Contains("Enable Audio", Markup);
        Assert.Contains("#audio-activation-panel[hidden]", Styles);
        Assert.True(click >= 0);
        Assert.True(unmute > click);
        Assert.True(playback > unmute);
    }

    [Fact]
    public void PresentationSubresourcesUseCurrentAssetVersion()
    {
        Assert.Contains("href=\"media.css?v=55f4c16\"", Markup);
        Assert.Contains("src=\"media.js?v=55f4c16\"", Markup);
        Assert.DoesNotContain("href=\"media.css\"", Markup);
        Assert.DoesNotContain("src=\"media.js\"", Markup);
    }

    [Fact]
    public void BlockedAudioActivationIsRetryableAndObservable()
    {
        int click = Script.IndexOf(
            "enableAudio.addEventListener(\"click\"",
            StringComparison.Ordinal);
        int clear = Script.IndexOf(
            "const clear = (notify = true) =>",
            StringComparison.Ordinal);
        string activation = Script[click..clear];

        Assert.Contains("video.muted = true", activation);
        Assert.Contains("audioActivationPanel.hidden = false", activation);
        Assert.Contains(
            "send(\"audio-activation-blocked\", \"playback-blocked\")",
            activation);
        Assert.DoesNotContain("presentation-faulted", activation);
    }

    [Fact]
    public void PresentationCleanupRestoresMutedDefault()
    {
        int clearStart = Script.IndexOf(
            "const clear = (notify = true) =>",
            StringComparison.Ordinal);
        int beginStart = Script.IndexOf(
            "const begin = (message) =>",
            StringComparison.Ordinal);
        Assert.True(clearStart >= 0);
        Assert.True(beginStart > clearStart);

        string clear = Script[clearStart..beginStart];

        Assert.Contains("video.defaultMuted = true", clear);
        Assert.Contains("video.muted = true", clear);
        Assert.Contains("video.srcObject = null", clear);
        Assert.Contains("resetAudioActivation()", clear);
    }
}

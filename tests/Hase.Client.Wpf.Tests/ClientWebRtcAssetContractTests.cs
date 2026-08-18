using System.IO;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientWebRtcAssetContractTests
{
    private static readonly string Script = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "media.js"));

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
    public void SilentPresentationIsMutedBeforePeerAndPlaybackCreation()
    {
        int audioSelection = Script.IndexOf(
            "includeAudio = message.includeAudio",
            StringComparison.Ordinal);
        int defaultMute = Script.IndexOf(
            "video.defaultMuted = !includeAudio",
            StringComparison.Ordinal);
        int activeMute = Script.IndexOf(
            "video.muted = !includeAudio",
            StringComparison.Ordinal);
        int peerCreation = Script.IndexOf(
            "new RTCPeerConnection(peerConfiguration)",
            StringComparison.Ordinal);
        int playback = Script.IndexOf(
            "video.play()",
            StringComparison.Ordinal);

        Assert.True(audioSelection >= 0);
        Assert.True(defaultMute > audioSelection);
        Assert.True(activeMute > defaultMute);
        Assert.True(peerCreation > activeMute);
        Assert.True(playback > peerCreation);
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
    }
}

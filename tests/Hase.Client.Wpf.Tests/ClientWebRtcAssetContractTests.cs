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
}

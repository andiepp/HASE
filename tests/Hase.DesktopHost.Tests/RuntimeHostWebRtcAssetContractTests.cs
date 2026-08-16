using System.IO;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostWebRtcAssetContractTests
{
    private static readonly string Script = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "media.js"));

    [Fact]
    public void RuntimeHostIsDirectEncryptedSendOnlyOfferer()
    {
        Assert.Contains("new RTCPeerConnection(peerConfiguration)", Script);
        Assert.Contains("iceServers: []", Script);
        Assert.Contains("rtcpMuxPolicy: \"require\"", Script);
        Assert.Contains("direction: \"sendonly\"", Script);
        Assert.Contains("RTCRtpSender.getCapabilities", Script);
        Assert.Contains("createOffer()", Script);
        Assert.Contains("fingerprint:sha-256", Script);
        Assert.DoesNotContain("createDataChannel", Script);
        Assert.DoesNotContain("stun:", Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("turn:", Script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeHostUsesExactLocalCaptureAndBoundedBridgeKinds()
    {
        Assert.Contains("navigator.mediaDevices.getUserMedia", Script);
        Assert.Contains("deviceId: { exact: message.videoDeviceId }", Script);
        Assert.Contains("apply-negotiation", Script);
        Assert.Contains("ice-candidate", Script);
        Assert.Contains("ice-complete", Script);
        Assert.DoesNotContain("enumerateDevices", Script);
    }
}

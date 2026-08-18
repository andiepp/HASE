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
            "audio.muted = false",
            click,
            StringComparison.Ordinal);
        int playback = Script.IndexOf(
            "audio.play()",
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
        Assert.Contains("href=\"media.css?v=55f4c17\"", Markup);
        Assert.Contains("src=\"media.js?v=55f4c17\"", Markup);
        Assert.DoesNotContain("href=\"media.css\"", Markup);
        Assert.DoesNotContain("src=\"media.js\"", Markup);
    }

    [Fact]
    public void VideoPlaybackIsParserMutedAndAudioUsesDedicatedElement()
    {
        Assert.Contains(
            "<video id=\"presentation\" autoplay muted playsinline>",
            Markup);
        Assert.Contains(
            "<audio id=\"audio-presentation\" preload=\"none\"></audio>",
            Markup);
        Assert.Contains("#audio-presentation", Styles);
        Assert.Contains("const audio = document.getElementById", Script);
        Assert.Contains("remoteVideoStream = new MediaStream()", Script);
        Assert.Contains("remoteAudioStream = new MediaStream()", Script);
    }

    [Fact]
    public void AudioTrackArrivalCannotStartVideoPlayback()
    {
        int audioBranch = Script.IndexOf(
            "if (event.track.kind === \"audio\")",
            StringComparison.Ordinal);
        int videoBranch = Script.IndexOf(
            "if (event.track.kind !== \"video\")",
            audioBranch,
            StringComparison.Ordinal);
        int videoPlayback = Script.IndexOf(
            "void video.play()",
            videoBranch,
            StringComparison.Ordinal);
        string audioHandling = Script[audioBranch..videoBranch];

        Assert.True(audioBranch >= 0);
        Assert.True(videoBranch > audioBranch);
        Assert.True(videoPlayback > videoBranch);
        Assert.Contains("remoteAudioStream.addTrack(event.track)",
            audioHandling);
        Assert.Contains("offerAudioActivation()", audioHandling);
        Assert.Contains("return;", audioHandling);
        Assert.DoesNotContain("video.play()", audioHandling);
    }

    [Fact]
    public void EitherTrackArrivalOrderOffersAudioOnlyAfterVideoStarts()
    {
        int activationGuard = Script.IndexOf(
            "includeAudio && presentationStarted && !audioActivated",
            StringComparison.Ordinal);
        int audioTrack = Script.IndexOf(
            "remoteAudioStream.addTrack(event.track)",
            StringComparison.Ordinal);
        int audioOffer = Script.IndexOf(
            "offerAudioActivation()",
            audioTrack,
            StringComparison.Ordinal);
        int videoStarted = Script.IndexOf(
            "presentationStarted = true",
            audioOffer,
            StringComparison.Ordinal);
        int videoOffer = Script.IndexOf(
            "offerAudioActivation()",
            videoStarted,
            StringComparison.Ordinal);

        Assert.True(activationGuard >= 0);
        Assert.True(audioTrack >= 0);
        Assert.True(audioOffer > audioTrack);
        Assert.True(videoStarted > audioOffer);
        Assert.True(videoOffer > videoStarted);
    }

    [Fact]
    public void AudioActivationWaitsForStartedVideoAndUsesAudioElement()
    {
        Assert.Contains(
            "includeAudio && presentationStarted && !audioActivated",
            Script);
        Assert.Contains("audio.srcObject = remoteAudioStream", Script);
        Assert.Contains("audio.muted = false", Script);
        Assert.Contains("void audio.play()", Script);
        Assert.DoesNotContain("video.muted = false", Script);
    }

    [Fact]
    public void PresentationCleanupReleasesSplitStreams()
    {
        Assert.Contains("remoteVideoStream.getTracks()", Script);
        Assert.Contains("remoteAudioStream.getTracks()", Script);
        Assert.Contains("remoteVideoStream = null", Script);
        Assert.Contains("remoteAudioStream = null", Script);
        Assert.Contains("audio.pause()", Script);
        Assert.Contains("audio.srcObject = null", Script);
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

        Assert.Contains("audio.muted = true", activation);
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

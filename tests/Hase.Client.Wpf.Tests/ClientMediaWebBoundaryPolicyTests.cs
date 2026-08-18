using Hase.Client.Wpf.AppHost.Media;
using Hase.Client.Media;
using System.Text.Json;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientMediaWebBoundaryPolicyTests
{
    [Theory]
    [InlineData("https://hase-media-client.local/index.html?v=55f4c17")]
    [InlineData("https://hase-media-client.local/media.js?v=55f4c17")]
    public void FixedLocalResourcesAreAllowed(string uri)
    {
        Assert.True(new ClientMediaWebViewPolicy().IsAllowedResource(uri));
    }

    [Theory]
    [InlineData("http://hase-media-client.local/index.html")]
    [InlineData("https://example.test/media.js")]
    [InlineData("file:///C:/media/index.html")]
    [InlineData("javascript:alert(1)")]
    public void ExternalResourcesAreDenied(string uri)
    {
        Assert.False(new ClientMediaWebViewPolicy().IsAllowedResource(uri));
    }

    [Fact]
    public void ViewingClientNeverGrantsMediaOrOtherBrowserPermission()
    {
        Assert.False(new ClientMediaWebViewPolicy().IsPermissionAllowed());
    }

    [Fact]
    public void ApplicationNavigationUsesCurrentAssetVersion()
    {
        Assert.Equal("55f4c17", ClientMediaWebViewPolicy.AssetVersion);
        Assert.Equal(
            "https://hase-media-client.local/index.html?v=55f4c17",
            ClientMediaWebViewPolicy.ApplicationUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("{\"version\":1,\"kind\":\"ready\"}", ClientMediaWebMessageKind.Ready)]
    [InlineData("{\"version\":1,\"kind\":\"presentation-started\"}", ClientMediaWebMessageKind.PresentationStarted)]
    [InlineData("{\"version\":1,\"kind\":\"presentation-stopped\"}", ClientMediaWebMessageKind.PresentationStopped)]
    public void FixedPresentationEventsAreAccepted(
        string json,
        ClientMediaWebMessageKind expected)
    {
        Assert.True(new ClientMediaWebMessageValidator()
            .TryValidate(json, out var message));
        Assert.Equal(expected, message!.Kind);
    }

    [Theory]
    [InlineData("{\"version\":2,\"kind\":\"ready\"}")]
    [InlineData("{\"version\":\"1\",\"kind\":\"ready\"}")]
    [InlineData("{\"version\":1,\"kind\":\"ready\",\"deviceId\":\"secret\"}")]
    [InlineData("{\"version\":1,\"kind\":\"presentation-faulted\",\"failureCode\":\"driver detail\"}")]
    [InlineData("{\"version\":1,\"kind\":\"unknown\"}")]
    public void ExpandedOrSensitiveEventsAreRejected(string json)
    {
        Assert.False(new ClientMediaWebMessageValidator()
            .TryValidate(json, out _));
    }

    [Fact]
    public void ClientAnswerIsAcceptedAsSensitiveNegotiation()
    {
        var json = JsonSerializer.Serialize(new
        {
            version = 1,
            kind = "negotiation",
            sequence = 1,
            negotiationKind = "answer",
            sensitivePayload = "v=0\r\na=fingerprint:sha-256 AA:BB\r\n"
        });

        Assert.True(new ClientMediaWebMessageValidator()
            .TryValidate(json, out var message));
        Assert.Equal(ClientMediaWebMessageKind.Negotiation, message!.Kind);
        Assert.Equal(RemoteMediaNegotiationKind.Answer,
            message.NegotiationMessage!.Kind);
    }

    [Theory]
    [InlineData(0, "answer", "sdp")]
    [InlineData(1, "offer", "sdp")]
    [InlineData(1, "ice-complete", "not-empty")]
    [InlineData(1, "ice-candidate", "")]
    public void InvalidClientNegotiationIsRejected(
        uint sequence,
        string negotiationKind,
        string sensitivePayload)
    {
        var json = JsonSerializer.Serialize(new
        {
            version = 1,
            kind = "negotiation",
            sequence,
            negotiationKind,
            sensitivePayload
        });

        Assert.False(new ClientMediaWebMessageValidator()
            .TryValidate(json, out _));
    }

    [Theory]
    [InlineData("negotiation-rejected")]
    [InlineData("codec-unsupported")]
    public void SanitizedPeerFailureIsAccepted(string failureCode)
    {
        var json = $$"""
            {"version":1,"kind":"presentation-faulted","failureCode":"{{failureCode}}"}
            """;

        Assert.True(new ClientMediaWebMessageValidator()
            .TryValidate(json, out var message));
        Assert.Equal(failureCode, message!.FailureCode);
    }

    [Fact]
    public void AudioActivationBlockIsAcceptedOnlyAsPlaybackBlock()
    {
        const string valid = """
            {"version":1,"kind":"audio-activation-blocked","failureCode":"playback-blocked"}
            """;
        const string invalid = """
            {"version":1,"kind":"audio-activation-blocked","failureCode":"browser-failed"}
            """;

        Assert.True(new ClientMediaWebMessageValidator()
            .TryValidate(valid, out var message));
        Assert.Equal(ClientMediaWebMessageKind.AudioActivationBlocked,
            message!.Kind);
        Assert.Equal("playback-blocked", message.FailureCode);
        Assert.False(new ClientMediaWebMessageValidator()
            .TryValidate(invalid, out _));
    }
}

using Hase.DesktopHost.App.Media;
using Hase.Runtime.Media;
using System.Text.Json;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaWebMessageValidatorTests
{
    private readonly RuntimeHostMediaWebMessageValidator validator = new();

    [Theory]
    [InlineData("{\"version\":1,\"kind\":\"ready\"}", RuntimeHostMediaWebMessageKind.Ready)]
    [InlineData("{\"version\":1,\"kind\":\"capture-started\"}", RuntimeHostMediaWebMessageKind.CaptureStarted)]
    [InlineData("{\"version\":1,\"kind\":\"capture-stopped\"}", RuntimeHostMediaWebMessageKind.CaptureStopped)]
    public void KnownLifecycleMessageIsAccepted(
        string json,
        RuntimeHostMediaWebMessageKind expected)
    {
        Assert.True(validator.TryValidate(json, out var message));
        Assert.Equal(expected, message!.Kind);
        Assert.Null(message.FailureCode);
    }

    [Theory]
    [InlineData("device-unavailable")]
    [InlineData("device-busy")]
    [InlineData("permission-denied")]
    [InlineData("constraint-rejected")]
    [InlineData("browser-failed")]
    public void EnumeratedFailureIsAccepted(string failureCode)
    {
        var json = $$"""
            {"version":1,"kind":"capture-faulted","failureCode":"{{failureCode}}"}
            """;

        Assert.True(validator.TryValidate(json, out var message));
        Assert.Equal(failureCode, message!.FailureCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"version\":2,\"kind\":\"ready\"}")]
    [InlineData("{\"version\":1,\"kind\":\"unknown\"}")]
    [InlineData("{\"version\":1,\"kind\":\"ready\",\"url\":\"https://example.test\"}")]
    [InlineData("{\"version\":1,\"kind\":\"ready\",\"deviceId\":\"secret\"}")]
    [InlineData("{\"version\":1,\"kind\":\"capture-faulted\",\"failureCode\":\"driver said secret\"}")]
    [InlineData("{\"version\":1,\"kind\":\"capture-started\",\"failureCode\":\"browser-failed\"}")]
    public void MalformedOrExpandedEnvelopeIsRejected(string? json)
    {
        Assert.False(validator.TryValidate(json, out var message));
        Assert.Null(message);
    }

    [Fact]
    public void OversizedEnvelopeIsRejected()
    {
        var json = new string('x',
            RuntimeHostMediaWebMessageValidator.MaximumMessageUtf8Bytes + 1);

        Assert.False(validator.TryValidate(json, out _));
    }

    [Fact]
    public void HostOfferIsAcceptedAsSensitiveNegotiation()
    {
        var json = JsonSerializer.Serialize(new
        {
            version = 1,
            kind = "negotiation",
            sequence = 1,
            negotiationKind = "offer",
            sensitivePayload = "v=0\r\na=fingerprint:sha-256 AA:BB\r\n"
        });

        Assert.True(validator.TryValidate(json, out var message));
        Assert.Equal(RuntimeHostMediaWebMessageKind.Negotiation, message!.Kind);
        Assert.Equal(RuntimeHostMediaNegotiationKind.Offer,
            message.NegotiationMessage!.Kind);
        Assert.Equal((uint)1, message.NegotiationMessage.Sequence);
    }

    [Theory]
    [InlineData(0, "offer", "sdp")]
    [InlineData(1, "answer", "sdp")]
    [InlineData(1, "ice-complete", "not-empty")]
    [InlineData(1, "ice-candidate", "")]
    public void InvalidHostNegotiationIsRejected(
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

        Assert.False(validator.TryValidate(json, out _));
    }

    [Fact]
    public void NegotiationPropertiesCannotExpandLifecycleMessage()
    {
        const string json =
            "{\"version\":1,\"kind\":\"ready\",\"sequence\":1," +
            "\"negotiationKind\":\"offer\",\"sensitivePayload\":\"sdp\"}";

        Assert.False(validator.TryValidate(json, out _));
    }

    [Theory]
    [InlineData("peer-connected", null, RuntimeHostMediaWebMessageKind.PeerConnected)]
    [InlineData("peer-faulted", "transport-failed", RuntimeHostMediaWebMessageKind.PeerFaulted)]
    public void PeerLifecycleIsSanitized(
        string kind,
        string? failureCode,
        RuntimeHostMediaWebMessageKind expected)
    {
        var json = failureCode is null
            ? $$"""{"version":1,"kind":"{{kind}}"}"""
            : $$"""{"version":1,"kind":"{{kind}}","failureCode":"{{failureCode}}"}""";

        Assert.True(validator.TryValidate(json, out var message));
        Assert.Equal(expected, message!.Kind);
    }
}

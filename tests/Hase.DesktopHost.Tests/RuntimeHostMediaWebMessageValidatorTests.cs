using Hase.DesktopHost.App.Media;

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
}

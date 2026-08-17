using Hase.DesktopHost.App.Media;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaBindingWebMessageValidatorTests
{
    private readonly RuntimeHostMediaBindingWebMessageValidator validator =
        new();

    [Theory]
    [InlineData("ready", RuntimeHostMediaBindingWebMessageKind.Ready)]
    [InlineData("discovery-requested", RuntimeHostMediaBindingWebMessageKind.DiscoveryRequested)]
    [InlineData("cancelled", RuntimeHostMediaBindingWebMessageKind.Cancelled)]
    public void BoundedLifecycleMessage_ShouldSucceed(
        string kind,
        RuntimeHostMediaBindingWebMessageKind expected)
    {
        Assert.True(validator.TryValidate(
            $$"""{"version":1,"kind":"{{kind}}"}""",
            out var message));
        Assert.Equal(expected, message!.Kind);
    }

    [Fact]
    public void ExactSelection_ShouldSucceed()
    {
        const string json =
            "{\"version\":1,\"kind\":\"selection-confirmed\"," +
            "\"videoDeviceId\":\"opaque-video\"," +
            "\"audioDeviceId\":\"opaque-audio\"}";

        Assert.True(validator.TryValidate(json, out var message));
        Assert.Equal("opaque-video", message!.VideoDeviceId);
        Assert.Equal("opaque-audio", message.AudioDeviceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"version\":2,\"kind\":\"ready\"}")]
    [InlineData("{\"version\":1,\"kind\":\"selection-confirmed\"}")]
    [InlineData("{\"version\":1,\"kind\":\"ready\",\"videoDeviceId\":\"secret\"}")]
    [InlineData("{\"version\":1,\"kind\":\"faulted\",\"failureCode\":\"driver-secret\"}")]
    [InlineData("{\"version\":1,\"kind\":\"ready\",\"url\":\"https://example.test\"}")]
    public void ExpandedOrMalformedMessage_ShouldReject(string? json)
    {
        Assert.False(validator.TryValidate(json, out var message));
        Assert.Null(message);
    }

    [Theory]
    [InlineData("device-unavailable")]
    [InlineData("permission-denied")]
    [InlineData("enumeration-failed")]
    [InlineData("browser-failed")]
    public void SanitizedFailure_ShouldSucceed(string code)
    {
        Assert.True(validator.TryValidate(
            $$"""{"version":1,"kind":"faulted","failureCode":"{{code}}"}""",
            out var message));
        Assert.Equal(code, message!.FailureCode);
    }
}

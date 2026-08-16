using Hase.Client.Wpf.AppHost.Media;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientMediaWebBoundaryPolicyTests
{
    [Theory]
    [InlineData("https://hase-media-client.local/index.html")]
    [InlineData("https://hase-media-client.local/media.js")]
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
}

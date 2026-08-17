using Hase.DesktopHost.App.Media;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaBindingWebViewPolicyTests
{
    private readonly RuntimeHostMediaBindingWebViewPolicy policy = new();

    [Theory]
    [InlineData("https://hase-media.local/binding.html")]
    [InlineData("https://hase-media.local/binding.css")]
    [InlineData("https://hase-media.local/binding.js")]
    public void ExactBindingAsset_ShouldBeAllowed(string uri)
    {
        Assert.True(policy.IsAllowedResource(uri));
    }

    [Theory]
    [InlineData("https://hase-media.local/index.html")]
    [InlineData("https://hase-media.local/media.js")]
    [InlineData("https://hase-media.local/binding.html?expanded=true")]
    [InlineData("https://example.test/binding.html")]
    [InlineData("http://hase-media.local/binding.html")]
    public void OtherResource_ShouldBeDenied(string uri)
    {
        Assert.False(policy.IsAllowedResource(uri));
    }

    [Fact]
    public void Permission_ShouldRequireExplicitArmingAndEndImmediately()
    {
        const string origin = "https://hase-media.local/";
        Assert.False(policy.IsPermissionAllowed(
            origin,
            RuntimeHostMediaPermissionKind.Camera));

        policy.ArmDiscovery();
        Assert.True(policy.IsPermissionAllowed(
            origin,
            RuntimeHostMediaPermissionKind.Camera));
        Assert.True(policy.IsPermissionAllowed(
            origin,
            RuntimeHostMediaPermissionKind.Microphone));
        Assert.False(policy.IsPermissionAllowed(
            origin,
            RuntimeHostMediaPermissionKind.Other));

        policy.EndDiscovery();
        Assert.False(policy.IsPermissionAllowed(
            origin,
            RuntimeHostMediaPermissionKind.Camera));
    }
}

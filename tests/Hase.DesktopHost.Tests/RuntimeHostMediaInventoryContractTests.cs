using System.IO;
using Hase.DesktopHost.App.Media;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaInventoryContractTests
{
    private static readonly string Script = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "inventory.js"));
    private static readonly string Html = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Media", "Assets", "inventory.html"));

    [Fact]
    public void InventoryAssetEnumeratesAndDebouncesWithoutOpeningCapture()
    {
        Assert.Contains("enumerateDevices()", Script);
        Assert.Contains("devicechange", Script);
        Assert.Contains("debounceMilliseconds = 250", Script);
        Assert.Contains("maximumSources = 16", Script);
        Assert.Contains(
            "failed observation is not an authoritative empty",
            Script);
        Assert.DoesNotContain("getUserMedia", Script);
        Assert.DoesNotContain("RTCPeerConnection", Script);
        Assert.DoesNotContain("fetch(", Script);
    }

    [Fact]
    public void InventoryAssetRetainsClosedContentSecurityPolicy()
    {
        Assert.Contains("default-src 'none'", Html);
        Assert.Contains("connect-src 'none'", Html);
        Assert.Contains("media-src 'none'", Html);
        Assert.DoesNotContain("http:", Html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            RuntimeHostMediaWebViewPolicy.VirtualHostName,
            RuntimeHostMediaInventoryWebViewPolicy.VirtualHostName);
    }

    [Fact]
    public void ValidatorAcceptsOnlyBoundedUniqueVideoIdentities()
    {
        var validator = new RuntimeHostMediaInventoryWebMessageValidator();

        Assert.True(validator.TryValidate(
            "{\"version\":1,\"kind\":\"inventory\",\"devices\":[{\"deviceId\":\"one\"}]}",
            out var observations));
        Assert.Equal("one", Assert.Single(observations!).VideoDeviceId);
        Assert.False(validator.TryValidate(
            "{\"version\":1,\"kind\":\"inventory\",\"devices\":[{\"deviceId\":\"one\"},{\"deviceId\":\"one\"}]}",
            out _));
        Assert.False(validator.TryValidate(
            "{\"version\":1,\"kind\":\"inventory\",\"devices\":[],\"label\":\"raw\"}",
            out _));
    }
}

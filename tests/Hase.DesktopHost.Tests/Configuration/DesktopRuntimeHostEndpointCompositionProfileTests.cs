using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostEndpointCompositionProfileTests
{
    [Fact]
    public void Constructor_CurrentPhysicalComposition_ShouldPreserveEndpoints()
    {
        var native = new DesktopRuntimeHostNativeNetworkEndpointProfile("native-01", "device.local", 5000);
        var compact = new DesktopRuntimeHostCompactSerialEndpointProfile(
            "compact-01", 0x2341, 0x0043, 115200, TimeSpan.FromSeconds(3));

        var profile = new DesktopRuntimeHostEndpointCompositionProfile([native], [compact]);

        Assert.Equal(native, Assert.Single(profile.NativeNetworkEndpoints));
        Assert.Equal(compact, Assert.Single(profile.CompactSerialEndpoints));
    }

    [Fact]
    public void Constructor_DuplicateExpectedIdentity_ShouldReject()
    {
        var native = new DesktopRuntimeHostNativeNetworkEndpointProfile("endpoint-01", "device.local", 5000);
        var compact = new DesktopRuntimeHostCompactSerialEndpointProfile(
            "endpoint-01", 0x2341, 0x0043, 115200, TimeSpan.FromSeconds(3));

        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostEndpointCompositionProfile([native], [compact]));
    }

    [Fact]
    public void Constructor_MoreThan64Endpoints_ShouldReject()
    {
        DesktopRuntimeHostEndpointEntry[] entries = Enumerable.Range(1, 65)
            .Select(index => new DesktopRuntimeHostEndpointEntry(
                "someone-elses-instrument", $"foreign-{index}"))
            .ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DesktopRuntimeHostEndpointCompositionProfile(entries));
    }

    [Fact]
    public void Constructor_EmptyComposition_ShouldReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DesktopRuntimeHostEndpointCompositionProfile([], []));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void NativeNetwork_InvalidPort_ShouldReject(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DesktopRuntimeHostNativeNetworkEndpointProfile("native-01", "device.local", port));
    }

    [Fact]
    public void CompactSerial_InvalidTimeout_ShouldReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DesktopRuntimeHostCompactSerialEndpointProfile(
                "compact-01", 0x2341, 0x0043, 115200, TimeSpan.Zero));
    }
}

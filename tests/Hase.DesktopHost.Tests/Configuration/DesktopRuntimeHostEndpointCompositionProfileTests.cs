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
        Assert.Empty(profile.Kel103SerialEndpoints);
    }

    [Fact]
    public void Constructor_Kel103Composition_ShouldPreserveEndpoint()
    {
        var kel103 = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "kel-01", "korad-kel103", 2, "external-target", 115200);

        var profile = new DesktopRuntimeHostEndpointCompositionProfile([], [], [kel103]);

        Assert.Equal(kel103, Assert.Single(profile.Kel103SerialEndpoints));
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
    public void Constructor_DuplicateExpectedIdentityAcrossCompactAndKel103_ShouldReject()
    {
        var compact = new DesktopRuntimeHostCompactSerialEndpointProfile(
            "endpoint-01", 0x2341, 0x0043, 115200, TimeSpan.FromSeconds(3));
        var kel103 = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "endpoint-01", "korad-kel103", 2, "external-target", 115200);

        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostEndpointCompositionProfile([], [compact], [kel103]));
    }

    [Fact]
    public void Constructor_MoreThan64EndpointsAcrossFamilies_ShouldReject()
    {
        DesktopRuntimeHostKel103SerialEndpointProfile[] kel103Endpoints = Enumerable.Range(1, 64)
            .Select(index => new DesktopRuntimeHostKel103SerialEndpointProfile(
                $"kel-{index}", "korad-kel103", 2, $"external-target-{index}", 115200))
            .ToArray();
        var native = new DesktopRuntimeHostNativeNetworkEndpointProfile("native-01", "device.local", 5000);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DesktopRuntimeHostEndpointCompositionProfile([native], [], kel103Endpoints));
    }

    [Fact]
    public void Kel103Serial_Values_ShouldNormalizeAndPreserveDefinitionReference()
    {
        var endpoint = new DesktopRuntimeHostKel103SerialEndpointProfile(
            " kel-01 ", " korad-kel103 ", 2, " external-target ", 115200);

        Assert.Equal("kel-01", endpoint.ExpectedEndpointId);
        Assert.Equal("korad-kel103", endpoint.DefinitionReference.Id.Value);
        Assert.Equal((ushort)2, endpoint.DefinitionReference.Version);
        Assert.Equal("external-target", endpoint.SerialPort);
        Assert.DoesNotContain("external-target", endpoint.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(9600)]
    [InlineData(0)]
    public void Kel103Serial_UnsupportedBaudRate_ShouldReject(int baudRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DesktopRuntimeHostKel103SerialEndpointProfile(
                "kel-01", "korad-kel103", 2, "external-target", baudRate));
    }

    [Fact]
    public void Constructor_RfLabComposition_ShouldPreserveEndpoint()
    {
        var rfLab = new DesktopRuntimeHostRfLabSerialEndpointProfile(
            "rflab-01", "rflab-signal-lab", 1, "external-target", 115200);

        var profile = new DesktopRuntimeHostEndpointCompositionProfile([], [], [], [rfLab]);

        Assert.Equal(rfLab, Assert.Single(profile.RfLabSerialEndpoints));
        Assert.Empty(profile.Kel103SerialEndpoints);
    }

    [Fact]
    public void Constructor_ThreeArgumentComposition_ShouldLeaveRfLabEmpty()
    {
        var kel103 = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "kel-01", "korad-kel103", 2, "external-target", 115200);

        var profile = new DesktopRuntimeHostEndpointCompositionProfile([], [], [kel103]);

        Assert.Empty(profile.RfLabSerialEndpoints);
    }

    [Fact]
    public void Constructor_DuplicateExpectedIdentityAcrossKel103AndRfLab_ShouldReject()
    {
        var kel103 = new DesktopRuntimeHostKel103SerialEndpointProfile(
            "endpoint-01", "korad-kel103", 2, "kel-target", 115200);
        var rfLab = new DesktopRuntimeHostRfLabSerialEndpointProfile(
            "endpoint-01", "rflab-signal-lab", 1, "rflab-target", 115200);

        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostEndpointCompositionProfile([], [], [kel103], [rfLab]));
    }

    [Fact]
    public void Constructor_MoreThan64EndpointsIncludingRfLab_ShouldReject()
    {
        DesktopRuntimeHostRfLabSerialEndpointProfile[] rfLabEndpoints = Enumerable.Range(1, 64)
            .Select(index => new DesktopRuntimeHostRfLabSerialEndpointProfile(
                $"rflab-{index}", "rflab-signal-lab", 1, $"external-target-{index}", 115200))
            .ToArray();
        var native = new DesktopRuntimeHostNativeNetworkEndpointProfile("native-01", "device.local", 5000);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DesktopRuntimeHostEndpointCompositionProfile([native], [], [], rfLabEndpoints));
    }

    [Fact]
    public void RfLabSerial_Values_ShouldNormalizeAndPreserveDefinitionReference()
    {
        var endpoint = new DesktopRuntimeHostRfLabSerialEndpointProfile(
            " rflab-01 ", " rflab-signal-lab ", 2, " external-target ", 115200);

        Assert.Equal("rflab-01", endpoint.ExpectedEndpointId);
        Assert.Equal("rflab-signal-lab", endpoint.DefinitionReference.Id.Value);
        Assert.Equal((ushort)2, endpoint.DefinitionReference.Version);
        Assert.Equal("external-target", endpoint.SerialPort);
        Assert.DoesNotContain("external-target", endpoint.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(9600)]
    [InlineData(0)]
    public void RfLabSerial_UnsupportedBaudRate_ShouldReject(int baudRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DesktopRuntimeHostRfLabSerialEndpointProfile(
                "rflab-01", "rflab-signal-lab", 1, "external-target", baudRate));
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

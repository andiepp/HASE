using System.IO;
using System.Net;
using Hase.DesktopHost.Configuration;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost.Tests.Configuration;

public sealed class DesktopRuntimeHostDevelopmentProfileTests
{
    [Fact]
    public void Ipv4Loopback_ShouldAccept()
    {
        var profile = new DesktopRuntimeHostDevelopmentProfile(
            AbsolutePath("runtime-host.id"),
            "127.0.0.1",
            52110,
            includeByteBufferSimulation: true);

        Assert.Equal(IPAddress.Loopback, profile.LoopbackAddress);
        Assert.Equal(52110, profile.Port);
        Assert.Equal("http://127.0.0.1:52110", profile.BindingDisplay);
        Assert.True(profile.IncludeByteBufferSimulation);
        Assert.Null(profile.EndpointCompositionFilePath);
        Assert.Equal(
            RuntimeDiagnosticLevel.Operational,
            profile.MaximumDiagnosticLevel);
    }

    [Fact]
    public void Ipv6Loopback_ShouldAccept()
    {
        var profile = new DesktopRuntimeHostDevelopmentProfile(
            AbsolutePath("runtime-host.id"),
            "::1",
            52110,
            includeByteBufferSimulation: true);

        Assert.Equal(IPAddress.IPv6Loopback, profile.LoopbackAddress);
    }

    [Theory]
    [InlineData("192.168.0.10")]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.1")]
    [InlineData("::")]
    [InlineData("2001:db8::1")]
    public void NonLoopbackAddress_ShouldRefuse(string address)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostDevelopmentProfile(
                AbsolutePath("runtime-host.id"),
                address,
                52110,
                includeByteBufferSimulation: true));

        Assert.Contains("loopback-only", exception.Message);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("not-an-address")]
    [InlineData(" ")]
    public void InvalidAddressText_ShouldReject(string address)
    {
        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostDevelopmentProfile(
                AbsolutePath("runtime-host.id"),
                address,
                52110,
                includeByteBufferSimulation: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void PortOutOfRange_ShouldReject(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DesktopRuntimeHostDevelopmentProfile(
                AbsolutePath("runtime-host.id"),
                "127.0.0.1",
                port,
                includeByteBufferSimulation: true));
    }

    [Fact]
    public void RelativeIdentityPath_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostDevelopmentProfile(
                "runtime-host.id",
                "127.0.0.1",
                52110,
                includeByteBufferSimulation: true));
    }

    [Fact]
    public void DuplicateIdentityAndCompositionPath_ShouldReject()
    {
        string identityPath = AbsolutePath("runtime-host.id");

        Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostDevelopmentProfile(
                identityPath,
                "127.0.0.1",
                52110,
                identityPath));
    }

    [Fact]
    public void NoEndpointSource_ShouldReject()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new DesktopRuntimeHostDevelopmentProfile(
                AbsolutePath("runtime-host.id"),
                "127.0.0.1",
                52110,
                endpointCompositionFilePath: null,
                includeByteBufferSimulation: false));

        Assert.Contains("endpoint composition", exception.Message);
    }

    [Fact]
    public void EndpointCompositionWithoutSimulation_ShouldAccept()
    {
        var profile = new DesktopRuntimeHostDevelopmentProfile(
            AbsolutePath("runtime-host.id"),
            "127.0.0.1",
            52110,
            AbsolutePath("desktop-runtime-endpoints.json"));

        Assert.Equal(
            AbsolutePath("desktop-runtime-endpoints.json"),
            profile.EndpointCompositionFilePath);
        Assert.False(profile.IncludeByteBufferSimulation);
    }

    [Fact]
    public void Label_ShouldNameLoopbackOnlyNoTls()
    {
        var profile = new DesktopRuntimeHostDevelopmentProfile(
            AbsolutePath("runtime-host.id"),
            "127.0.0.1",
            52110,
            includeByteBufferSimulation: true);

        Assert.Equal(
            "Desktop Runtime Host development profile (loopback only, no TLS)",
            profile.ToString());
    }

    private static string AbsolutePath(string fileName) =>
        Path.Combine(Path.GetTempPath(), "hase-60c1", fileName);
}

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostDevelopmentLoopbackClientOptionsTests
{
    [Fact]
    public void Ipv4LoopbackHttp_ShouldAccept()
    {
        var options = new RuntimeHostDevelopmentLoopbackClientOptions(
            new Uri("http://127.0.0.1:52110"));

        Assert.Equal(
            new Uri("http://127.0.0.1:52110"),
            options.Address);
    }

    [Fact]
    public void Ipv6LoopbackHttp_ShouldAccept()
    {
        var options = new RuntimeHostDevelopmentLoopbackClientOptions(
            new Uri("http://[::1]:52110"));

        Assert.Equal("::1", options.Address.IdnHost);
    }

    [Fact]
    public void HttpsAddress_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeHostDevelopmentLoopbackClientOptions(
                new Uri("https://127.0.0.1:52110")));
    }

    [Theory]
    [InlineData("http://192.168.0.10:52110")]
    [InlineData("http://10.0.0.1:52110")]
    [InlineData("http://[2001:db8::1]:52110")]
    public void NonLoopbackAddress_ShouldRefuse(string address)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new RuntimeHostDevelopmentLoopbackClientOptions(
                new Uri(address)));

        Assert.Contains("loopback-only", exception.Message);
    }

    [Fact]
    public void HostName_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeHostDevelopmentLoopbackClientOptions(
                new Uri("http://localhost:52110")));
    }

    [Theory]
    [InlineData("http://127.0.0.1:52110/api")]
    [InlineData("http://127.0.0.1:52110/?query=1")]
    [InlineData("http://user@127.0.0.1:52110")]
    public void AddressWithExtraComponents_ShouldReject(string address)
    {
        Assert.Throws<ArgumentException>(
            () => new RuntimeHostDevelopmentLoopbackClientOptions(
                new Uri(address)));
    }

    [Fact]
    public void NullAddress_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostDevelopmentLoopbackClientOptions(
                null!));
    }
}

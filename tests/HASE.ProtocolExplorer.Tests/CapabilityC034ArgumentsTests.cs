using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC034ArgumentsTests
{
    [Fact]
    public void Constructor_EndpointHost_ShouldPreserveValue()
    {
        var arguments =
            new CapabilityC034Arguments(
                "192.168.0.223");

        Assert.Equal(
            "192.168.0.223",
            arguments.EndpointHost);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_MissingEndpointHost_ShouldReject(
        string? endpointHost)
    {
        Assert.Throws<ArgumentException>(
            () => new CapabilityC034Arguments(
                endpointHost!));
    }
}

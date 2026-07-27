using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteRuntimeHostIdTests
{
    [Fact]
    public void Constructor_ValidValue_ShouldPreserveTrimmedIdentity()
    {
        var runtimeHostId =
            new RemoteRuntimeHostId(
                "  host-01  ");

        Assert.Equal(
            "host-01",
            runtimeHostId.Value);
        Assert.Equal(
            "host-01",
            runtimeHostId.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_MissingValue_ShouldThrow(
        string? value)
    {
        Assert.Throws<ArgumentException>(
            "value",
            () => new RemoteRuntimeHostId(
                value!));
    }
}

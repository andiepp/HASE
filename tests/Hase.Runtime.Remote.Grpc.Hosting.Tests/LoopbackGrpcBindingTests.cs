using System.Net;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class LoopbackGrpcBindingTests
{
    [Fact]
    public void Constructor_NullAddress_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "address",
            () =>
                new LoopbackGrpcBinding(
                    null!,
                    0));
    }

    [Theory]
    [MemberData(nameof(NonLoopbackAddresses))]
    public void Constructor_NonLoopbackAddress_ShouldThrow(
        IPAddress address)
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                "address",
                () =>
                    new LoopbackGrpcBinding(
                        address,
                        0));

        Assert.Equal(
            "The gRPC host address must be a loopback address. (Parameter 'address')",
            exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Constructor_InvalidPort_ShouldThrow(
        int port)
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                "port",
                () =>
                    new LoopbackGrpcBinding(
                        IPAddress.Loopback,
                        port));

        Assert.Equal(
            port,
            exception.ActualValue);
    }

    [Theory]
    [MemberData(nameof(ValidBindings))]
    public void Constructor_ValidLoopbackBinding_ShouldPreserveValues(
        IPAddress address,
        int port)
    {
        var binding =
            new LoopbackGrpcBinding(
                address,
                port);

        Assert.Equal(
            address,
            binding.Address);
        Assert.Equal(
            port,
            binding.Port);
    }

    public static TheoryData<IPAddress> NonLoopbackAddresses
    {
        get;
    } =
        new()
        {
            IPAddress.Any,
            IPAddress.IPv6Any,
            IPAddress.Parse(
                "192.168.1.10"),
            IPAddress.Parse(
                "100.64.0.10")
        };

    public static TheoryData<IPAddress, int> ValidBindings
    {
        get;
    } =
        new()
        {
            {
                IPAddress.Loopback,
                0
            },
            {
                IPAddress.IPv6Loopback,
                5000
            }
        };
}

using System.Net;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class PrivateNetworkGrpcBindingTests
{
    [Fact]
    public void Constructor_NullAddress_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "address",
            () =>
                new PrivateNetworkGrpcBinding(
                    null!,
                    5000));
    }

    [Theory]
    [MemberData(nameof(LoopbackAddresses))]
    public void Constructor_LoopbackAddress_ShouldThrow(
        IPAddress address)
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                "address",
                () =>
                    new PrivateNetworkGrpcBinding(
                        address,
                        5000));

        Assert.Equal(
            "The private-network gRPC host address must not be a "
            + "loopback address. (Parameter 'address')",
            exception.Message);
    }

    [Theory]
    [MemberData(nameof(WildcardAddresses))]
    public void Constructor_WildcardAddress_ShouldThrow(
        IPAddress address)
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                "address",
                () =>
                    new PrivateNetworkGrpcBinding(
                        address,
                        5000));

        Assert.Equal(
            "The private-network gRPC host address must not be a "
            + "wildcard address. (Parameter 'address')",
            exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Constructor_InvalidPort_ShouldThrow(
        int port)
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                "port",
                () =>
                    new PrivateNetworkGrpcBinding(
                        IPAddress.Parse(
                            "192.0.2.10"),
                        port));

        Assert.Equal(
            port,
            exception.ActualValue);
    }

    [Theory]
    [MemberData(nameof(ValidBindings))]
    public void Constructor_ValidPrivateNetworkBinding_ShouldPreserveValues(
        IPAddress address,
        int port)
    {
        var binding =
            new PrivateNetworkGrpcBinding(
                address,
                port);

        Assert.Equal(
            address,
            binding.Address);
        Assert.Equal(
            port,
            binding.Port);
    }

    public static TheoryData<IPAddress> LoopbackAddresses
    {
        get;
    } =
        new()
        {
            IPAddress.Loopback,
            IPAddress.IPv6Loopback
        };

    public static TheoryData<IPAddress> WildcardAddresses
    {
        get;
    } =
        new()
        {
            IPAddress.Any,
            IPAddress.IPv6Any
        };

    public static TheoryData<IPAddress, int> ValidBindings
    {
        get;
    } =
        new()
        {
            {
                IPAddress.Parse(
                    "192.0.2.10"),
                5000
            },
            {
                IPAddress.Parse(
                    "2001:db8::10"),
                65535
            }
        };
}

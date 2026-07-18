using System.Net;
using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class NetworkEndpointCandidateTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldExposeValues()
    {
        // Arrange
        const string serviceInstanceName =
            "doit-esp32-devkitc-v4-01";

        IPAddress address =
            IPAddress.Parse(
                "192.168.0.223");

        const int port =
            5000;

        // Act
        var candidate =
            new NetworkEndpointCandidate(
                serviceInstanceName,
                address,
                port);

        // Assert
        Assert.Equal(
            serviceInstanceName,
            candidate.ServiceInstanceName);

        Assert.Equal(
            address,
            candidate.Address);

        Assert.Equal(
            port,
            candidate.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_InvalidServiceInstanceName_ShouldThrow(
        string serviceInstanceName)
    {
        // Act
        void Act()
        {
            _ = new NetworkEndpointCandidate(
                serviceInstanceName,
                IPAddress.Loopback,
                5000);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_NullAddress_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new NetworkEndpointCandidate(
                "doit-esp32-devkitc-v4-01",
                null!,
                5000);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Constructor_InvalidPort_ShouldThrow(
        int port)
    {
        // Act
        void Act()
        {
            _ = new NetworkEndpointCandidate(
                "doit-esp32-devkitc-v4-01",
                IPAddress.Loopback,
                port);
        }

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Equals_SameAddressAndPort_ShouldBeEqual()
    {
        // Arrange
        var first =
            new NetworkEndpointCandidate(
                "doit-esp32-devkitc-v4-01",
                IPAddress.Parse(
                    "192.168.0.223"),
                5000);

        var second =
            new NetworkEndpointCandidate(
                "another-service-instance",
                IPAddress.Parse(
                    "192.168.0.223"),
                5000);

        // Act
        bool equal =
            first.Equals(
                second);

        // Assert
        Assert.True(
            equal);

        Assert.Equal(
            first.GetHashCode(),
            second.GetHashCode());
    }

    [Theory]
    [InlineData("192.168.0.224", 5000)]
    [InlineData("192.168.0.223", 5001)]
    public void Equals_DifferentAddressOrPort_ShouldNotBeEqual(
        string address,
        int port)
    {
        // Arrange
        var first =
            new NetworkEndpointCandidate(
                "doit-esp32-devkitc-v4-01",
                IPAddress.Parse(
                    "192.168.0.223"),
                5000);

        var second =
            new NetworkEndpointCandidate(
                "doit-esp32-devkitc-v4-01",
                IPAddress.Parse(
                    address),
                port);

        // Act
        bool equal =
            first.Equals(
                second);

        // Assert
        Assert.False(
            equal);
    }
}
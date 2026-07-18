using System.Net;
using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class MdnsNetworkEndpointBrowserTests
{
    [Fact]
    public void ServiceType_ShouldIdentifyHaseTcpService()
    {
        // Assert
        Assert.Equal(
            "_hase._tcp",
            MdnsNetworkEndpointBrowser.ServiceType);
    }

    [Fact]
    public void CreateCandidates_IPv4Address_ShouldCreateCandidate()
    {
        // Arrange
        IPAddress address =
            IPAddress.Parse(
                "192.168.0.223");

        // Act
        IReadOnlyList<
            NetworkEndpointCandidate> candidates =
                MdnsNetworkEndpointBrowser
                    .CreateCandidates(
                        "doit-esp32-devkitc-v4-01",
                        new[]
                        {
                            address
                        },
                        5000);

        // Assert
        NetworkEndpointCandidate candidate =
            Assert.Single(
                candidates);

        Assert.Equal(
            "doit-esp32-devkitc-v4-01",
            candidate.ServiceInstanceName);

        Assert.Equal(
            address,
            candidate.Address);

        Assert.Equal(
            5000,
            candidate.Port);
    }

    [Fact]
    public void CreateCandidates_IPv6Address_ShouldIgnoreAddress()
    {
        // Arrange
        IPAddress address =
            IPAddress.Parse(
                "fe80::1");

        // Act
        IReadOnlyList<
            NetworkEndpointCandidate> candidates =
                MdnsNetworkEndpointBrowser
                    .CreateCandidates(
                        "doit-esp32-devkitc-v4-01",
                        new[]
                        {
                            address
                        },
                        5000);

        // Assert
        Assert.Empty(
            candidates);
    }

    [Fact]
    public void CreateCandidates_MixedAddresses_ShouldReturnOnlyIPv4()
    {
        // Arrange
        IPAddress ipv4Address =
            IPAddress.Parse(
                "192.168.0.223");

        IPAddress ipv6Address =
            IPAddress.Parse(
                "fe80::1");

        // Act
        IReadOnlyList<
            NetworkEndpointCandidate> candidates =
                MdnsNetworkEndpointBrowser
                    .CreateCandidates(
                        "doit-esp32-devkitc-v4-01",
                        new[]
                        {
                            ipv4Address,
                            ipv6Address
                        },
                        5000);

        // Assert
        NetworkEndpointCandidate candidate =
            Assert.Single(
                candidates);

        Assert.Equal(
            ipv4Address,
            candidate.Address);
    }

    [Fact]
    public void CreateCandidates_DuplicateIPv4Addresses_ShouldDeduplicate()
    {
        // Arrange
        IPAddress firstAddress =
            IPAddress.Parse(
                "192.168.0.223");

        IPAddress secondAddress =
            IPAddress.Parse(
                "192.168.0.223");

        // Act
        IReadOnlyList<
            NetworkEndpointCandidate> candidates =
                MdnsNetworkEndpointBrowser
                    .CreateCandidates(
                        "doit-esp32-devkitc-v4-01",
                        new[]
                        {
                            firstAddress,
                            secondAddress
                        },
                        5000);

        // Assert
        Assert.Single(
            candidates);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateCandidates_InvalidInstanceName_ShouldIgnoreAnnouncement(
        string? serviceInstanceName)
    {
        // Act
        IReadOnlyList<
            NetworkEndpointCandidate> candidates =
                MdnsNetworkEndpointBrowser
                    .CreateCandidates(
                        serviceInstanceName,
                        new[]
                        {
                            IPAddress.Loopback
                        },
                        5000);

        // Assert
        Assert.Empty(
            candidates);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void CreateCandidates_InvalidPort_ShouldIgnoreAnnouncement(
        int port)
    {
        // Act
        IReadOnlyList<
            NetworkEndpointCandidate> candidates =
                MdnsNetworkEndpointBrowser
                    .CreateCandidates(
                        "doit-esp32-devkitc-v4-01",
                        new[]
                        {
                            IPAddress.Loopback
                        },
                        port);

        // Assert
        Assert.Empty(
            candidates);
    }

    [Fact]
    public void CreateCandidates_NullAddresses_ShouldIgnoreAnnouncement()
    {
        // Act
        IReadOnlyList<
            NetworkEndpointCandidate> candidates =
                MdnsNetworkEndpointBrowser
                    .CreateCandidates(
                        "doit-esp32-devkitc-v4-01",
                        null,
                        5000);

        // Assert
        Assert.Empty(
            candidates);
    }

    [Fact]
    public void CreateCandidates_MultipleIPv4Addresses_ShouldReturnEachAddress()
    {
        // Arrange
        IPAddress firstAddress =
            IPAddress.Parse(
                "192.168.0.223");

        IPAddress secondAddress =
            IPAddress.Parse(
                "192.168.0.224");

        // Act
        IReadOnlyList<
            NetworkEndpointCandidate> candidates =
                MdnsNetworkEndpointBrowser
                    .CreateCandidates(
                        "doit-esp32-devkitc-v4-01",
                        new[]
                        {
                            firstAddress,
                            secondAddress
                        },
                        5000);

        // Assert
        Assert.Equal(
            2,
            candidates.Count);

        Assert.Contains(
            candidates,
            candidate =>
                candidate.Address.Equals(
                    firstAddress)
                && candidate.Port == 5000);

        Assert.Contains(
            candidates,
            candidate =>
                candidate.Address.Equals(
                    secondAddress)
                && candidate.Port == 5000);
    }
}
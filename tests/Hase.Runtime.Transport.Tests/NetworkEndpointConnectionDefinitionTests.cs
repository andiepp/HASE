using System.Net;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Tcp;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NetworkEndpointConnectionDefinitionTests
{
    [Fact]
    public void FromVerifiedEndpoint_ValidEndpoint_ShouldCreateDefinition()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "doit-esp32-devkitc-v4-01");

        var verifiedEndpoint =
            new VerifiedNetworkEndpoint(
                new NetworkEndpointCandidate(
                    "doit-esp32-devkitc-v4-01",
                    IPAddress.Parse(
                        "192.168.0.223"),
                    5000),
                endpointId);

        // Act
        NetworkEndpointConnectionDefinition definition =
            NetworkEndpointConnectionDefinition
                .FromVerifiedEndpoint(
                    verifiedEndpoint);

        // Assert
        Assert.Equal(
            EndpointConnectionOrigin.Discovered,
            definition.Origin);

        Assert.Equal(
            "192.168.0.223",
            definition.TransportOptions.Host);

        Assert.Equal(
            5000,
            definition.TransportOptions.Port);

        Assert.Same(
            endpointId,
            definition.ExpectedEndpointId);
    }

    [Fact]
    public void FromVerifiedEndpoint_NullEndpoint_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = NetworkEndpointConnectionDefinition
                .FromVerifiedEndpoint(
                    null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void FromConfiguration_ExpectedIdentity_ShouldCreateDefinition()
    {
        // Arrange
        var transportOptions =
            new TcpTransportOptions(
                "commercial-instrument.local",
                5000);

        var endpointId =
            new EndpointId(
                "commercial-instrument-01");

        // Act
        NetworkEndpointConnectionDefinition definition =
            NetworkEndpointConnectionDefinition
                .FromConfiguration(
                    transportOptions,
                    endpointId);

        // Assert
        Assert.Equal(
            EndpointConnectionOrigin.Configured,
            definition.Origin);

        Assert.Same(
            transportOptions,
            definition.TransportOptions);

        Assert.Same(
            endpointId,
            definition.ExpectedEndpointId);
    }

    [Fact]
    public void FromConfiguration_WithoutIdentity_ShouldAllowNullIdentity()
    {
        // Arrange
        var transportOptions =
            new TcpTransportOptions(
                "192.168.0.50",
                5000);

        // Act
        NetworkEndpointConnectionDefinition definition =
            NetworkEndpointConnectionDefinition
                .FromConfiguration(
                    transportOptions);

        // Assert
        Assert.Equal(
            EndpointConnectionOrigin.Configured,
            definition.Origin);

        Assert.Same(
            transportOptions,
            definition.TransportOptions);

        Assert.Null(
            definition.ExpectedEndpointId);
    }

    [Fact]
    public void FromConfiguration_NullTransportOptions_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = NetworkEndpointConnectionDefinition
                .FromConfiguration(
                    null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }
}
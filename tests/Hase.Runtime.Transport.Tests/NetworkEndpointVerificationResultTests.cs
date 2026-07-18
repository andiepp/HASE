using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using System.Net;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NetworkEndpointVerificationResultTests
{
    [Fact]
    public void VerifiedNetworkEndpoint_ValidValues_ShouldExposeValues()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        var endpointId =
            new EndpointId(
                "EnvironmentEndpoint");

        // Act
        var result =
            new VerifiedNetworkEndpoint(
                candidate,
                endpointId);

        // Assert
        Assert.Same(
            candidate,
            result.Candidate);

        Assert.Same(
            endpointId,
            result.EndpointId);
    }

    [Fact]
    public void VerifiedNetworkEndpoint_NullCandidate_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new VerifiedNetworkEndpoint(
                null!,
                new EndpointId(
                    "EnvironmentEndpoint"));
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void VerifiedNetworkEndpoint_NullEndpointId_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new VerifiedNetworkEndpoint(
                CreateCandidate(),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Theory]
    [InlineData(
        NetworkEndpointVerificationFailure.Unreachable)]
    [InlineData(
        NetworkEndpointVerificationFailure.TimedOut)]
    [InlineData(
        NetworkEndpointVerificationFailure.NonHaseEndpoint)]
    [InlineData(
        NetworkEndpointVerificationFailure.InvalidProtocolResponse)]
    public void RejectedCandidate_ValidFailure_ShouldExposeValues(
        NetworkEndpointVerificationFailure failure)
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        // Act
        var result =
            new RejectedNetworkEndpointCandidate(
                candidate,
                failure,
                "Candidate verification failed.");

        // Assert
        Assert.Same(
            candidate,
            result.Candidate);

        Assert.Equal(
            failure,
            result.Failure);

        Assert.Equal(
            "Candidate verification failed.",
            result.Detail);
    }

    [Fact]
    public void RejectedCandidate_NullCandidate_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new RejectedNetworkEndpointCandidate(
                null!,
                NetworkEndpointVerificationFailure
                    .Unreachable,
                "Candidate verification failed.");
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void RejectedCandidate_InvalidFailure_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new RejectedNetworkEndpointCandidate(
                CreateCandidate(),
                (NetworkEndpointVerificationFailure)999,
                "Candidate verification failed.");
        }

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void RejectedCandidate_InvalidDetail_ShouldThrow(
        string detail)
    {
        // Act
        void Act()
        {
            _ = new RejectedNetworkEndpointCandidate(
                CreateCandidate(),
                NetworkEndpointVerificationFailure
                    .Unreachable,
                detail);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    private static NetworkEndpointCandidate CreateCandidate()
    {
        return new NetworkEndpointCandidate(
            "doit-esp32-devkitc-v4-01",
            IPAddress.Parse(
                "192.168.0.223"),
            5000);
    }
}
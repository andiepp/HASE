using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointDiscoveryResultTests
{
    [Fact]
    public void Constructor_AllCandidateOutcomes_ShouldBeRetainedInOrder()
    {
        var firstResult =
            new RejectedUsbSerialEndpointCandidate(
                new UsbSerialEndpointCandidate(
                    "COM1"),
                UsbSerialEndpointVerificationFailure.NonHaseEndpoint,
                "Not a HASE endpoint.");

        VerifiedUsbSerialEndpoint secondResult =
            CreateVerifiedResult(
                portName: "COM10",
                endpointIdValue: "uno-01");

        var result =
            new UsbSerialEndpointDiscoveryResult(
                new UsbSerialEndpointVerificationResult[]
                {
                    firstResult,
                    secondResult
                });

        Assert.Equal(
            new UsbSerialEndpointVerificationResult[]
            {
                firstResult,
                secondResult
            },
            result.CandidateResults);
    }

    [Fact]
    public void Constructor_DuplicateAuthoritativeIdentity_ShouldRetainFirstVerifiedEndpoint()
    {
        VerifiedUsbSerialEndpoint firstResult =
            CreateVerifiedResult(
                portName: "COM10",
                endpointIdValue: "uno-01");

        VerifiedUsbSerialEndpoint duplicateResult =
            CreateVerifiedResult(
                portName: "COM11",
                endpointIdValue: "uno-01");

        var result =
            new UsbSerialEndpointDiscoveryResult(
                new UsbSerialEndpointVerificationResult[]
                {
                    firstResult,
                    duplicateResult
                });

        Assert.Equal(
            2,
            result.CandidateResults.Count);

        VerifiedUsbSerialEndpoint verifiedEndpoint =
            Assert.Single(
                result.VerifiedEndpoints);

        Assert.Same(
            firstResult,
            verifiedEndpoint);
    }

    [Fact]
    public void Constructor_DistinctAuthoritativeIdentities_ShouldRetainBoth()
    {
        VerifiedUsbSerialEndpoint firstResult =
            CreateVerifiedResult(
                portName: "COM10",
                endpointIdValue: "uno-01");

        VerifiedUsbSerialEndpoint secondResult =
            CreateVerifiedResult(
                portName: "COM11",
                endpointIdValue: "uno-02");

        var result =
            new UsbSerialEndpointDiscoveryResult(
                new UsbSerialEndpointVerificationResult[]
                {
                    firstResult,
                    secondResult
                });

        Assert.Equal(
            new[]
            {
                firstResult,
                secondResult
            },
            result.VerifiedEndpoints);
    }

    [Fact]
    public void Constructor_NullResults_ShouldThrow()
    {
        void Act()
        {
            _ = new UsbSerialEndpointDiscoveryResult(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullResultEntry_ShouldThrow()
    {
        void Act()
        {
            _ = new UsbSerialEndpointDiscoveryResult(
                new UsbSerialEndpointVerificationResult[]
                {
                    null!
                });
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    private static VerifiedUsbSerialEndpoint CreateVerifiedResult(
        string portName,
        string endpointIdValue)
    {
        return new VerifiedUsbSerialEndpoint(
            new UsbSerialEndpointCandidate(
                portName),
            new EndpointId(
                endpointIdValue),
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 3),
            new EndpointDescriptorDefinition());
    }
}
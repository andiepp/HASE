using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointVerificationResultTests
{
    [Fact]
    public void VerifiedEndpoint_ValidValues_ShouldExposeValues()
    {
        // Arrange
        UsbSerialEndpointCandidate candidate =
            CreateCandidate();

        var endpointId =
            new EndpointId(
                "arduino-uno-01");

        DescriptorReference descriptorReference =
            CreateDescriptorReference();

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        // Act
        var result =
            new VerifiedUsbSerialEndpoint(
                candidate,
                endpointId,
                descriptorReference,
                descriptorDefinition);

        // Assert
        Assert.Same(
            candidate,
            result.Candidate);

        Assert.Same(
            endpointId,
            result.EndpointId);

        Assert.Same(
            descriptorReference,
            result.DescriptorReference);

        Assert.Same(
            descriptorDefinition,
            result.DescriptorDefinition);
    }

    [Fact]
    public void VerifiedEndpoint_NullCandidate_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new VerifiedUsbSerialEndpoint(
                null!,
                new EndpointId(
                    "arduino-uno-01"),
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition());
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void VerifiedEndpoint_NullEndpointId_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new VerifiedUsbSerialEndpoint(
                CreateCandidate(),
                null!,
                CreateDescriptorReference(),
                new EndpointDescriptorDefinition());
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void VerifiedEndpoint_NullDescriptorReference_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new VerifiedUsbSerialEndpoint(
                CreateCandidate(),
                new EndpointId(
                    "arduino-uno-01"),
                null!,
                new EndpointDescriptorDefinition());
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void VerifiedEndpoint_NullDescriptorDefinition_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new VerifiedUsbSerialEndpoint(
                CreateCandidate(),
                new EndpointId(
                    "arduino-uno-01"),
                CreateDescriptorReference(),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Theory]
    [InlineData(
        UsbSerialEndpointVerificationFailure.PortBusy)]
    [InlineData(
        UsbSerialEndpointVerificationFailure.PortUnavailable)]
    [InlineData(
        UsbSerialEndpointVerificationFailure.AccessDenied)]
    [InlineData(
        UsbSerialEndpointVerificationFailure.ConnectionFailed)]
    [InlineData(
        UsbSerialEndpointVerificationFailure.TimedOut)]
    [InlineData(
        UsbSerialEndpointVerificationFailure.NonHaseEndpoint)]
    [InlineData(
        UsbSerialEndpointVerificationFailure.InvalidCompactResponse)]
    [InlineData(
        UsbSerialEndpointVerificationFailure
            .UnsupportedCompactProtocolVersion)]
    [InlineData(
        UsbSerialEndpointVerificationFailure.InvalidEndpointIdentity)]
    [InlineData(
        UsbSerialEndpointVerificationFailure.UnknownDescriptorReference)]
    [InlineData(
        UsbSerialEndpointVerificationFailure.IncompatibleDescriptor)]
    public void RejectedCandidate_ValidFailure_ShouldExposeValues(
        UsbSerialEndpointVerificationFailure failure)
    {
        // Arrange
        UsbSerialEndpointCandidate candidate =
            CreateCandidate();

        // Act
        var result =
            new RejectedUsbSerialEndpointCandidate(
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
            _ = new RejectedUsbSerialEndpointCandidate(
                null!,
                UsbSerialEndpointVerificationFailure
                    .PortUnavailable,
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
            _ = new RejectedUsbSerialEndpointCandidate(
                CreateCandidate(),
                (UsbSerialEndpointVerificationFailure)999,
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
            _ = new RejectedUsbSerialEndpointCandidate(
                CreateCandidate(),
                UsbSerialEndpointVerificationFailure
                    .ConnectionFailed,
                detail);
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    private static UsbSerialEndpointCandidate CreateCandidate()
    {
        return new UsbSerialEndpointCandidate(
            "COM10",
            vendorId: 0x2341,
            productId: 0x0043,
            productName: "Arduino Uno",
            manufacturerName: "Arduino LLC (www.arduino.cc)",
            serialNumber: "75836333537351D06110");
    }

    private static DescriptorReference CreateDescriptorReference()
    {
        return new DescriptorReference(
            new DescriptorId(
                "arduino-uno-validation"),
            version: 1);
    }
}
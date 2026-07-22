using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactUsbSerialEndpointOpenFailureTests
{
    [Theory]
    [InlineData(
        SerialPortOpenFailure.Busy,
        UsbSerialEndpointVerificationFailure.PortBusy)]
    [InlineData(
        SerialPortOpenFailure.Unavailable,
        UsbSerialEndpointVerificationFailure.PortUnavailable)]
    [InlineData(
        SerialPortOpenFailure.AccessDenied,
        UsbSerialEndpointVerificationFailure.AccessDenied)]
    [InlineData(
        SerialPortOpenFailure.Failed,
        UsbSerialEndpointVerificationFailure.ConnectionFailed)]
    public async Task VerifyAsync_SerialPortOpenFailure_ShouldReturnRejectedResult(
        SerialPortOpenFailure openFailure,
        UsbSerialEndpointVerificationFailure expectedFailure)
    {
        // Arrange
        UsbSerialEndpointCandidate candidate =
            CreateCandidate();

        var innerException =
            new IOException(
                "Native serial open failed.");

        var openException =
            new SerialPortOpenException(
                candidate.PortName,
                openFailure,
                innerException);

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                new ThrowingCompactEndpointConnectionFactory(
                    openException));

        // Act
        UsbSerialEndpointVerificationResult result =
            await operation.VerifyAsync(
                candidate,
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    3),
                CancellationToken.None);

        // Assert
        RejectedUsbSerialEndpointCandidate rejectedResult =
            Assert.IsType<RejectedUsbSerialEndpointCandidate>(
                result);

        Assert.Same(
            candidate,
            rejectedResult.Candidate);

        Assert.Equal(
            expectedFailure,
            rejectedResult.Failure);

        Assert.Equal(
            openException.Message,
            rejectedResult.Detail);
    }

    [Fact]
    public async Task VerifyAsync_GenericIoFailure_ShouldContinueToPropagate()
    {
        // Arrange
        var expectedException =
            new IOException(
                "The compact exchange failed.");

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                new ThrowingCompactEndpointConnectionFactory(
                    expectedException));

        // Act
        Task Act()
        {
            return operation.VerifyAsync(
                CreateCandidate(),
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    3),
                CancellationToken.None);
        }

        // Assert
        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);
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

    private static SerialTransportOptions CreateTransportOptions()
    {
        return new SerialTransportOptions(
            "COM10",
            115200);
    }

    private sealed class ThrowingCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly Exception _exception;

        public ThrowingCompactEndpointConnectionFactory(
            Exception exception)
        {
            _exception =
                exception;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}
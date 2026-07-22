using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointCandidateVerifierContractTests
{
    [Fact]
    public async Task VerifyAsync_ShouldExposeVerificationResult()
    {
        // Arrange
        UsbSerialEndpointCandidate candidate =
            CreateCandidate();

        var transportOptions =
            new SerialTransportOptions(
                candidate.PortName,
                115200);

        TimeSpan timeout =
            TimeSpan.FromSeconds(
                3);

        UsbSerialEndpointVerificationResult expectedResult =
            new RejectedUsbSerialEndpointCandidate(
                candidate,
                UsbSerialEndpointVerificationFailure
                    .NonHaseEndpoint,
                "The candidate is not a HASE compact endpoint.");

        IUsbSerialEndpointCandidateVerifier verifier =
            new StubUsbSerialEndpointCandidateVerifier(
                expectedResult);

        // Act
        UsbSerialEndpointVerificationResult actualResult =
            await verifier.VerifyAsync(
                candidate,
                transportOptions,
                timeout);

        // Assert
        Assert.Same(
            expectedResult,
            actualResult);
    }

    [Fact]
    public async Task VerifyAsync_ShouldReceiveCandidateSettingsAndTimeout()
    {
        // Arrange
        UsbSerialEndpointCandidate candidate =
            CreateCandidate();

        var transportOptions =
            new SerialTransportOptions(
                candidate.PortName,
                115200);

        TimeSpan timeout =
            TimeSpan.FromSeconds(
                3);

        var verifier =
            new RecordingUsbSerialEndpointCandidateVerifier();

        // Act
        _ = await verifier.VerifyAsync(
            candidate,
            transportOptions,
            timeout);

        // Assert
        Assert.Same(
            candidate,
            verifier.Candidate);

        Assert.Same(
            transportOptions,
            verifier.TransportOptions);

        Assert.Equal(
            timeout,
            verifier.Timeout);
    }

    [Fact]
    public async Task VerifyAsync_CancelledToken_ShouldPropagateCancellation()
    {
        // Arrange
        UsbSerialEndpointCandidate candidate =
            CreateCandidate();

        var transportOptions =
            new SerialTransportOptions(
                candidate.PortName,
                115200);

        IUsbSerialEndpointCandidateVerifier verifier =
            new RecordingUsbSerialEndpointCandidateVerifier();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                candidate,
                transportOptions,
                TimeSpan.FromSeconds(
                    3),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
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

    private sealed class StubUsbSerialEndpointCandidateVerifier
        : IUsbSerialEndpointCandidateVerifier
    {
        private readonly UsbSerialEndpointVerificationResult _result;

        public StubUsbSerialEndpointCandidateVerifier(
            UsbSerialEndpointVerificationResult result)
        {
            _result =
                result;
        }

        public Task<UsbSerialEndpointVerificationResult> VerifyAsync(
            UsbSerialEndpointCandidate candidate,
            SerialTransportOptions transportOptions,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _result);
        }
    }

    private sealed class RecordingUsbSerialEndpointCandidateVerifier
        : IUsbSerialEndpointCandidateVerifier
    {
        public UsbSerialEndpointCandidate? Candidate
        {
            get;
            private set;
        }

        public SerialTransportOptions? TransportOptions
        {
            get;
            private set;
        }

        public TimeSpan Timeout
        {
            get;
            private set;
        }

        public Task<UsbSerialEndpointVerificationResult> VerifyAsync(
            UsbSerialEndpointCandidate candidate,
            SerialTransportOptions transportOptions,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Candidate =
                candidate;

            TransportOptions =
                transportOptions;

            Timeout =
                timeout;

            UsbSerialEndpointVerificationResult result =
                new RejectedUsbSerialEndpointCandidate(
                    candidate,
                    UsbSerialEndpointVerificationFailure
                        .NonHaseEndpoint,
                    "The candidate is not a HASE compact endpoint.");

            return Task.FromResult(
                result);
        }
    }
}
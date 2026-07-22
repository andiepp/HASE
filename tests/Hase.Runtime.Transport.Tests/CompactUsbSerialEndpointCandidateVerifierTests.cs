using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactUsbSerialEndpointCandidateVerifierTests
{
    [Fact]
    public void Constructor_NullOperation_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactUsbSerialEndpointCandidateVerifier(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task VerifyAsync_ValidValues_ShouldReturnOperationResult()
    {
        // Arrange
        UsbSerialEndpointCandidate candidate =
            CreateCandidate();

        SerialTransportOptions transportOptions =
            CreateTransportOptions();

        UsbSerialEndpointVerificationResult expectedResult =
            new RejectedUsbSerialEndpointCandidate(
                candidate,
                UsbSerialEndpointVerificationFailure
                    .NonHaseEndpoint,
                "The candidate is not a HASE compact endpoint.");

        var operation =
            new StubVerificationOperation(
                expectedResult);

        var verifier =
            new CompactUsbSerialEndpointCandidateVerifier(
                operation);

        // Act
        UsbSerialEndpointVerificationResult actualResult =
            await verifier.VerifyAsync(
                candidate,
                transportOptions,
                TimeSpan.FromSeconds(
                    3));

        // Assert
        Assert.Same(
            expectedResult,
            actualResult);

        Assert.Equal(
            1,
            operation.CallCount);

        Assert.Same(
            candidate,
            operation.Candidate);

        Assert.Same(
            transportOptions,
            operation.TransportOptions);

        Assert.Equal(
            TimeSpan.FromSeconds(
                3),
            operation.Timeout);
    }

    [Fact]
    public async Task VerifyAsync_NullCandidate_ShouldThrowBeforeOperation()
    {
        // Arrange
        var operation =
            new CountingVerificationOperation();

        var verifier =
            new CompactUsbSerialEndpointCandidateVerifier(
                operation);

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                null!,
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    3));
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentNullException>(
                Act);

        Assert.Equal(
            0,
            operation.CallCount);
    }

    [Fact]
    public async Task VerifyAsync_NullTransportOptions_ShouldThrowBeforeOperation()
    {
        // Arrange
        var operation =
            new CountingVerificationOperation();

        var verifier =
            new CompactUsbSerialEndpointCandidateVerifier(
                operation);

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                CreateCandidate(),
                null!,
                TimeSpan.FromSeconds(
                    3));
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentNullException>(
                Act);

        Assert.Equal(
            0,
            operation.CallCount);
    }

    [Theory]
    [InlineData("COM11")]
    [InlineData("com10")]
    public async Task VerifyAsync_PortDoesNotMatch_ShouldThrowBeforeOperation(
        string portName)
    {
        // Arrange
        var operation =
            new CountingVerificationOperation();

        var verifier =
            new CompactUsbSerialEndpointCandidateVerifier(
                operation);

        var transportOptions =
            new SerialTransportOptions(
                portName,
                115200);

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                CreateCandidate(),
                transportOptions,
                TimeSpan.FromSeconds(
                    3));
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentException>(
                Act);

        Assert.Equal(
            0,
            operation.CallCount);
    }

    [Theory]
    [MemberData(nameof(InvalidTimeouts))]
    public async Task VerifyAsync_InvalidTimeout_ShouldThrowBeforeOperation(
        TimeSpan timeout)
    {
        // Arrange
        var operation =
            new CountingVerificationOperation();

        var verifier =
            new CompactUsbSerialEndpointCandidateVerifier(
                operation);

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                CreateCandidate(),
                CreateTransportOptions(),
                timeout);
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            0,
            operation.CallCount);
    }

    [Fact]
    public async Task VerifyAsync_PreCancelledToken_ShouldNotInvokeOperation()
    {
        // Arrange
        var operation =
            new CountingVerificationOperation();

        var verifier =
            new CompactUsbSerialEndpointCandidateVerifier(
                operation);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                CreateCandidate(),
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    3),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);

        Assert.Equal(
            0,
            operation.CallCount);
    }

    [Fact]
    public async Task VerifyAsync_OperationCancellation_ShouldPropagate()
    {
        // Arrange
        var verifier =
            new CompactUsbSerialEndpointCandidateVerifier(
                new CancellingVerificationOperation());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                CreateCandidate(),
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    3),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);
    }

    [Fact]
    public async Task VerifyAsync_NullOperationResult_ShouldThrow()
    {
        // Arrange
        var verifier =
            new CompactUsbSerialEndpointCandidateVerifier(
                new NullVerificationOperation());

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                CreateCandidate(),
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    3));
        }

        // Assert
        await Assert.ThrowsAsync<
            InvalidOperationException>(
                Act);
    }

    public static TheoryData<TimeSpan> InvalidTimeouts =>
        new()
        {
            TimeSpan.Zero,
            TimeSpan.FromTicks(
                -1),
            Timeout.InfiniteTimeSpan
        };

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

    private sealed class StubVerificationOperation
        : IUsbSerialEndpointVerificationOperation
    {
        private readonly UsbSerialEndpointVerificationResult _result;

        public StubVerificationOperation(
            UsbSerialEndpointVerificationResult result)
        {
            _result =
                result;
        }

        public int CallCount
        {
            get;
            private set;
        }

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
            CancellationToken cancellationToken)
        {
            CallCount++;

            Candidate =
                candidate;

            TransportOptions =
                transportOptions;

            Timeout =
                timeout;

            return Task.FromResult(
                _result);
        }
    }

    private sealed class CountingVerificationOperation
        : IUsbSerialEndpointVerificationOperation
    {
        public int CallCount
        {
            get;
            private set;
        }

        public Task<UsbSerialEndpointVerificationResult> VerifyAsync(
            UsbSerialEndpointCandidate candidate,
            SerialTransportOptions transportOptions,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            CallCount++;

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

    private sealed class CancellingVerificationOperation
        : IUsbSerialEndpointVerificationOperation
    {
        public Task<UsbSerialEndpointVerificationResult> VerifyAsync(
            UsbSerialEndpointCandidate candidate,
            SerialTransportOptions transportOptions,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromCanceled<
                UsbSerialEndpointVerificationResult>(
                    new CancellationToken(
                        canceled: true));
        }
    }

    private sealed class NullVerificationOperation
        : IUsbSerialEndpointVerificationOperation
    {
        public Task<UsbSerialEndpointVerificationResult> VerifyAsync(
            UsbSerialEndpointCandidate candidate,
            SerialTransportOptions transportOptions,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                UsbSerialEndpointVerificationResult>(
                    null!);
        }
    }
}
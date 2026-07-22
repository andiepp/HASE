using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactUsbSerialEndpointVerificationTimeoutTests
{
    [Fact]
    public async Task VerifyAsync_Timeout_ShouldReturnTimedOutResult()
    {
        // Arrange
        UsbSerialEndpointCandidate candidate =
            CreateCandidate();

        var connectionFactory =
            new BlockingCompactEndpointConnectionFactory();

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                connectionFactory);

        TimeSpan timeout =
            TimeSpan.FromMilliseconds(
                20);

        // Act
        UsbSerialEndpointVerificationResult result =
            await operation.VerifyAsync(
                candidate,
                CreateTransportOptions(),
                timeout,
                CancellationToken.None);

        // Assert
        RejectedUsbSerialEndpointCandidate rejectedResult =
            Assert.IsType<RejectedUsbSerialEndpointCandidate>(
                result);

        Assert.Same(
            candidate,
            rejectedResult.Candidate);

        Assert.Equal(
            UsbSerialEndpointVerificationFailure.TimedOut,
            rejectedResult.Failure);

        Assert.Contains(
            timeout.ToString(),
            rejectedResult.Detail,
            StringComparison.Ordinal);

        Assert.True(
            connectionFactory.ReceivedCancellation);

        Assert.True(
            connectionFactory.ReceivedCancelableToken);
    }

    [Fact]
    public async Task VerifyAsync_CallerCancellation_ShouldPropagate()
    {
        // Arrange
        var connectionFactory =
            new BlockingCompactEndpointConnectionFactory();

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                connectionFactory);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.CancelAfter(
            TimeSpan.FromMilliseconds(
                20));

        // Act
        Task Act()
        {
            return operation.VerifyAsync(
                CreateCandidate(),
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    10),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);

        Assert.True(
            connectionFactory.ReceivedCancellation);

        Assert.True(
            connectionFactory.ReceivedCancelableToken);
    }

    [Fact]
    public async Task VerifyAsync_PreCancelledCaller_ShouldNotConnect()
    {
        // Arrange
        var connectionFactory =
            new CountingCompactEndpointConnectionFactory();

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                connectionFactory);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task Act()
        {
            return operation.VerifyAsync(
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
            connectionFactory.CallCount);
    }

    [Fact]
    public async Task VerifyAsync_IndependentCancellation_ShouldPropagate()
    {
        // Arrange
        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                new IndependentlyCancellingConnectionFactory());

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

    private static SerialTransportOptions CreateTransportOptions()
    {
        return new SerialTransportOptions(
            "COM10",
            115200);
    }

    private sealed class BlockingCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        public bool ReceivedCancellation
        {
            get;
            private set;
        }

        public bool ReceivedCancelableToken
        {
            get;
            private set;
        }

        public async Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            ReceivedCancelableToken =
                cancellationToken.CanBeCanceled;

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ReceivedCancellation =
                    true;

                throw;
            }

            throw new InvalidOperationException(
                "The blocking connection factory completed unexpectedly.");
        }
    }

    private sealed class CountingCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        public int CallCount
        {
            get;
            private set;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            throw new InvalidOperationException(
                "A connection was not expected.");
        }
    }

    private sealed class IndependentlyCancellingConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromCanceled<CompactEndpointConnection>(
                new CancellationToken(
                    canceled: true));
        }
    }
}
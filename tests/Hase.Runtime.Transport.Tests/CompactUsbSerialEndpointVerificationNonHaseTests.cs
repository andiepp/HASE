using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactUsbSerialEndpointVerificationNonHaseTests
{
    [Fact]
    public async Task VerifyAsync_StreamEndsBeforeCompactFrame_ShouldRejectAsNonHase()
    {
        var candidate =
            new UsbSerialEndpointCandidate(
                "COM5");

        var exception =
            new EndOfStreamException(
                "The serial byte stream ended before a complete compact "
                + "serial frame was received.");

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                new ThrowingCompactEndpointConnectionFactory(
                    exception));

        UsbSerialEndpointVerificationResult result =
            await operation.VerifyAsync(
                candidate,
                new SerialTransportOptions(
                    "COM5",
                    115200),
                timeout: TimeSpan.FromSeconds(
                    1),
                cancellationToken: CancellationToken.None);

        RejectedUsbSerialEndpointCandidate rejected =
            Assert.IsType<RejectedUsbSerialEndpointCandidate>(
                result);

        Assert.Same(
            candidate,
            rejected.Candidate);

        Assert.Equal(
            UsbSerialEndpointVerificationFailure.NonHaseEndpoint,
            rejected.Failure);

        Assert.Equal(
            exception.Message,
            rejected.Detail);
    }

    private sealed class ThrowingCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly Exception _exception;

        public ThrowingCompactEndpointConnectionFactory(
            Exception exception)
        {
            _exception =
                exception
                ?? throw new ArgumentNullException(
                    nameof(exception));
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<CompactEndpointConnection>(
                _exception);
        }
    }
}
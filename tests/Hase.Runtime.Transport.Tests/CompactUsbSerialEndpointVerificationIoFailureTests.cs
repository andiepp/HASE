using Hase.CompactProtocol;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactUsbSerialEndpointVerificationIoFailureTests
{
    [Fact]
    public async Task VerifyAsync_UnclassifiedIoFailure_ShouldPropagate()
    {
        var candidate =
            new UsbSerialEndpointCandidate(
                "COM5");

        var exception =
            new IOException(
                "The serial transport failed during compact verification.");

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                new ThrowingCompactEndpointConnectionFactory(
                    exception));

        async Task Act()
        {
            _ = await operation.VerifyAsync(
                candidate,
                new SerialTransportOptions(
                    "COM5",
                    115200),
                timeout: TimeSpan.FromSeconds(
                    1),
                cancellationToken: CancellationToken.None);
        }

        IOException actual =
            await Assert.ThrowsAsync<IOException>(
                Act);

        Assert.Same(
            exception,
            actual);
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
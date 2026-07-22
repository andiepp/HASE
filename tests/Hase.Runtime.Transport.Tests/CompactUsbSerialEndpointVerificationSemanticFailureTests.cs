using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactUsbSerialEndpointVerificationSemanticFailureTests
{
    [Fact]
    public async Task VerifyAsync_UnsupportedProtocolVersion_ShouldRejectCandidate()
    {
        var exception =
            new CompactProtocolVersionNotSupportedException(
                actualVersion: 2,
                supportedVersion: 1);

        RejectedUsbSerialEndpointCandidate result =
            await VerifyFailureAsync(
                exception);

        Assert.Equal(
            UsbSerialEndpointVerificationFailure
                .UnsupportedCompactProtocolVersion,
            result.Failure);

        Assert.Equal(
            exception.Message,
            result.Detail);
    }

    [Fact]
    public async Task VerifyAsync_InvalidBootstrapIdentity_ShouldRejectCandidate()
    {
        var exception =
            new CompactBootstrapIdentityException(
                new ArgumentException(
                    "Invalid identity."));

        RejectedUsbSerialEndpointCandidate result =
            await VerifyFailureAsync(
                exception);

        Assert.Equal(
            UsbSerialEndpointVerificationFailure.InvalidEndpointIdentity,
            result.Failure);

        Assert.Equal(
            exception.Message,
            result.Detail);
    }

    [Fact]
    public async Task VerifyAsync_UnknownDescriptorReference_ShouldRejectCandidate()
    {
        var exception =
            new CompactDescriptorNotFoundException(
                new DescriptorReference(
                    new DescriptorId(
                        "arduino-uno-environment"),
                    version: 3));

        RejectedUsbSerialEndpointCandidate result =
            await VerifyFailureAsync(
                exception);

        Assert.Equal(
            UsbSerialEndpointVerificationFailure.UnknownDescriptorReference,
            result.Failure);

        Assert.Equal(
            exception.Message,
            result.Detail);
    }

    private static async Task<RejectedUsbSerialEndpointCandidate>
        VerifyFailureAsync(
            Exception exception)
    {
        var candidate =
            new UsbSerialEndpointCandidate(
                "COM5");

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

        return rejected;
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
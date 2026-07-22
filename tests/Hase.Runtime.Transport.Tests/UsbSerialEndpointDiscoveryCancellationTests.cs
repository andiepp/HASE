using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using System.Runtime.CompilerServices;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointDiscoveryCancellationTests
{
    [Fact]
    public async Task DiscoverAsync_CallerCancelledDuringVerification_ShouldPropagateAndStop()
    {
        var firstCandidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        var laterCandidate =
            new UsbSerialEndpointCandidate(
                "COM11");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        var verifier =
            new CancellingCandidateVerifier(
                cancellationTokenSource);

        var service =
            new UsbSerialEndpointDiscoveryService(
                new TestCandidateSource(
                    firstCandidate,
                    laterCandidate),
                verifier);

        async Task Act()
        {
            _ = await service.DiscoverAsync(
                new UsbSerialEndpointDiscoveryOptions(
                    baudRate: 115200,
                    verificationTimeout: TimeSpan.FromSeconds(
                        1)),
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            Act);

        UsbSerialEndpointCandidate verifiedCandidate =
            Assert.Single(
                verifier.Candidates);

        Assert.Same(
            firstCandidate,
            verifiedCandidate);

        Assert.True(
            cancellationTokenSource.IsCancellationRequested);
    }

    private sealed class TestCandidateSource
        : IUsbSerialEndpointCandidateSource
    {
        private readonly IReadOnlyList<UsbSerialEndpointCandidate> _candidates;

        public TestCandidateSource(
            params UsbSerialEndpointCandidate[] candidates)
        {
            _candidates =
                candidates;
        }

        public async IAsyncEnumerable<UsbSerialEndpointCandidate> EnumerateAsync(
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            foreach (
                UsbSerialEndpointCandidate candidate
                in _candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Yield();

                yield return candidate;
            }
        }
    }

    private sealed class CancellingCandidateVerifier
        : IUsbSerialEndpointCandidateVerifier
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CancellingCandidateVerifier(
            CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource =
                cancellationTokenSource
                ?? throw new ArgumentNullException(
                    nameof(cancellationTokenSource));
        }

        public List<UsbSerialEndpointCandidate> Candidates
        {
            get;
        } =
            [];

        public Task<UsbSerialEndpointVerificationResult> VerifyAsync(
            UsbSerialEndpointCandidate candidate,
            SerialTransportOptions transportOptions,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Candidates.Add(
                candidate);

            _cancellationTokenSource.Cancel();

            return Task.FromCanceled<
                UsbSerialEndpointVerificationResult>(
                    cancellationToken);
        }
    }
}
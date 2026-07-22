using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using System.Runtime.CompilerServices;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointDiscoveryFailureIsolationTests
{
    [Fact]
    public async Task DiscoverAsync_VerifierIoFailure_ShouldRejectCandidateAndContinue()
    {
        var failingCandidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        var laterCandidate =
            new UsbSerialEndpointCandidate(
                "COM11");

        var verifier =
            new TestCandidateVerifier(
                failingCandidate);

        var service =
            new UsbSerialEndpointDiscoveryService(
                new TestCandidateSource(
                    failingCandidate,
                    laterCandidate),
                verifier);

        UsbSerialEndpointDiscoveryResult result =
            await service.DiscoverAsync(
                new UsbSerialEndpointDiscoveryOptions(
                    baudRate: 115200,
                    verificationTimeout: TimeSpan.FromSeconds(
                        1)));

        Assert.Equal(
            new[]
            {
                failingCandidate,
                laterCandidate
            },
            verifier.Candidates);

        Assert.Equal(
            2,
            result.CandidateResults.Count);

        RejectedUsbSerialEndpointCandidate failedResult =
            Assert.IsType<RejectedUsbSerialEndpointCandidate>(
                result.CandidateResults[0]);

        Assert.Same(
            failingCandidate,
            failedResult.Candidate);

        Assert.Equal(
            UsbSerialEndpointVerificationFailure.ConnectionFailed,
            failedResult.Failure);

        Assert.Equal(
            "Unexpected serial I/O failure.",
            failedResult.Detail);

        RejectedUsbSerialEndpointCandidate laterResult =
            Assert.IsType<RejectedUsbSerialEndpointCandidate>(
                result.CandidateResults[1]);

        Assert.Same(
            laterCandidate,
            laterResult.Candidate);

        Assert.Equal(
            UsbSerialEndpointVerificationFailure.NonHaseEndpoint,
            laterResult.Failure);
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

    private sealed class TestCandidateVerifier
        : IUsbSerialEndpointCandidateVerifier
    {
        private readonly UsbSerialEndpointCandidate _failingCandidate;

        public TestCandidateVerifier(
            UsbSerialEndpointCandidate failingCandidate)
        {
            _failingCandidate =
                failingCandidate;
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
            cancellationToken.ThrowIfCancellationRequested();

            Candidates.Add(
                candidate);

            if (ReferenceEquals(
                candidate,
                _failingCandidate))
            {
                return Task.FromException<
                    UsbSerialEndpointVerificationResult>(
                        new IOException(
                            "Unexpected serial I/O failure."));
            }

            UsbSerialEndpointVerificationResult result =
                new RejectedUsbSerialEndpointCandidate(
                    candidate,
                    UsbSerialEndpointVerificationFailure.NonHaseEndpoint,
                    "Not a HASE compact endpoint.");

            return Task.FromResult(
                result);
        }
    }
}
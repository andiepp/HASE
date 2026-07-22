using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using System.Runtime.CompilerServices;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointDiscoveryCandidateDeduplicationTests
{
    [Fact]
    public async Task DiscoverAsync_DuplicatePortIdentity_ShouldVerifyFirstCandidateOnce()
    {
        var firstCandidate =
            new UsbSerialEndpointCandidate(
                "COM10",
                vendorId: 0x2341,
                productId: 0x0043,
                productName: "Arduino Uno");

        var duplicateCandidate =
            new UsbSerialEndpointCandidate(
                "COM10",
                vendorId: 0x2341,
                productId: 0x0043,
                productName: "Duplicate metadata record");

        var verifier =
            new TestCandidateVerifier();

        var service =
            new UsbSerialEndpointDiscoveryService(
                new TestCandidateSource(
                    firstCandidate,
                    duplicateCandidate),
                verifier);

        IReadOnlyList<UsbSerialEndpointVerificationResult> results =
            await service.DiscoverAsync(
                new UsbSerialEndpointDiscoveryOptions(
                    baudRate: 115200,
                    verificationTimeout: TimeSpan.FromSeconds(
                        1)));

        UsbSerialEndpointCandidate verifiedCandidate =
            Assert.Single(
                verifier.Candidates);

        Assert.Same(
            firstCandidate,
            verifiedCandidate);

        UsbSerialEndpointVerificationResult result =
            Assert.Single(
                results);

        Assert.Same(
            firstCandidate,
            result.Candidate);
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

            UsbSerialEndpointVerificationResult result =
                new RejectedUsbSerialEndpointCandidate(
                    candidate,
                    UsbSerialEndpointVerificationFailure.NonHaseEndpoint,
                    "Rejected by the test verifier.");

            return Task.FromResult(
                result);
        }
    }
}
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using System.Runtime.CompilerServices;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointDiscoveryIdentityDeduplicationTests
{
    [Fact]
    public async Task DiscoverAsync_DistinctPortsWithSameAuthoritativeIdentity_ShouldRetainBothOutcomesAndOneEndpoint()
    {
        var firstCandidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        var secondCandidate =
            new UsbSerialEndpointCandidate(
                "COM11");

        var verifier =
            new SameIdentityCandidateVerifier(
                endpointIdValue:
                    "uno-01");

        var service =
            new UsbSerialEndpointDiscoveryService(
                new TestCandidateSource(
                    firstCandidate,
                    secondCandidate),
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
                firstCandidate,
                secondCandidate
            },
            verifier.Candidates);

        Assert.Equal(
            2,
            result.CandidateResults.Count);

        Assert.All(
            result.CandidateResults,
            candidateResult =>
                Assert.IsType<VerifiedUsbSerialEndpoint>(
                    candidateResult));

        VerifiedUsbSerialEndpoint verifiedEndpoint =
            Assert.Single(
                result.VerifiedEndpoints);

        Assert.Equal(
            "uno-01",
            verifiedEndpoint.EndpointId.Value);

        Assert.Same(
            firstCandidate,
            verifiedEndpoint.Candidate);
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

    private sealed class SameIdentityCandidateVerifier
        : IUsbSerialEndpointCandidateVerifier
    {
        private readonly string _endpointIdValue;

        public SameIdentityCandidateVerifier(
            string endpointIdValue)
        {
            _endpointIdValue =
                endpointIdValue;
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

            UsbSerialEndpointVerificationResult result =
                new VerifiedUsbSerialEndpoint(
                    candidate,
                    new EndpointId(
                        _endpointIdValue),
                    new DescriptorReference(
                        new DescriptorId(
                            "arduino-uno-environment"),
                        version: 3),
                    new EndpointDescriptorDefinition());

            return Task.FromResult(
                result);
        }
    }
}
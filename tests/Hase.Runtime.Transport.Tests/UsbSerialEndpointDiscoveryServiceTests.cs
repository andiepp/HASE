using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using System.Runtime.CompilerServices;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_Candidates_ShouldBeVerifiedSequentiallyInSourceOrder()
    {
        var firstCandidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        var secondCandidate =
            new UsbSerialEndpointCandidate(
                "COM1");

        var verifier =
            new TestCandidateVerifier();

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
                        2)));

        Assert.Equal(
            new[]
            {
                firstCandidate,
                secondCandidate
            },
            verifier.Candidates);

        Assert.Equal(
            new[]
            {
                firstCandidate,
                secondCandidate
            },
            result.CandidateResults.Select(
                candidateResult => candidateResult.Candidate));

        Assert.Equal(
            1,
            verifier.MaximumConcurrentCallCount);

        Assert.Empty(
            result.VerifiedEndpoints);
    }

    [Fact]
    public async Task DiscoverAsync_FilteredCandidate_ShouldNotBeVerified()
    {
        var rejectedByFilter =
            new UsbSerialEndpointCandidate(
                "COM1");

        var acceptedByFilter =
            new UsbSerialEndpointCandidate(
                "COM10");

        var verifier =
            new TestCandidateVerifier();

        var filter =
            new TestCandidateFilter(
                acceptedByFilter);

        var service =
            new UsbSerialEndpointDiscoveryService(
                new TestCandidateSource(
                    rejectedByFilter,
                    acceptedByFilter),
                verifier,
                filter);

        UsbSerialEndpointDiscoveryResult result =
            await service.DiscoverAsync(
                new UsbSerialEndpointDiscoveryOptions(
                    baudRate: 115200,
                    verificationTimeout: TimeSpan.FromSeconds(
                        2)));

        Assert.Equal(
            new[]
            {
                rejectedByFilter,
                acceptedByFilter
            },
            filter.Candidates);

        Assert.Equal(
            new[]
            {
                acceptedByFilter
            },
            verifier.Candidates);

        UsbSerialEndpointVerificationResult candidateResult =
            Assert.Single(
                result.CandidateResults);

        Assert.Same(
            acceptedByFilter,
            candidateResult.Candidate);
    }

    [Fact]
    public async Task DiscoverAsync_Options_ShouldCreateCandidateSpecificTransportOptions()
    {
        var candidate =
            new UsbSerialEndpointCandidate(
                "COM10");

        var verifier =
            new TestCandidateVerifier();

        var service =
            new UsbSerialEndpointDiscoveryService(
                new TestCandidateSource(
                    candidate),
                verifier);

        TimeSpan timeout =
            TimeSpan.FromSeconds(
                3);

        _ = await service.DiscoverAsync(
            new UsbSerialEndpointDiscoveryOptions(
                baudRate: 57600,
                dataBits: 7,
                SerialParity.Even,
                SerialStopBits.Two,
                SerialHandshake.RequestToSend,
                verificationTimeout: timeout));

        SerialTransportOptions transportOptions =
            Assert.Single(
                verifier.TransportOptions);

        Assert.Equal(
            candidate.PortName,
            transportOptions.PortName);

        Assert.Equal(
            57600,
            transportOptions.BaudRate);

        Assert.Equal(
            7,
            transportOptions.DataBits);

        Assert.Equal(
            SerialParity.Even,
            transportOptions.Parity);

        Assert.Equal(
            SerialStopBits.Two,
            transportOptions.StopBits);

        Assert.Equal(
            SerialHandshake.RequestToSend,
            transportOptions.Handshake);

        Assert.Equal(
            timeout,
            Assert.Single(
                verifier.Timeouts));
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

    private sealed class TestCandidateFilter
        : IUsbSerialEndpointCandidateFilter
    {
        private readonly UsbSerialEndpointCandidate _acceptedCandidate;

        public TestCandidateFilter(
            UsbSerialEndpointCandidate acceptedCandidate)
        {
            _acceptedCandidate =
                acceptedCandidate;
        }

        public List<UsbSerialEndpointCandidate> Candidates
        {
            get;
        } =
            [];

        public bool IsMatch(
            UsbSerialEndpointCandidate candidate)
        {
            Candidates.Add(
                candidate);

            return ReferenceEquals(
                candidate,
                _acceptedCandidate);
        }
    }

    private sealed class TestCandidateVerifier
        : IUsbSerialEndpointCandidateVerifier
    {
        private int _concurrentCallCount;

        public List<UsbSerialEndpointCandidate> Candidates
        {
            get;
        } =
            [];

        public List<SerialTransportOptions> TransportOptions
        {
            get;
        } =
            [];

        public List<TimeSpan> Timeouts
        {
            get;
        } =
            [];

        public int MaximumConcurrentCallCount
        {
            get;
            private set;
        }

        public async Task<UsbSerialEndpointVerificationResult> VerifyAsync(
            UsbSerialEndpointCandidate candidate,
            SerialTransportOptions transportOptions,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            int concurrentCallCount =
                Interlocked.Increment(
                    ref _concurrentCallCount);

            MaximumConcurrentCallCount =
                Math.Max(
                    MaximumConcurrentCallCount,
                    concurrentCallCount);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                Candidates.Add(
                    candidate);

                TransportOptions.Add(
                    transportOptions);

                Timeouts.Add(
                    timeout);

                await Task.Yield();

                return new RejectedUsbSerialEndpointCandidate(
                    candidate,
                    UsbSerialEndpointVerificationFailure.NonHaseEndpoint,
                    "Rejected by the test verifier.");
            }
            finally
            {
                Interlocked.Decrement(
                    ref _concurrentCallCount);
            }
        }
    }
}
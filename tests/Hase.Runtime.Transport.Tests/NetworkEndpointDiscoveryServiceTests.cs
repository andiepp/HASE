using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using System.Net;
using System.Runtime.CompilerServices;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NetworkEndpointDiscoveryServiceTests
{
    [Fact]
    public void Constructor_NullBrowser_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new NetworkEndpointDiscoveryService(
                null!,
                new StubCandidateVerifier());
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullVerifier_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new NetworkEndpointDiscoveryService(
                new StubEndpointBrowser(),
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task DiscoverAsync_VerifiedCandidate_ShouldYieldEndpoint()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate(
                "endpoint-01",
                "192.168.0.223",
                5000);

        var endpointId =
            new EndpointId(
                "EnvironmentEndpoint");

        var browser =
            new StubEndpointBrowser(
                candidate);

        var verifier =
            new StubCandidateVerifier();

        verifier.AddResult(
            candidate,
            new VerifiedNetworkEndpoint(
                candidate,
                endpointId));

        var service =
            new NetworkEndpointDiscoveryService(
                browser,
                verifier);

        // Act
        IReadOnlyList<
            NetworkEndpointVerificationResult> results =
                await CollectAsync(
                    service.DiscoverAsync(
                        TimeSpan.FromSeconds(
                            3)));

        // Assert
        VerifiedNetworkEndpoint verified =
            Assert.IsType<
                VerifiedNetworkEndpoint>(
                    Assert.Single(
                        results));

        Assert.Same(
            candidate,
            verified.Candidate);

        Assert.Same(
            endpointId,
            verified.EndpointId);
    }

    [Fact]
    public async Task DiscoverAsync_DuplicateEndpointId_ShouldYieldOnce()
    {
        // Arrange
        NetworkEndpointCandidate firstCandidate =
            CreateCandidate(
                "endpoint-01",
                "192.168.0.223",
                5000);

        NetworkEndpointCandidate secondCandidate =
            CreateCandidate(
                "endpoint-02",
                "192.168.0.224",
                5000);

        var endpointId =
            new EndpointId(
                "EnvironmentEndpoint");

        var browser =
            new StubEndpointBrowser(
                firstCandidate,
                secondCandidate);

        var verifier =
            new StubCandidateVerifier();

        verifier.AddResult(
            firstCandidate,
            new VerifiedNetworkEndpoint(
                firstCandidate,
                endpointId));

        verifier.AddResult(
            secondCandidate,
            new VerifiedNetworkEndpoint(
                secondCandidate,
                new EndpointId(
                    "EnvironmentEndpoint")));

        var service =
            new NetworkEndpointDiscoveryService(
                browser,
                verifier);

        // Act
        IReadOnlyList<
            NetworkEndpointVerificationResult> results =
                await CollectAsync(
                    service.DiscoverAsync(
                        TimeSpan.FromSeconds(
                            3)));

        // Assert
        VerifiedNetworkEndpoint verified =
            Assert.IsType<
                VerifiedNetworkEndpoint>(
                    Assert.Single(
                        results));

        Assert.Same(
            firstCandidate,
            verified.Candidate);

        Assert.Equal(
            endpointId,
            verified.EndpointId);

        Assert.Equal(
            2,
            verifier.VerifyCallCount);
    }

    [Fact]
    public async Task DiscoverAsync_RejectedCandidates_ShouldRemainVisible()
    {
        // Arrange
        NetworkEndpointCandidate firstCandidate =
            CreateCandidate(
                "endpoint-01",
                "192.168.0.223",
                5000);

        NetworkEndpointCandidate secondCandidate =
            CreateCandidate(
                "endpoint-02",
                "192.168.0.224",
                5000);

        var browser =
            new StubEndpointBrowser(
                firstCandidate,
                secondCandidate);

        var verifier =
            new StubCandidateVerifier();

        verifier.AddResult(
            firstCandidate,
            new RejectedNetworkEndpointCandidate(
                firstCandidate,
                NetworkEndpointVerificationFailure
                    .Unreachable,
                "Connection refused."));

        verifier.AddResult(
            secondCandidate,
            new RejectedNetworkEndpointCandidate(
                secondCandidate,
                NetworkEndpointVerificationFailure
                    .TimedOut,
                "Verification timed out."));

        var service =
            new NetworkEndpointDiscoveryService(
                browser,
                verifier);

        // Act
        IReadOnlyList<
            NetworkEndpointVerificationResult> results =
                await CollectAsync(
                    service.DiscoverAsync(
                        TimeSpan.FromSeconds(
                            3)));

        // Assert
        Assert.Equal(
            2,
            results.Count);

        Assert.Collection(
            results,
            result =>
            {
                RejectedNetworkEndpointCandidate rejected =
                    Assert.IsType<
                        RejectedNetworkEndpointCandidate>(
                            result);

                Assert.Equal(
                    NetworkEndpointVerificationFailure
                        .Unreachable,
                    rejected.Failure);
            },
            result =>
            {
                RejectedNetworkEndpointCandidate rejected =
                    Assert.IsType<
                        RejectedNetworkEndpointCandidate>(
                            result);

                Assert.Equal(
                    NetworkEndpointVerificationFailure
                        .TimedOut,
                    rejected.Failure);
            });
    }

    [Fact]
    public async Task DiscoverAsync_Cancellation_ShouldPropagate()
    {
        // Arrange
        var browser =
            new PendingEndpointBrowser();

        var service =
            new NetworkEndpointDiscoveryService(
                browser,
                new StubCandidateVerifier());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task collectTask =
            CollectAsync(
                service.DiscoverAsync(
                    Timeout.InfiniteTimeSpan,
                    cancellationTokenSource.Token));

        await browser.BrowsingStarted;

        // Act
        cancellationTokenSource.Cancel();

        // Assert
        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                    () => collectTask);

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task DiscoverAsync_InvalidTimeout_ShouldThrow(
        int timeoutMilliseconds)
    {
        // Arrange
        var service =
            new NetworkEndpointDiscoveryService(
                new StubEndpointBrowser(),
                new StubCandidateVerifier());

        // Act
        Task Act()
        {
            return CollectAsync(
                service.DiscoverAsync(
                    TimeSpan.FromMilliseconds(
                        timeoutMilliseconds)));
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentOutOfRangeException>(
                Act);
    }

    private static async Task<
        IReadOnlyList<NetworkEndpointVerificationResult>>
        CollectAsync(
            IAsyncEnumerable<
                NetworkEndpointVerificationResult> results)
    {
        var collectedResults =
            new List<
                NetworkEndpointVerificationResult>();

        await foreach (
            NetworkEndpointVerificationResult result
            in results)
        {
            collectedResults.Add(
                result);
        }

        return collectedResults;
    }

    private static NetworkEndpointCandidate CreateCandidate(
        string serviceInstanceName,
        string address,
        int port)
    {
        return new NetworkEndpointCandidate(
            serviceInstanceName,
            IPAddress.Parse(
                address),
            port);
    }

    private sealed class StubEndpointBrowser
        : INetworkEndpointBrowser
    {
        private readonly IReadOnlyList<
            NetworkEndpointCandidate> _candidates;

        public StubEndpointBrowser(
            params NetworkEndpointCandidate[] candidates)
        {
            _candidates =
                candidates;
        }

        public async IAsyncEnumerable<
            NetworkEndpointCandidate> BrowseAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            foreach (
                NetworkEndpointCandidate candidate
                in _candidates)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                yield return candidate;
            }
        }
    }

    private sealed class PendingEndpointBrowser
        : INetworkEndpointBrowser
    {
        private readonly TaskCompletionSource
            _browsingStarted =
                new(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

        public Task BrowsingStarted =>
            _browsingStarted.Task;

        public async IAsyncEnumerable<
            NetworkEndpointCandidate> BrowseAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            _browsingStarted.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            yield break;
        }
    }

    private sealed class StubCandidateVerifier
        : INetworkEndpointCandidateVerifier
    {
        private readonly Dictionary<
            NetworkEndpointCandidate,
            NetworkEndpointVerificationResult> _results =
                new();

        public int VerifyCallCount
        {
            get;
            private set;
        }

        public void AddResult(
            NetworkEndpointCandidate candidate,
            NetworkEndpointVerificationResult result)
        {
            _results.Add(
                candidate,
                result);
        }

        public Task<
            NetworkEndpointVerificationResult> VerifyAsync(
                NetworkEndpointCandidate candidate,
                TimeSpan timeout,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            VerifyCallCount++;

            if (!_results.TryGetValue(
                    candidate,
                    out NetworkEndpointVerificationResult?
                        result))
            {
                throw new InvalidOperationException(
                    "No verification result was configured "
                    + "for the candidate.");
            }

            return Task.FromResult(
                result);
        }
    }
}
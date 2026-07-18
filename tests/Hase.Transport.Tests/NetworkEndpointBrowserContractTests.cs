using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class NetworkEndpointBrowserContractTests
{
    [Fact]
    public async Task BrowseAsync_ShouldExposeCandidates()
    {
        // Arrange
        var expectedCandidate =
            new NetworkEndpointCandidate(
                "doit-esp32-devkitc-v4-01",
                System.Net.IPAddress.Parse(
                    "192.168.0.223"),
                5000);

        INetworkEndpointBrowser browser =
            new StubNetworkEndpointBrowser(
                expectedCandidate);

        // Act
        var actualCandidates =
            new List<NetworkEndpointCandidate>();

        await foreach (
            NetworkEndpointCandidate candidate
            in browser.BrowseAsync())
        {
            actualCandidates.Add(
                candidate);
        }

        // Assert
        NetworkEndpointCandidate actualCandidate =
            Assert.Single(
                actualCandidates);

        Assert.Same(
            expectedCandidate,
            actualCandidate);
    }

    [Fact]
    public async Task BrowseAsync_CancelledToken_ShouldStopBrowsing()
    {
        // Arrange
        INetworkEndpointBrowser browser =
            new StubNetworkEndpointBrowser();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        async Task Act()
        {
            await foreach (
                NetworkEndpointCandidate candidate
                in browser.BrowseAsync(
                    cancellationTokenSource.Token))
            {
            }
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);
    }

    private sealed class StubNetworkEndpointBrowser
        : INetworkEndpointBrowser
    {
        private readonly IReadOnlyList<
            NetworkEndpointCandidate> _candidates;

        public StubNetworkEndpointBrowser(
            params NetworkEndpointCandidate[] candidates)
        {
            _candidates =
                candidates;
        }

        public async IAsyncEnumerable<
            NetworkEndpointCandidate> BrowseAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            cancellationToken
                .ThrowIfCancellationRequested();

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
}
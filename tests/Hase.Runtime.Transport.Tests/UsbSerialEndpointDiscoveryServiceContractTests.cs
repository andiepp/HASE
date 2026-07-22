using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using System.Runtime.CompilerServices;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class UsbSerialEndpointDiscoveryServiceContractTests
{
    [Fact]
    public void Constructor_NullCandidateSource_ShouldThrow()
    {
        void Act()
        {
            _ = new UsbSerialEndpointDiscoveryService(
                null!,
                new UnexpectedCandidateVerifier());
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullCandidateVerifier_ShouldThrow()
    {
        void Act()
        {
            _ = new UsbSerialEndpointDiscoveryService(
                new TestCandidateSource(),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task DiscoverAsync_NullOptions_ShouldThrowWithoutEnumerating()
    {
        var candidateSource =
            new TestCandidateSource();

        var service =
            new UsbSerialEndpointDiscoveryService(
                candidateSource,
                new UnexpectedCandidateVerifier());

        async Task Act()
        {
            _ = await service.DiscoverAsync(
                null!);
        }

        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);

        Assert.Equal(
            0,
            candidateSource.EnumerateCallCount);
    }

    [Fact]
    public async Task DiscoverAsync_PreCancelledCaller_ShouldThrowWithoutEnumerating()
    {
        var candidateSource =
            new TestCandidateSource();

        var service =
            new UsbSerialEndpointDiscoveryService(
                candidateSource,
                new UnexpectedCandidateVerifier());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await service.DiscoverAsync(
                new UsbSerialEndpointDiscoveryOptions(
                    baudRate: 115200,
                    verificationTimeout: TimeSpan.FromSeconds(
                        1)),
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            candidateSource.EnumerateCallCount);
    }

    private sealed class TestCandidateSource
        : IUsbSerialEndpointCandidateSource
    {
        public int EnumerateCallCount
        {
            get;
            private set;
        }

        public IAsyncEnumerable<UsbSerialEndpointCandidate> EnumerateAsync(
            CancellationToken cancellationToken = default)
        {
            EnumerateCallCount++;

            return EnumerateCoreAsync(
                cancellationToken);
        }

        private static async IAsyncEnumerable<UsbSerialEndpointCandidate>
            EnumerateCoreAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Yield();

            yield break;
        }
    }

    private sealed class UnexpectedCandidateVerifier
        : IUsbSerialEndpointCandidateVerifier
    {
        public Task<UsbSerialEndpointVerificationResult> VerifyAsync(
            UsbSerialEndpointCandidate candidate,
            SerialTransportOptions transportOptions,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The candidate verifier should not be called.");
        }
    }
}
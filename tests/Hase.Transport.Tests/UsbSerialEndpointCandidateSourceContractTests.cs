using System.Runtime.CompilerServices;
using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class UsbSerialEndpointCandidateSourceContractTests
{
    [Fact]
    public async Task EnumerateAsync_ShouldExposeCandidates()
    {
        // Arrange
        var expectedCandidate =
            new UsbSerialEndpointCandidate(
                "COM10",
                vendorId: 0x1A86,
                productId: 0x7523,
                productName: "USB Serial",
                manufacturerName: "QinHeng Electronics",
                serialNumber: "ABC123");

        IUsbSerialEndpointCandidateSource source =
            new StubUsbSerialEndpointCandidateSource(
                expectedCandidate);

        // Act
        var actualCandidates =
            new List<UsbSerialEndpointCandidate>();

        await foreach (
            UsbSerialEndpointCandidate candidate
            in source.EnumerateAsync())
        {
            actualCandidates.Add(
                candidate);
        }

        // Assert
        UsbSerialEndpointCandidate actualCandidate =
            Assert.Single(
                actualCandidates);

        Assert.Same(
            expectedCandidate,
            actualCandidate);
    }

    [Fact]
    public async Task EnumerateAsync_CancelledToken_ShouldStopEnumeration()
    {
        // Arrange
        IUsbSerialEndpointCandidateSource source =
            new StubUsbSerialEndpointCandidateSource();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        async Task Act()
        {
            await foreach (
                UsbSerialEndpointCandidate candidate
                in source.EnumerateAsync(
                    cancellationTokenSource.Token))
            {
            }
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);
    }

    private sealed class StubUsbSerialEndpointCandidateSource
        : IUsbSerialEndpointCandidateSource
    {
        private readonly IReadOnlyList<
            UsbSerialEndpointCandidate> _candidates;

        public StubUsbSerialEndpointCandidateSource(
            params UsbSerialEndpointCandidate[] candidates)
        {
            _candidates =
                candidates;
        }

        public async IAsyncEnumerable<
            UsbSerialEndpointCandidate> EnumerateAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            cancellationToken
                .ThrowIfCancellationRequested();

            foreach (
                UsbSerialEndpointCandidate candidate
                in _candidates)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                yield return candidate;
            }
        }
    }
}
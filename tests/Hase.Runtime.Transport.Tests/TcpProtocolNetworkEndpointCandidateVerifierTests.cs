using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    TcpProtocolNetworkEndpointCandidateVerifierTests
{
    [Fact]
    public async Task VerifyAsync_DiscoverResponse_ShouldVerifyEndpoint()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        var endpointId =
            new EndpointId(
                "EnvironmentEndpoint");

        var verifier =
            CreateVerifier(
                (
                    receivedCandidate,
                    timeout,
                    cancellationToken) =>
                {
                    Assert.Same(
                        candidate,
                        receivedCandidate);

                    cancellationToken
                        .ThrowIfCancellationRequested();

                    ProtocolMessage response =
                        new DiscoverResponse(
                            new CorrelationId(
                                1),
                            endpointId,
                            Array.Empty<InstrumentId>());

                    return Task.FromResult(
                        response);
                });

        // Act
        NetworkEndpointVerificationResult result =
            await verifier.VerifyAsync(
                candidate,
                TimeSpan.FromSeconds(
                    3));

        // Assert
        VerifiedNetworkEndpoint verified =
            Assert.IsType<
                VerifiedNetworkEndpoint>(
                    result);

        Assert.Same(
            candidate,
            verified.Candidate);

        Assert.Same(
            endpointId,
            verified.EndpointId);
    }

    [Fact]
    public async Task VerifyAsync_UnexpectedResponse_ShouldRejectCandidate()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        var verifier =
            CreateVerifier(
                (
                    receivedCandidate,
                    timeout,
                    cancellationToken) =>
                {
                    ProtocolMessage response =
                        new ReadPropertyResponse(
                            new CorrelationId(
                                1),
                            ProtocolResult.Success,
                            null);

                    return Task.FromResult(
                        response);
                });

        // Act
        NetworkEndpointVerificationResult result =
            await verifier.VerifyAsync(
                candidate,
                TimeSpan.FromSeconds(
                    3));

        // Assert
        RejectedNetworkEndpointCandidate rejected =
            Assert.IsType<
                RejectedNetworkEndpointCandidate>(
                    result);

        Assert.Equal(
            NetworkEndpointVerificationFailure
                .InvalidProtocolResponse,
            rejected.Failure);
    }

    [Fact]
    public async Task VerifyAsync_TimeoutCancellation_ShouldRejectCandidate()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        var verifier =
            CreateVerifier(
                async (
                    receivedCandidate,
                    timeout,
                    cancellationToken) =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "The timeout wait completed unexpectedly.");
                });

        // Act
        NetworkEndpointVerificationResult result =
            await verifier.VerifyAsync(
                candidate,
                TimeSpan.FromMilliseconds(
                    20));

        // Assert
        RejectedNetworkEndpointCandidate rejected =
            Assert.IsType<
                RejectedNetworkEndpointCandidate>(
                    result);

        Assert.Equal(
            NetworkEndpointVerificationFailure
                .TimedOut,
            rejected.Failure);
    }

    [Fact]
    public async Task VerifyAsync_CallerCancellation_ShouldPropagate()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        var verifier =
            CreateVerifier(
                async (
                    receivedCandidate,
                    timeout,
                    cancellationToken) =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "The cancellation wait completed unexpectedly.");
                });

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<NetworkEndpointVerificationResult> verificationTask =
            verifier.VerifyAsync(
                candidate,
                Timeout.InfiniteTimeSpan,
                cancellationTokenSource.Token);

        // Act
        cancellationTokenSource.Cancel();

        // Assert
        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                    () => verificationTask);

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);
    }

    [Fact]
    public async Task VerifyAsync_ConnectionTimeout_ShouldRejectCandidate()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        var verifier =
            CreateThrowingVerifier(
                new TimeoutException(
                    "Connection timed out."));

        // Act
        NetworkEndpointVerificationResult result =
            await verifier.VerifyAsync(
                candidate,
                TimeSpan.FromSeconds(
                    3));

        // Assert
        AssertRejected(
            result,
            NetworkEndpointVerificationFailure
                .TimedOut);
    }

    [Fact]
    public async Task VerifyAsync_InvalidProtocolData_ShouldRejectCandidate()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        var verifier =
            CreateThrowingVerifier(
                new InvalidDataException(
                    "Invalid protocol data."));

        // Act
        NetworkEndpointVerificationResult result =
            await verifier.VerifyAsync(
                candidate,
                TimeSpan.FromSeconds(
                    3));

        // Assert
        AssertRejected(
            result,
            NetworkEndpointVerificationFailure
                .NonHaseEndpoint);
    }

    [Fact]
    public async Task VerifyAsync_ConnectionRefused_ShouldRejectCandidate()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        var verifier =
            CreateThrowingVerifier(
                new SocketException(
                    (int)SocketError.ConnectionRefused));

        // Act
        NetworkEndpointVerificationResult result =
            await verifier.VerifyAsync(
                candidate,
                TimeSpan.FromSeconds(
                    3));

        // Assert
        AssertRejected(
            result,
            NetworkEndpointVerificationFailure
                .Unreachable);
    }

    [Fact]
    public async Task VerifyAsync_TransportFailure_ShouldRejectCandidate()
    {
        // Arrange
        NetworkEndpointCandidate candidate =
            CreateCandidate();

        var verifier =
            CreateThrowingVerifier(
                new IOException(
                    "The connection was closed."));

        // Act
        NetworkEndpointVerificationResult result =
            await verifier.VerifyAsync(
                candidate,
                TimeSpan.FromSeconds(
                    3));

        // Assert
        AssertRejected(
            result,
            NetworkEndpointVerificationFailure
                .Unreachable);
    }

    [Fact]
    public async Task VerifyAsync_NullCandidate_ShouldThrow()
    {
        // Arrange
        var verifier =
            CreateVerifier(
                (
                    candidate,
                    timeout,
                    cancellationToken) =>
                        throw new InvalidOperationException(
                            "The exchange must not be called."));

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                null!,
                TimeSpan.FromSeconds(
                    3));
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentNullException>(
                Act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task VerifyAsync_InvalidTimeout_ShouldThrow(
        int timeoutMilliseconds)
    {
        // Arrange
        var verifier =
            CreateVerifier(
                (
                    candidate,
                    timeout,
                    cancellationToken) =>
                        throw new InvalidOperationException(
                            "The exchange must not be called."));

        // Act
        Task Act()
        {
            return verifier.VerifyAsync(
                CreateCandidate(),
                TimeSpan.FromMilliseconds(
                    timeoutMilliseconds));
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentOutOfRangeException>(
                Act);
    }

    [Fact]
    public void Constructor_NegativeMaximumPayloadLength_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TcpProtocolNetworkEndpointCandidateVerifier(
                -1);
        }

        // Assert
        Assert.Throws<
            ArgumentOutOfRangeException>(
                Act);
    }

    private static
        TcpProtocolNetworkEndpointCandidateVerifier CreateVerifier(
            Func<
                NetworkEndpointCandidate,
                TimeSpan,
                CancellationToken,
                Task<ProtocolMessage>> exchange)
    {
        return new TcpProtocolNetworkEndpointCandidateVerifier(
            exchange);
    }

    private static
        TcpProtocolNetworkEndpointCandidateVerifier
            CreateThrowingVerifier(
                Exception exception)
    {
        return CreateVerifier(
            (
                candidate,
                timeout,
                cancellationToken) =>
                    Task.FromException<
                        ProtocolMessage>(
                            exception));
    }

    private static void AssertRejected(
        NetworkEndpointVerificationResult result,
        NetworkEndpointVerificationFailure expectedFailure)
    {
        RejectedNetworkEndpointCandidate rejected =
            Assert.IsType<
                RejectedNetworkEndpointCandidate>(
                    result);

        Assert.Equal(
            expectedFailure,
            rejected.Failure);
    }

    private static NetworkEndpointCandidate CreateCandidate()
    {
        return new NetworkEndpointCandidate(
            "doit-esp32-devkitc-v4-01",
            IPAddress.Parse(
                "192.168.0.223"),
            5000);
    }
}
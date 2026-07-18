using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;
using Hase.Transport.Tcp;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    TcpProtocolNetworkEndpointCandidateVerifierEndToEndTests
{
    private const int MaximumPayloadLength =
        4096;

    [Fact]
    public async Task VerifyAsync_LoopbackTcpEndpoint_ShouldVerifyEndpoint()
    {
        // Arrange
        var expectedEndpointId =
            new EndpointId(
                "loopback.environment.endpoint");

        using var listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        listener.Start();

        int port =
            ((IPEndPoint)listener.LocalEndpoint)
            .Port;

        using var testCancellationTokenSource =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    5));

        Task serverTask =
            ServeSingleDiscoverExchangeAsync(
                listener,
                expectedEndpointId,
                testCancellationTokenSource.Token);

        var candidate =
            new NetworkEndpointCandidate(
                "loopback-hase-endpoint",
                IPAddress.Loopback,
                port);

        var verifier =
            new TcpProtocolNetworkEndpointCandidateVerifier(
                MaximumPayloadLength);

        // Act
        NetworkEndpointVerificationResult result =
            await verifier.VerifyAsync(
                candidate,
                TimeSpan.FromSeconds(
                    3),
                testCancellationTokenSource.Token);

        await serverTask;

        // Assert
        VerifiedNetworkEndpoint verifiedEndpoint =
            Assert.IsType<
                VerifiedNetworkEndpoint>(
                    result);

        Assert.Same(
            candidate,
            verifiedEndpoint.Candidate);

        Assert.Equal(
            expectedEndpointId,
            verifiedEndpoint.EndpointId);
    }

    private static async Task ServeSingleDiscoverExchangeAsync(
        TcpListener listener,
        EndpointId endpointId,
        CancellationToken cancellationToken)
    {
        using TcpClient client =
            await listener.AcceptTcpClientAsync(
                cancellationToken);

        NetworkStream stream =
            client.GetStream();

        byte[] requestBytes =
            await TcpFrameReader.ReadAsync(
                stream,
                MaximumPayloadLength,
                cancellationToken);

        var envelopeCodec =
            new ProtocolEnvelopeByteCodec();

        var payloadCodec =
            new BinaryProtocolPayloadCodec();

        ProtocolEnvelope requestEnvelope =
            envelopeCodec.Decode(
                requestBytes);

        ProtocolMessage requestMessage =
            payloadCodec.Decode(
                requestEnvelope);

        DiscoverRequest discoverRequest =
            Assert.IsType<
                DiscoverRequest>(
                    requestMessage);

        var response =
            new DiscoverResponse(
                discoverRequest.CorrelationId,
                endpointId,
                [
                    new InstrumentId(
                        "environment.sensor")
                ]);

        ProtocolEnvelope responseEnvelope =
            payloadCodec.Encode(
                response);

        byte[] responseBytes =
            envelopeCodec.Encode(
                responseEnvelope);

        byte[] responseFrame =
            TcpFrameCodec.Encode(
                responseBytes);

        await stream.WriteAsync(
            responseFrame.AsMemory(),
            cancellationToken);

        await stream.FlushAsync(
            cancellationToken);
    }
}
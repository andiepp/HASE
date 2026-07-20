
using System.Net;
using System.Net.Sockets;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Tcp;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class TcpNativeEndpointBootstrapClientEndToEndTests
{
    private const int MaximumPayloadLength =
        4096;

    [Fact]
    public async Task BootstrapAsync_LoopbackTcpEndpoint_ShouldBootstrapAndClose()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "loopback.bootstrap.endpoint");

        var descriptor =
            new EndpointDescriptor(
                endpointId);

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
            ServeBootstrapAsync(
                listener,
                endpointId,
                descriptor,
                testCancellationTokenSource.Token);

        NetworkEndpointConnectionDefinition definition =
            NetworkEndpointConnectionDefinition
                .FromConfiguration(
                    new TcpTransportOptions(
                        IPAddress.Loopback.ToString(),
                        port,
                        TimeSpan.FromSeconds(
                            3)),
                    endpointId);

        var client =
            new TcpNativeEndpointBootstrapClient(
                new ProtocolNativeEndpointBootstrapper(),
                MaximumPayloadLength);

        // Act
        NativeEndpointBootstrapResult result =
            await client.BootstrapAsync(
                definition,
                testCancellationTokenSource.Token);

        await serverTask;

        // Assert
        Assert.Equal(
            endpointId,
            result.EndpointId);

        Assert.Equal(
            endpointId,
            result.Descriptor.Id);

        Assert.Empty(
            result.Descriptor.Instruments);
    }

    private static async Task ServeBootstrapAsync(
        TcpListener listener,
        EndpointId endpointId,
        EndpointDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        using TcpClient client =
            await listener.AcceptTcpClientAsync(
                cancellationToken);

        NetworkStream stream =
            client.GetStream();

        var envelopeCodec =
            new ProtocolEnvelopeByteCodec();

        var payloadCodec =
            new BinaryProtocolPayloadCodec();

        ProtocolMessage firstRequest =
            await ReadRequestAsync(
                stream,
                envelopeCodec,
                payloadCodec,
                cancellationToken);

        DiscoverRequest discoverRequest =
            Assert.IsType<DiscoverRequest>(
                firstRequest);

        await WriteResponseAsync(
            stream,
            new DiscoverResponse(
                discoverRequest.CorrelationId,
                endpointId,
                Array.Empty<InstrumentId>()),
            envelopeCodec,
            payloadCodec,
            cancellationToken);

        ProtocolMessage secondRequest =
            await ReadRequestAsync(
                stream,
                envelopeCodec,
                payloadCodec,
                cancellationToken);

        ReadEndpointDescriptorRequest descriptorRequest =
            Assert.IsType<ReadEndpointDescriptorRequest>(
                secondRequest);

        Assert.Equal(
            endpointId,
            descriptorRequest.EndpointId);

        await WriteResponseAsync(
            stream,
            new ReadEndpointDescriptorResponse(
                descriptorRequest.CorrelationId,
                ProtocolResult.Success,
                descriptor),
            envelopeCodec,
            payloadCodec,
            cancellationToken);

        byte[] closeProbe =
            new byte[1];

        int closeProbeLength =
            await stream.ReadAsync(
                closeProbe.AsMemory(),
                cancellationToken);

        Assert.Equal(
            0,
            closeProbeLength);
    }

    private static async Task<ProtocolMessage> ReadRequestAsync(
        NetworkStream stream,
        ProtocolEnvelopeByteCodec envelopeCodec,
        BinaryProtocolPayloadCodec payloadCodec,
        CancellationToken cancellationToken)
    {
        byte[] requestBytes =
            await TcpFrameReader.ReadAsync(
                stream,
                MaximumPayloadLength,
                cancellationToken);

        ProtocolEnvelope requestEnvelope =
            envelopeCodec.Decode(
                requestBytes);

        return payloadCodec.Decode(
            requestEnvelope);
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        ProtocolMessage response,
        ProtocolEnvelopeByteCodec envelopeCodec,
        BinaryProtocolPayloadCodec payloadCodec,
        CancellationToken cancellationToken)
    {
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
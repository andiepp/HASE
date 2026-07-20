using System.Net;
using System.Net.Sockets;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Tcp;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NativeNetworkEndpointAttachmentServiceEndToEndTests
{
    private const int MaximumPayloadLength =
        4096;

    [Fact]
    public async Task AttachAsync_LoopbackTcpEndpoint_ShouldAttachAndShutdown()
    {
        var endpointId =
            new EndpointId(
                "loopback.attachment.endpoint");

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
                    10));

        Task serverTask =
            ServeEndpointAsync(
                listener,
                endpointId,
                descriptor,
                testCancellationTokenSource.Token);

        var runtimeContext =
            new RuntimeContext();

        var service =
            new NativeNetworkEndpointAttachmentService(
                runtimeContext,
                new ProtocolNativeEndpointBootstrapper(),
                new ProtocolRuntimeEndpointSynchronizer(
                    new EndpointDescriptorCompatibilityValidator()),
                new DefaultRuntimeEndpointReconnectPolicy(),
                MaximumPayloadLength);

        NetworkEndpointConnectionDefinition connectionDefinition =
            NetworkEndpointConnectionDefinition.FromConfiguration(
                new TcpTransportOptions(
                    IPAddress.Loopback.ToString(),
                    port,
                    TimeSpan.FromSeconds(
                        3)),
                endpointId);

        var request =
            new EndpointAttachmentRequest(
                connectionDefinition,
                EndpointProvidedDescriptorSource.Instance);

        IEndpointAttachmentSession session =
            await service.AttachAsync(
                request,
                testCancellationTokenSource.Token);

        Assert.Same(
            request,
            session.Request);

        Assert.Equal(
            endpointId,
            session.RuntimeEndpoint.Descriptor.Id);

        Assert.Equal(
            EndpointConnectionState.Ready,
            session.RuntimeEndpoint.ConnectionStatus.State);

        Assert.Same(
            session.RuntimeEndpoint,
            Assert.Single(
                runtimeContext.Endpoints));

        await session.ShutdownAsync(
            testCancellationTokenSource.Token);

        await serverTask;

        Assert.Empty(
            runtimeContext.Endpoints);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            session.RuntimeEndpoint.ConnectionStatus.State);
    }

    private static async Task ServeEndpointAsync(
        TcpListener listener,
        EndpointId endpointId,
        EndpointDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        await ServeBootstrapConnectionAsync(
            listener,
            endpointId,
            descriptor,
            cancellationToken);

        await ServeOperationalConnectionAsync(
            listener,
            endpointId,
            descriptor,
            cancellationToken);
    }

    private static async Task ServeBootstrapConnectionAsync(
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

        await ServeDiscoverAsync(
            stream,
            endpointId,
            envelopeCodec,
            payloadCodec,
            cancellationToken);

        await ServeDescriptorReadAsync(
            stream,
            endpointId,
            descriptor,
            envelopeCodec,
            payloadCodec,
            cancellationToken);

        await AssertConnectionClosedAsync(
            stream,
            cancellationToken);
    }

    private static async Task ServeOperationalConnectionAsync(
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

        await ServeDiscoverAsync(
            stream,
            endpointId,
            envelopeCodec,
            payloadCodec,
            cancellationToken);

        await ServeDescriptorReadAsync(
            stream,
            endpointId,
            descriptor,
            envelopeCodec,
            payloadCodec,
            cancellationToken);

        await AssertConnectionClosedAsync(
            stream,
            cancellationToken);
    }

    private static async Task ServeDiscoverAsync(
        NetworkStream stream,
        EndpointId endpointId,
        ProtocolEnvelopeByteCodec envelopeCodec,
        BinaryProtocolPayloadCodec payloadCodec,
        CancellationToken cancellationToken)
    {
        ProtocolMessage request =
            await ReadRequestAsync(
                stream,
                envelopeCodec,
                payloadCodec,
                cancellationToken);

        DiscoverRequest discoverRequest =
            Assert.IsType<DiscoverRequest>(
                request);

        await WriteResponseAsync(
            stream,
            new DiscoverResponse(
                discoverRequest.CorrelationId,
                endpointId,
                Array.Empty<InstrumentId>()),
            envelopeCodec,
            payloadCodec,
            cancellationToken);
    }

    private static async Task ServeDescriptorReadAsync(
        NetworkStream stream,
        EndpointId endpointId,
        EndpointDescriptor descriptor,
        ProtocolEnvelopeByteCodec envelopeCodec,
        BinaryProtocolPayloadCodec payloadCodec,
        CancellationToken cancellationToken)
    {
        ProtocolMessage request =
            await ReadRequestAsync(
                stream,
                envelopeCodec,
                payloadCodec,
                cancellationToken);

        ReadEndpointDescriptorRequest descriptorRequest =
            Assert.IsType<ReadEndpointDescriptorRequest>(
                request);

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
    }

    private static async Task AssertConnectionClosedAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
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
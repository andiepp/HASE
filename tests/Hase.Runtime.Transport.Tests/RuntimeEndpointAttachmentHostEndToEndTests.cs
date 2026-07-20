using System.Net;
using System.Net.Sockets;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Tcp;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointAttachmentHostEndToEndTests
{
    private const int MaximumPayloadLength =
        4096;

    [Fact]
    public async Task Inventory_LoopbackTcpEndpoint_ShouldAttachFindAndDetach()
    {
        var endpointId =
            new EndpointId(
                "loopback.inventory.endpoint");

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

        await using RuntimeEndpointAttachmentHost host =
            RuntimeEndpointAttachmentHost.CreateNativeNetwork(
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

        RuntimeEndpointAttachmentInventoryEntry entry =
            await host.AttachmentInventory.AttachAsync(
                request,
                testCancellationTokenSource.Token);

        Assert.Equal(
            endpointId,
            entry.EndpointId);

        Assert.Equal(
            EndpointConnectionState.Ready,
            entry.RuntimeEndpoint.ConnectionStatus.State);

        Assert.Same(
            entry,
            host.AttachmentInventory.Find(
                endpointId));

        Assert.Same(
            entry,
            Assert.Single(
                host.AttachmentInventory.List()));

        Assert.Same(
            entry.RuntimeEndpoint,
            Assert.Single(
                host.RuntimeContext.Endpoints));

        bool detached =
            await host.AttachmentInventory.DetachAsync(
                endpointId,
                testCancellationTokenSource.Token);

        await serverTask;

        Assert.True(
            detached);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            entry.RuntimeEndpoint.ConnectionStatus.State);

        Assert.Null(
            host.AttachmentInventory.Find(
                endpointId));

        Assert.Empty(
            host.AttachmentInventory.List());

        Assert.Empty(
            host.RuntimeContext.Endpoints);
    }

    private static async Task ServeEndpointAsync(
        TcpListener listener,
        EndpointId endpointId,
        EndpointDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        await ServeConnectionAsync(
            listener,
            endpointId,
            descriptor,
            cancellationToken);

        await ServeConnectionAsync(
            listener,
            endpointId,
            descriptor,
            cancellationToken);
    }

    private static async Task ServeConnectionAsync(
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
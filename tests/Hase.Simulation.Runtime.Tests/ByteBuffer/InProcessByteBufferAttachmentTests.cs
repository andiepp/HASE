using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Simulation.Runtime.ByteBuffer;

namespace Hase.Simulation.Runtime.Tests.ByteBuffer;

public sealed class InProcessByteBufferAttachmentTests
{
    private static readonly EndpointId EndpointId =
        new(
            "simulation-byte-buffer-validation");

    [Fact]
    public async Task AttachAsync_PublishesReadySynchronizedEndpoint()
    {
        RuntimeContext context =
            new();

        var service =
            new InProcessEndpointAttachmentService(
                context);

        await using IEndpointAttachmentSession session =
            await service.AttachAsync(
                CreateRequest());

        Assert.Same(
            session.RuntimeEndpoint,
            Assert.Single(
                context.Endpoints));

        Assert.Equal(
            EndpointId,
            session.RuntimeEndpoint.Descriptor.Id);

        Assert.Equal(
            EndpointConnectionState.Ready,
            session.RuntimeEndpoint.ConnectionStatus.State);

        object? value =
            session.RuntimeEndpoint
                .FindInstrument(
                    ByteBufferDescriptorFactory.InstrumentId)!
                .FindProperty(
                    ByteBufferDescriptorFactory.ValuePropertyId)!
                .CurrentValue?
                .Value;

        Assert.Equal(
            new ByteArrayValue(
                ReadOnlySpan<byte>.Empty),
            value);
    }

    [Fact]
    public async Task CommandOperations_ReplaceBufferAndReturnAcceptedBytes()
    {
        var service =
            new InProcessEndpointAttachmentService(
                new RuntimeContext());

        await using IEndpointAttachmentSession session =
            await service.AttachAsync(
                CreateRequest());

        var payload =
            new ByteArrayValue(
                new byte[]
                {
                    0x00,
                    0x7f,
                    0xff
                });

        EndpointAttachmentCommandOperationResult result =
            await session.CommandOperations.ExecuteAsync(
                ByteBufferDescriptorFactory.InstrumentId,
                ByteBufferDescriptorFactory.ReplaceCommandPath,
                payload);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            payload,
            result.ReturnValue);
    }

    [Fact]
    public async Task PropertyOperations_ReadReplacedBuffer()
    {
        var service =
            new InProcessEndpointAttachmentService(
                new RuntimeContext());

        await using IEndpointAttachmentSession session =
            await service.AttachAsync(
                CreateRequest());

        var payload =
            new ByteArrayValue(
                new byte[]
                {
                    0x12,
                    0x34
                });

        await session.CommandOperations.ExecuteAsync(
            ByteBufferDescriptorFactory.InstrumentId,
            ByteBufferDescriptorFactory.ReplaceCommandPath,
            payload);

        EndpointAttachmentPropertyOperationResult read =
            await session.PropertyOperations.ReadAsync(
                ByteBufferDescriptorFactory.InstrumentId,
                ByteBufferDescriptorFactory.ValuePropertyId);

        Assert.True(
            read.IsSuccess);

        Assert.Equal(
            payload,
            read.ConfirmedValue?.Value);
    }

    [Fact]
    public async Task ShutdownAsync_RemovesEndpointAndMarksItDisconnected()
    {
        RuntimeContext context =
            new();

        var service =
            new InProcessEndpointAttachmentService(
                context);

        IEndpointAttachmentSession session =
            await service.AttachAsync(
                CreateRequest());

        RuntimeEndpoint endpoint =
            session.RuntimeEndpoint;

        await session.ShutdownAsync();
        await session.ShutdownAsync();

        Assert.Empty(
            context.Endpoints);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task AttachAsync_WrongDescriptorSource_RejectsRequest()
    {
        var service =
            new InProcessEndpointAttachmentService(
                new RuntimeContext());

        EndpointAttachmentRequest request =
            new(
                CreateDefinition(),
                EndpointProvidedDescriptorSource.Instance);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AttachAsync(
                request));
    }

    private static EndpointAttachmentRequest CreateRequest()
    {
        return new EndpointAttachmentRequest(
            CreateDefinition(),
            InProcessEndpointDescriptorSource.Instance);
    }

    private static InProcessEndpointConnectionDefinition CreateDefinition()
    {
        return new InProcessEndpointConnectionDefinition(
            new EndpointDescriptor(
                EndpointId,
                [
                    ByteBufferDescriptorFactory.CreateDescriptor()
                ]),
            runtimeInstrument =>
                new ByteBufferInstrumentExecutor(
                    new ByteBufferSimulation(),
                    runtimeInstrument));
    }
}

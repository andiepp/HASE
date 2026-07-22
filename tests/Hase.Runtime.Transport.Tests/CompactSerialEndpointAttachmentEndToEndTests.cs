using System.Threading.Channels;
using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactSerialEndpointAttachmentEndToEndTests
{
    [Fact]
    public async Task AttachAndDetach_ShouldOwnTemporaryAndOperationalConnections()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "arduino-uno-01");

        var descriptorReference =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-validation"),
                version: 1);

        var definition =
            new CompactEndpointDefinition(
                descriptorReference,
                new EndpointDescriptorDefinition(),
                []);

        var definitionRepository =
            new InMemoryCompactEndpointDefinitionRepository(
                [
                    definition
                ]);

        var bootstrapStream =
            new BootstrapRespondingSerialByteStream(
                endpointId,
                descriptorReference);

        var operationalStream =
            new BootstrapRespondingSerialByteStream(
                endpointId,
                descriptorReference);

        var streamFactory =
            new SequentialSerialByteStreamFactory(
                bootstrapStream,
                operationalStream);

        var runtimeContext =
            new RuntimeContext();

        var attachmentService =
            new CompactSerialEndpointAttachmentService(
                runtimeContext,
                streamFactory,
                definitionRepository,
                new DefaultRuntimeEndpointReconnectPolicy(),
                new CompactEndpointHealthProbeOptions(
                    probeInterval:
                        TimeSpan.FromHours(
                            1),
                    probeTimeout:
                        TimeSpan.FromSeconds(
                            3)));

        await using var host =
            new RuntimeEndpointAttachmentHost(
                runtimeContext,
                attachmentService);

        var request =
            new EndpointAttachmentRequest(
                SerialEndpointConnectionDefinition.FromConfiguration(
                    new SerialTransportOptions(
                        "COM10",
                        115200),
                    endpointId),
                HostRepositoryDescriptorSource.Instance);

        // Act
        RuntimeEndpointAttachmentInventoryEntry entry =
            await host.AttachmentInventory.AttachAsync(
                request);

        // Assert
        Assert.Equal(
            2,
            streamFactory.OpenCallCount);

        Assert.Equal(
            1,
            bootstrapStream.WriteCallCount);

        Assert.Equal(
            1,
            bootstrapStream.DisposeCallCount);

        Assert.Equal(
            1,
            operationalStream.WriteCallCount);

        Assert.Equal(
            0,
            operationalStream.DisposeCallCount);

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

        Assert.Collection(
            host.AttachmentInventory.List(),
            listedEntry =>
                Assert.Same(
                    entry,
                    listedEntry));

        Assert.Collection(
            runtimeContext.Endpoints,
            runtimeEndpoint =>
                Assert.Same(
                    entry.RuntimeEndpoint,
                    runtimeEndpoint));

        // Act
        bool detached =
            await host.AttachmentInventory.DetachAsync(
                endpointId);

        // Assert
        Assert.True(
            detached);

        Assert.Equal(
            1,
            operationalStream.DisposeCallCount);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            entry.RuntimeEndpoint.ConnectionStatus.State);

        Assert.Empty(
            host.AttachmentInventory.List());

        Assert.Empty(
            runtimeContext.Endpoints);
    }

    private sealed class SequentialSerialByteStreamFactory
        : ISerialByteStreamFactory
    {
        private readonly Queue<ISerialByteStream> _streams;

        public SequentialSerialByteStreamFactory(
            params ISerialByteStream[] streams)
        {
            _streams =
                new Queue<ISerialByteStream>(
                    streams);
        }

        public int OpenCallCount
        {
            get;
            private set;
        }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                options);

            cancellationToken.ThrowIfCancellationRequested();

            OpenCallCount++;

            if (_streams.Count == 0)
            {
                throw new InvalidOperationException(
                    "No scripted serial byte stream remains.");
            }

            return ValueTask.FromResult(
                _streams.Dequeue());
        }
    }

    private sealed class BootstrapRespondingSerialByteStream
        : ISerialByteStream
    {
        private readonly EndpointId _endpointId;
        private readonly DescriptorReference _descriptorReference;

        private readonly Channel<byte> _readBytes =
            Channel.CreateUnbounded<byte>(
                new UnboundedChannelOptions
                {
                    SingleReader =
                        true,
                    SingleWriter =
                        true
                });

        public BootstrapRespondingSerialByteStream(
            EndpointId endpointId,
            DescriptorReference descriptorReference)
        {
            _endpointId =
                endpointId;

            _descriptorReference =
                descriptorReference;
        }

        public int WriteCallCount
        {
            get;
            private set;
        }

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CompactSerialFrame request =
                CompactSerialFrameCodec.Decode(
                    buffer.Span);

            CompactBootstrapRequest bootstrapRequest =
                CompactBootstrapCodec.DecodeRequest(
                    request);

            CompactSerialFrame response =
                CompactBootstrapCodec.EncodeResponse(
                    new CompactBootstrapResponse(
                        bootstrapRequest.CorrelationId,
                        _endpointId,
                        _descriptorReference));

            foreach (byte value in
                CompactSerialFrameCodec.Encode(
                    response))
            {
                if (!_readBytes.Writer.TryWrite(
                        value))
                {
                    throw new InvalidOperationException(
                        "The scripted serial stream is already completed.");
                }
            }

            WriteCallCount++;

            return ValueTask.CompletedTask;
        }

        public async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte first;

            try
            {
                first =
                    await _readBytes.Reader.ReadAsync(
                        cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return 0;
            }

            buffer.Span[0] =
                first;

            int byteCount =
                1;

            while (byteCount < buffer.Length
                   && _readBytes.Reader.TryRead(
                       out byte value))
            {
                buffer.Span[byteCount] =
                    value;

                byteCount++;
            }

            return byteCount;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            _readBytes.Writer.TryComplete();

            return ValueTask.CompletedTask;
        }
    }
}
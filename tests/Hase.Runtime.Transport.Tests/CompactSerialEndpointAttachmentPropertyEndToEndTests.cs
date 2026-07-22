using System.Threading.Channels;
using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactSerialEndpointAttachmentPropertyEndToEndTests
{
    private static readonly EndpointId EndpointId =
        new(
            "arduino-uno-01");

    private static readonly DescriptorReference DescriptorReference =
        new(
            new DescriptorId(
                "arduino-uno-validation"),
            version: 1);

    private static readonly InstrumentId InstrumentId =
        new(
            "arduino-uno-controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "built-in-led-state");

    [Fact]
    public async Task AttachAsync_ShouldSynchronizePropertyBeforePublication()
    {
        // Arrange
        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition();

        var compactDefinition =
            new CompactEndpointDefinition(
                DescriptorReference,
                descriptorDefinition,
                [
                    new CompactPropertyMapping(
                        compactPropertyId: 0x01,
                        InstrumentId,
                        PropertyId,
                        CompactPropertyValueEncoding.Boolean)
                ]);

        var definitionRepository =
            new InMemoryCompactEndpointDefinitionRepository(
                [
                    compactDefinition
                ]);

        var temporaryBootstrapStream =
            new RespondingSerialByteStream(
                EndpointId,
                DescriptorReference,
                propertyValue: true);

        var operationalStream =
            new RespondingSerialByteStream(
                EndpointId,
                DescriptorReference,
                propertyValue: true);

        var streamFactory =
            new SequentialSerialByteStreamFactory(
                temporaryBootstrapStream,
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
                    EndpointId),
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
            temporaryBootstrapStream.BootstrapRequestCount);

        Assert.Equal(
            0,
            temporaryBootstrapStream.ReadPropertyRequestCount);

        Assert.Equal(
            1,
            temporaryBootstrapStream.DisposeCallCount);

        Assert.Equal(
            1,
            operationalStream.BootstrapRequestCount);

        Assert.Equal(
            1,
            operationalStream.ReadPropertyRequestCount);

        Assert.Equal(
            EndpointConnectionState.Ready,
            entry.RuntimeEndpoint.ConnectionStatus.State);

        RuntimeProperty runtimeProperty =
            entry.RuntimeEndpoint
                .FindInstrument(
                    InstrumentId)!
                .FindProperty(
                    PropertyId)!;

        PropertyValue currentValue =
            Assert.IsType<PropertyValue>(
                runtimeProperty.CurrentValue);

        Assert.True(
            Assert.IsType<bool>(
                currentValue.Value));

        Assert.Equal(
            TimeSpan.Zero,
            currentValue.TimestampUtc.Offset);

        Assert.Equal(
            PropertyQuality.Good,
            currentValue.Quality);

        Assert.Collection(
            runtimeContext.Endpoints,
            runtimeEndpoint =>
                Assert.Same(
                    entry.RuntimeEndpoint,
                    runtimeEndpoint));

        // Act
        bool detached =
            await host.AttachmentInventory.DetachAsync(
                EndpointId);

        // Assert
        Assert.True(
            detached);

        Assert.Equal(
            1,
            operationalStream.DisposeCallCount);

        Assert.Empty(
            host.AttachmentInventory.List());

        Assert.Empty(
            runtimeContext.Endpoints);
    }

    private static EndpointDescriptorDefinition
        CreateDescriptorDefinition()
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Led",
                    "State"),
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            property
                        ])
            };

        return new EndpointDescriptorDefinition(
            metadata:
                new(),
            instruments:
            [
                instrument
            ]);
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

    private sealed class RespondingSerialByteStream
        : ISerialByteStream
    {
        private readonly EndpointId _endpointId;
        private readonly DescriptorReference _descriptorReference;
        private readonly bool _propertyValue;

        private readonly Channel<byte> _readBytes =
            Channel.CreateUnbounded<byte>(
                new UnboundedChannelOptions
                {
                    SingleReader =
                        true,
                    SingleWriter =
                        true
                });

        public RespondingSerialByteStream(
            EndpointId endpointId,
            DescriptorReference descriptorReference,
            bool propertyValue)
        {
            _endpointId =
                endpointId;

            _descriptorReference =
                descriptorReference;

            _propertyValue =
                propertyValue;
        }

        public int BootstrapRequestCount
        {
            get;
            private set;
        }

        public int ReadPropertyRequestCount
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

            CompactSerialFrame response =
                request.MessageType switch
                {
                    (byte)CompactSerialMessageType.BootstrapRequest =>
                        CreateBootstrapResponse(
                            request),

                    (byte)CompactSerialMessageType.ReadPropertyRequest =>
                        CreateReadPropertyResponse(
                            request),

                    _ =>
                        throw new InvalidDataException(
                            $"Unexpected compact request message type "
                            + $"0x{request.MessageType:X2}.")
                };

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

        private CompactSerialFrame CreateBootstrapResponse(
            CompactSerialFrame request)
        {
            CompactBootstrapRequest bootstrapRequest =
                CompactBootstrapCodec.DecodeRequest(
                    request);

            BootstrapRequestCount++;

            return CompactBootstrapCodec.EncodeResponse(
                new CompactBootstrapResponse(
                    bootstrapRequest.CorrelationId,
                    _endpointId,
                    _descriptorReference));
        }

        private CompactSerialFrame CreateReadPropertyResponse(
            CompactSerialFrame request)
        {
            CompactReadPropertyRequest readRequest =
                CompactReadPropertyCodec.DecodeRequest(
                    request);

            ReadPropertyRequestCount++;

            return CompactReadPropertyCodec.EncodeResponse(
                new CompactReadPropertyResponse(
                    readRequest.CorrelationId,
                    readRequest.PropertyId,
                    CompactPropertyReadStatus.Success,
                    value:
                        new byte[]
                        {
                            _propertyValue
                                ? (byte)0x01
                                : (byte)0x00
                        }));
        }
    }
}
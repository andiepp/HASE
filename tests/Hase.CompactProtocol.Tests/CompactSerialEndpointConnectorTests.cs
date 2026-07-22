using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Transport.Serial;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactSerialEndpointConnectorTests
{
    [Fact]
    public async Task ConnectAsync_ValidEndpoint_ShouldReturnOwnedInitializedConnection()
    {
        var transportOptions =
            new SerialTransportOptions(
                "COM5",
                115200);

        var stream =
            new TestSerialByteStream(
                endpointIdValue:
                    "uno-01",
                descriptorIdValue:
                    "arduino-uno-environment",
                descriptorVersion:
                    3);

        var streamFactory =
            new TestSerialByteStreamFactory(
                stream);

        var repository =
            new TestEndpointDescriptorRepository(
                new EndpointDescriptorDefinition());

        var connector =
            new CompactSerialEndpointConnector(
                streamFactory,
                repository);

        CompactEndpointConnection result =
            await connector.ConnectAsync(
                transportOptions,
                expectedEndpointId: null);

        Assert.Same(
            transportOptions,
            streamFactory.Options);

        Assert.Equal(
            "uno-01",
            result.Descriptor.Id.Value);

        Assert.Equal(
            "arduino-uno-environment",
            repository.Reference?.Id.Value);

        Assert.Equal(
            (ushort)3,
            repository.Reference?.Version);

        Assert.Equal(
            0,
            stream.DisposeCallCount);

        Assert.Equal(
            Hase.Transport.TransportConnectionState.Connected,
            result.Connection.State);

        await result.DisposeAsync();

        Assert.Equal(
            1,
            stream.DisposeCallCount);
    }

    [Fact]
    public async Task ConnectAsync_ExpectedEndpointIdentity_ShouldBeValidated()
    {
        var stream =
            new TestSerialByteStream(
                endpointIdValue:
                    "uno-01");

        var connector =
            new CompactSerialEndpointConnector(
                new TestSerialByteStreamFactory(
                    stream),
                new TestEndpointDescriptorRepository(
                    new EndpointDescriptorDefinition()));

        CompactEndpointConnection result =
            await connector.ConnectAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200),
                new EndpointId(
                    "uno-01"));

        Assert.Equal(
            "uno-01",
            result.Descriptor.Id.Value);

        await result.DisposeAsync();
    }

    [Fact]
    public async Task ConnectAsync_MismatchedExpectedEndpointIdentity_ShouldDisposeOpenedStream()
    {
        var stream =
            new TestSerialByteStream(
                endpointIdValue:
                    "uno-02");

        var repository =
            new TestEndpointDescriptorRepository(
                new EndpointDescriptorDefinition());

        var connector =
            new CompactSerialEndpointConnector(
                new TestSerialByteStreamFactory(
                    stream),
                repository);

        async Task Act()
        {
            _ = await connector.ConnectAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200),
                new EndpointId(
                    "uno-01"));
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Equal(
            1,
            stream.DisposeCallCount);

        Assert.Equal(
            0,
            repository.CallCount);
    }

    [Fact]
    public async Task ConnectAsync_MissingDescriptor_ShouldDisposeOpenedStream()
    {
        var stream =
            new TestSerialByteStream(
                endpointIdValue:
                    "uno-01");

        var connector =
            new CompactSerialEndpointConnector(
                new TestSerialByteStreamFactory(
                    stream),
                new TestEndpointDescriptorRepository(
                    definition: null));

        async Task Act()
        {
            _ = await connector.ConnectAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200),
                expectedEndpointId: null);
        }

        await Assert.ThrowsAsync<CompactDescriptorNotFoundException>(
            Act);

        Assert.Equal(
            1,
            stream.DisposeCallCount);
    }

    [Fact]
    public async Task ConnectAsync_CancelledBeforeOpen_ShouldForwardCancellationToFactory()
    {
        var streamFactory =
            new TestSerialByteStreamFactory(
                new TestSerialByteStream(
                    endpointIdValue:
                        "uno-01"));

        var connector =
            new CompactSerialEndpointConnector(
                streamFactory,
                new TestEndpointDescriptorRepository(
                    new EndpointDescriptorDefinition()));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await connector.ConnectAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200),
                expectedEndpointId: null,
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            1,
            streamFactory.CallCount);

        Assert.True(
            streamFactory.ReceivedCancellationRequested);
    }

    [Fact]
    public async Task ConnectAsync_CancelledAfterOpen_ShouldDisposeOpenedStream()
    {
        using var cancellationTokenSource =
            new CancellationTokenSource();

        var stream =
            new TestSerialByteStream(
                endpointIdValue:
                    "uno-01");

        var streamFactory =
            new TestSerialByteStreamFactory(
                stream,
                onOpen:
                    () => cancellationTokenSource.Cancel());

        var connector =
            new CompactSerialEndpointConnector(
                streamFactory,
                new TestEndpointDescriptorRepository(
                    new EndpointDescriptorDefinition()));

        async Task Act()
        {
            _ = await connector.ConnectAsync(
                new SerialTransportOptions(
                    "COM5",
                    115200),
                expectedEndpointId: null,
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            1,
            stream.DisposeCallCount);
    }

    [Fact]
    public void Constructor_NullSerialByteStreamFactory_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactSerialEndpointConnector(
                null!,
                new TestEndpointDescriptorRepository(
                    new EndpointDescriptorDefinition()));
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullDescriptorRepository_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactSerialEndpointConnector(
                new TestSerialByteStreamFactory(
                    new TestSerialByteStream(
                        endpointIdValue:
                            "uno-01")),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task ConnectAsync_NullTransportOptions_ShouldThrowWithoutOpeningStream()
    {
        var streamFactory =
            new TestSerialByteStreamFactory(
                new TestSerialByteStream(
                    endpointIdValue:
                        "uno-01"));

        var connector =
            new CompactSerialEndpointConnector(
                streamFactory,
                new TestEndpointDescriptorRepository(
                    new EndpointDescriptorDefinition()));

        async Task Act()
        {
            _ = await connector.ConnectAsync(
                null!,
                expectedEndpointId: null);
        }

        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);

        Assert.Equal(
            0,
            streamFactory.CallCount);
    }

    private sealed class TestSerialByteStreamFactory
        : ISerialByteStreamFactory
    {
        private readonly ISerialByteStream _stream;
        private readonly Action? _onOpen;

        public TestSerialByteStreamFactory(
            ISerialByteStream stream,
            Action? onOpen = null)
        {
            _stream =
                stream;

            _onOpen =
                onOpen;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public SerialTransportOptions? Options
        {
            get;
            private set;
        }

        public bool ReceivedCancellationRequested
        {
            get;
            private set;
        }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            Options =
                options;

            ReceivedCancellationRequested =
                cancellationToken.IsCancellationRequested;

            cancellationToken.ThrowIfCancellationRequested();

            _onOpen?.Invoke();

            return ValueTask.FromResult(
                _stream);
        }
    }

    private sealed class TestEndpointDescriptorRepository
        : IEndpointDescriptorRepository
    {
        private readonly EndpointDescriptorDefinition? _definition;

        public TestEndpointDescriptorRepository(
            EndpointDescriptorDefinition? definition)
        {
            _definition =
                definition;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public DescriptorReference? Reference
        {
            get;
            private set;
        }

        public ValueTask<EndpointDescriptorDefinition?> FindAsync(
            DescriptorReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            Reference =
                reference;

            return ValueTask.FromResult(
                _definition);
        }
    }

    private sealed class TestSerialByteStream
        : ISerialByteStream
    {
        private readonly string _endpointIdValue;
        private readonly string _descriptorIdValue;
        private readonly ushort _descriptorVersion;

        private byte[] _responseBytes =
            [];

        private int _readPosition;

        public TestSerialByteStream(
            string endpointIdValue,
            string descriptorIdValue = "arduino-uno-environment",
            ushort descriptorVersion = 3)
        {
            _endpointIdValue =
                endpointIdValue;

            _descriptorIdValue =
                descriptorIdValue;

            _descriptorVersion =
                descriptorVersion;
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

            CompactSerialFrame requestFrame =
                CompactSerialFrameCodec.Decode(
                    buffer.Span);

            CompactBootstrapRequest request =
                CompactBootstrapCodec.DecodeRequest(
                    requestFrame);

            var response =
                new CompactBootstrapResponse(
                    request.CorrelationId,
                    new EndpointId(
                        _endpointIdValue),
                    new DescriptorReference(
                        new DescriptorId(
                            _descriptorIdValue),
                        _descriptorVersion));

            _responseBytes =
                CompactSerialFrameCodec.Encode(
                    CompactBootstrapCodec.EncodeResponse(
                        response));

            _readPosition =
                0;

            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int available =
                _responseBytes.Length
                - _readPosition;

            if (available == 0)
            {
                return ValueTask.FromResult(
                    0);
            }

            int count =
                Math.Min(
                    buffer.Length,
                    available);

            _responseBytes
                .AsMemory(
                    _readPosition,
                    count)
                .CopyTo(
                    buffer);

            _readPosition +=
                count;

            return ValueTask.FromResult(
                count);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }
}
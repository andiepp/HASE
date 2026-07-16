using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolRuntimeEndpointSynchronizerTests
{
    [Fact]
    public void Constructor_NullCompatibilityValidator_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new ProtocolRuntimeEndpointSynchronizer(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "compatibilityValidator",
            exception.ParamName);
    }

    [Fact]
    public async Task SynchronizeAsync_NullConnection_ShouldThrow()
    {
        // Arrange
        var synchronizer =
            CreateSynchronizer();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                CreateDescriptor());

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                null!,
                runtimeEndpoint);
        }

        // Assert
        ArgumentNullException exception =
            await Assert.ThrowsAsync<ArgumentNullException>(
                Act);

        Assert.Equal(
            "connection",
            exception.ParamName);
    }

    [Fact]
    public async Task SynchronizeAsync_NullRuntimeEndpoint_ShouldThrow()
    {
        // Arrange
        var synchronizer =
            CreateSynchronizer();

        var connection =
            new TestTransportConnection();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                null!);
        }

        // Assert
        ArgumentNullException exception =
            await Assert.ThrowsAsync<ArgumentNullException>(
                Act);

        Assert.Equal(
            "runtimeEndpoint",
            exception.ParamName);

        Assert.Equal(
            0,
            connection.ExchangeCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_CancelledBeforeExchange_ShouldThrow()
    {
        // Arrange
        var synchronizer =
            CreateSynchronizer();

        var connection =
            new TestTransportConnection();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                CreateDescriptor());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint,
                cancellationTokenSource.Token);
        }

        // Assert
        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                Act);

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);

        Assert.Equal(
            0,
            connection.ExchangeCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldSendReadEndpointDescriptorRequest()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var connection =
            new TestTransportConnection();

        connection.ResponseFactory =
            request =>
                EncodeMessage(
                    new ReadEndpointDescriptorResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        CreateDescriptor()));

        var synchronizer =
            CreateSynchronizer();

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint);

        // Assert
        Assert.Equal(
            1,
            connection.ExchangeCallCount);

        ReadEndpointDescriptorRequest request =
            connection.LastRequest
            ?? throw new InvalidOperationException(
                "The descriptor request was not captured.");

        Assert.Equal(
            ProtocolMessageType.ReadEndpointDescriptorRequest,
            request.MessageType);

        Assert.Equal(
            descriptor.Id,
            request.EndpointId);

        Assert.False(
            request.CorrelationId.IsNone);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldForwardCancellationToken()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var connection =
            new TestTransportConnection();

        connection.ResponseFactory =
            request =>
                EncodeMessage(
                    new ReadEndpointDescriptorResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        CreateDescriptor()));

        var synchronizer =
            CreateSynchronizer();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint,
            cancellationTokenSource.Token);

        // Assert
        Assert.Equal(
            cancellationTokenSource.Token,
            connection.LastCancellationToken);
    }

    [Fact]
    public async Task SynchronizeAsync_CompatibleDescriptor_ShouldSucceed()
    {
        // Arrange
        EndpointDescriptor runtimeDescriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                runtimeDescriptor);

        var connection =
            new TestTransportConnection();

        connection.ResponseFactory =
            request =>
                EncodeMessage(
                    new ReadEndpointDescriptorResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        CreateDescriptor()));

        var synchronizer =
            CreateSynchronizer();

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint);

        // Assert
        Assert.Equal(
            1,
            connection.ExchangeCallCount);

        Assert.NotNull(
            connection.LastRequest);
    }

    [Fact]
    public async Task SynchronizeAsync_WrongResponseType_ShouldThrow()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var connection =
            new TestTransportConnection();

        connection.ResponseFactory =
            request =>
                EncodeMessage(
                    new DiscoverResponse(
                        request.CorrelationId,
                        descriptor.Id,
                        Array.Empty<InstrumentId>()));

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The endpoint response did not decode as a "
            + "ReadEndpointDescriptorResponse.",
            exception.Message);
    }

    [Fact]
    public async Task SynchronizeAsync_CorrelationMismatch_ShouldThrow()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var connection =
            new TestTransportConnection();

        connection.ResponseFactory =
            request =>
                EncodeMessage(
                    new ReadEndpointDescriptorResponse(
                        CreateDifferentCorrelationId(
                            request.CorrelationId),
                        ProtocolResult.Success,
                        CreateDescriptor()));

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The endpoint-descriptor response correlation "
            + "identifier does not match the request.",
            exception.Message);
    }

    [Fact]
    public async Task SynchronizeAsync_UnsuccessfulProtocolResult_ShouldThrow()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var connection =
            new TestTransportConnection();

        connection.ResponseFactory =
            request =>
                EncodeMessage(
                    new ReadEndpointDescriptorResponse(
                        request.CorrelationId,
                        ProtocolResult.NotFound,
                        null));

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The endpoint returned descriptor result "
            + "'NotFound': (no message).",
            exception.Message);
    }

    [Fact]
    public async Task SynchronizeAsync_SuccessWithoutDescriptor_ShouldThrow()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var connection =
            new TestTransportConnection();

        connection.ResponseFactory =
            request =>
                EncodeMessage(
                    new ReadEndpointDescriptorResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        null));

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The successful endpoint-descriptor response did not "
            + "contain a descriptor.",
            exception.Message);
    }

    [Fact]
    public async Task SynchronizeAsync_IncompatibleDescriptor_ShouldThrow()
    {
        // Arrange
        EndpointDescriptor runtimeDescriptor =
            CreateDescriptor(
                displayName:
                    "Runtime Endpoint");

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                runtimeDescriptor);

        var connection =
            new TestTransportConnection();

        connection.ResponseFactory =
            request =>
                EncodeMessage(
                    new ReadEndpointDescriptorResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        CreateDescriptor(
                            displayName:
                                "Physical Endpoint")));

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The physical descriptor for endpoint 'endpoint-01' "
            + "is not strictly compatible with the existing "
            + "runtime descriptor.",
            exception.Message);
    }

    [Fact]
    public async Task SynchronizeAsync_MalformedResponseFrame_ShouldThrow()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var connection =
            new TestTransportConnection
            {
                RawResponse =
                [
                    1,
                    0,
                    (byte)ProtocolMessageRole.Response
                ]
            };

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Equal(
            1,
            connection.ExchangeCallCount);
    }

    private static ProtocolRuntimeEndpointSynchronizer
        CreateSynchronizer()
    {
        return new ProtocolRuntimeEndpointSynchronizer(
            new EndpointDescriptorCompatibilityValidator());
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        EndpointDescriptor descriptor)
    {
        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private static EndpointDescriptor CreateDescriptor(
        string displayName = "Environment Endpoint")
    {
        return new EndpointDescriptor(
            new EndpointId(
                "endpoint-01"))
        {
            Metadata =
                new EndpointMetadata
                {
                    DisplayName =
                        displayName,
                    Description =
                        "Endpoint used by protocol synchronizer tests."
                }
        };
    }

    private static CorrelationId CreateDifferentCorrelationId(
        CorrelationId correlationId)
    {
        uint value =
            correlationId.Value == uint.MaxValue
                ? 1
                : correlationId.Value + 1;

        if (value == CorrelationId.None.Value)
        {
            value =
                1;
        }

        return new CorrelationId(
            value);
    }

    private static byte[] EncodeMessage(
        ProtocolMessage message)
    {
        var payloadCodec =
            new BinaryProtocolPayloadCodec();

        ProtocolEnvelope envelope =
            payloadCodec.Encode(
                message);

        var envelopeByteCodec =
            new ProtocolEnvelopeByteCodec();

        return envelopeByteCodec.Encode(
            envelope);
    }

    private sealed class TestTransportConnection
        : ITransportConnection
    {
        private readonly BinaryProtocolPayloadCodec _payloadCodec =
            new();

        private readonly ProtocolEnvelopeByteCodec _envelopeByteCodec =
            new();

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public int ExchangeCallCount
        {
            get;
            private set;
        }

        public ReadEndpointDescriptorRequest? LastRequest
        {
            get;
            private set;
        }

        public CancellationToken LastCancellationToken
        {
            get;
            private set;
        }

        public Func<ReadEndpointDescriptorRequest, byte[]>?
            ResponseFactory
        {
            get;
            set;
        }

        public byte[]? RawResponse
        {
            get;
            set;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            ExchangeCallCount++;

            LastCancellationToken =
                cancellationToken;

            ProtocolEnvelope requestEnvelope =
                _envelopeByteCodec.Decode(
                    request);

            ProtocolMessage requestMessage =
                _payloadCodec.Decode(
                    requestEnvelope);

            LastRequest =
                requestMessage
                    as ReadEndpointDescriptorRequest
                ?? throw new InvalidDataException(
                    "The synchronizer did not send a "
                    + "ReadEndpointDescriptorRequest.");

            if (RawResponse is not null)
            {
                return Task.FromResult(
                    RawResponse);
            }

            Func<ReadEndpointDescriptorRequest, byte[]>
                responseFactory =
                    ResponseFactory
                    ?? throw new InvalidOperationException(
                        "No test response was configured.");

            return Task.FromResult(
                responseFactory(
                    LastRequest));
        }

        public void TransitionTo(
            TransportConnectionState state)
        {
            StateChanged?.Invoke(
                this,
                new TransportConnectionStateChangedEventArgs(
                    State,
                    state));
        }
    }
}

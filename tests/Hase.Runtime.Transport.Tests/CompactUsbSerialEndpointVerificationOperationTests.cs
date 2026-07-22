using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Discovery;
using Hase.Transport;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactUsbSerialEndpointVerificationOperationTests
{
    [Fact]
    public void Constructor_NullConnectionFactory_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new CompactUsbSerialEndpointVerificationOperation(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task VerifyAsync_InitializedConnection_ShouldReturnVerifiedResult()
    {
        // Arrange
        UsbSerialEndpointCandidate candidate =
            CreateCandidate();

        SerialTransportOptions transportOptions =
            CreateTransportOptions();

        CompactEndpointInitializationResult initializationResult =
            CreateInitializationResult();

        var protocolConnection =
            new StubCompactSerialProtocolConnection();

        var endpointConnection =
            new CompactEndpointConnection(
                protocolConnection,
                initializationResult);

        var connectionFactory =
            new StubCompactEndpointConnectionFactory(
                endpointConnection);

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                connectionFactory);

        // Act
        UsbSerialEndpointVerificationResult result =
            await operation.VerifyAsync(
                candidate,
                transportOptions,
                TimeSpan.FromSeconds(
                    3),
                CancellationToken.None);

        // Assert
        VerifiedUsbSerialEndpoint verifiedResult =
            Assert.IsType<VerifiedUsbSerialEndpoint>(
                result);

        Assert.Same(
            candidate,
            verifiedResult.Candidate);

        Assert.Same(
            initializationResult.EndpointId,
            verifiedResult.EndpointId);

        Assert.Same(
            initializationResult.DescriptorReference,
            verifiedResult.DescriptorReference);

        Assert.Same(
            initializationResult.DescriptorDefinition,
            verifiedResult.DescriptorDefinition);

        Assert.Same(
            transportOptions,
            connectionFactory.TransportOptions);

        Assert.Null(
            connectionFactory.ExpectedEndpointId);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    [Fact]
    public async Task VerifyAsync_LegacyConnection_ShouldDisposeAndThrow()
    {
        // Arrange
        var protocolConnection =
            new StubCompactSerialProtocolConnection();

        var endpointConnection =
            new CompactEndpointConnection(
                new EndpointDescriptor(
                    new EndpointId(
                        "arduino-uno-01")),
                protocolConnection);

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                new StubCompactEndpointConnectionFactory(
                    endpointConnection));

        // Act
        Task Act()
        {
            return operation.VerifyAsync(
                CreateCandidate(),
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    3),
                CancellationToken.None);
        }

        // Assert
        await Assert.ThrowsAsync<
            InvalidOperationException>(
                Act);

        Assert.Equal(
            1,
            protocolConnection.DisposeCallCount);
    }

    [Fact]
    public async Task VerifyAsync_ConnectionFailure_ShouldPropagate()
    {
        // Arrange
        var expectedException =
            new IOException(
                "The serial connection failed.");

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                new ThrowingCompactEndpointConnectionFactory(
                    expectedException));

        // Act
        Task Act()
        {
            return operation.VerifyAsync(
                CreateCandidate(),
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    3),
                CancellationToken.None);
        }

        // Assert
        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);
    }

    [Fact]
    public async Task VerifyAsync_PreCancelledToken_ShouldNotConnect()
    {
        // Arrange
        var connectionFactory =
            new CountingCompactEndpointConnectionFactory();

        var operation =
            new CompactUsbSerialEndpointVerificationOperation(
                connectionFactory);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task Act()
        {
            return operation.VerifyAsync(
                CreateCandidate(),
                CreateTransportOptions(),
                TimeSpan.FromSeconds(
                    3),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);

        Assert.Equal(
            0,
            connectionFactory.CallCount);
    }

    private static UsbSerialEndpointCandidate CreateCandidate()
    {
        return new UsbSerialEndpointCandidate(
            "COM10",
            vendorId: 0x2341,
            productId: 0x0043,
            productName: "Arduino Uno",
            manufacturerName: "Arduino LLC (www.arduino.cc)",
            serialNumber: "75836333537351D06110");
    }

    private static SerialTransportOptions CreateTransportOptions()
    {
        return new SerialTransportOptions(
            "COM10",
            115200);
    }

    private static CompactEndpointInitializationResult
        CreateInitializationResult()
    {
        var endpointId =
            new EndpointId(
                "arduino-uno-01");

        var descriptorReference =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-validation"),
                version: 1);

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        return new CompactEndpointInitializationResult(
            endpointId,
            descriptorReference,
            descriptorDefinition,
            descriptorDefinition.Materialize(
                endpointId));
    }

    private sealed class StubCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly CompactEndpointConnection _connection;

        public StubCompactEndpointConnectionFactory(
            CompactEndpointConnection connection)
        {
            _connection =
                connection;
        }

        public SerialTransportOptions? TransportOptions
        {
            get;
            private set;
        }

        public EndpointId? ExpectedEndpointId
        {
            get;
            private set;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            TransportOptions =
                transportOptions;

            ExpectedEndpointId =
                expectedEndpointId;

            return Task.FromResult(
                _connection);
        }
    }

    private sealed class ThrowingCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        private readonly Exception _exception;

        public ThrowingCompactEndpointConnectionFactory(
            Exception exception)
        {
            _exception =
                exception;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class CountingCompactEndpointConnectionFactory
        : ICompactEndpointConnectionFactory
    {
        public int CallCount
        {
            get;
            private set;
        }

        public Task<CompactEndpointConnection> ConnectAsync(
            SerialTransportOptions transportOptions,
            EndpointId? expectedEndpointId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            throw new InvalidOperationException(
                "A connection was not expected.");
        }
    }

    private sealed class StubCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void Invalidate()
        {
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }
}
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Transport;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointInitializationPreservationTests
{
    [Fact]
    public async Task InitializeWithResultAsync_ShouldPreserveBootstrapAndDefinition()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "uno-01");

        var descriptorReference =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 3);

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        var connection =
            new StubCompactSerialProtocolConnection(
                CompactBootstrapCodec.EncodeResponse(
                    new CompactBootstrapResponse(
                        correlationId: 1,
                        endpointId,
                        descriptorReference)));

        var repository =
            new StubEndpointDescriptorRepository(
                descriptorDefinition);

        var initializer =
            new CompactEndpointInitializer(
                connection,
                repository);

        // Act
        CompactEndpointInitializationResult result =
            await initializer.InitializeWithResultAsync(
                expectedEndpointId: null);

        // Assert
        Assert.Equal(
            endpointId,
            result.EndpointId);

        Assert.Equal(
            descriptorReference,
            result.DescriptorReference);

        Assert.Same(
            descriptorDefinition,
            result.DescriptorDefinition);

        Assert.Equal(
            result.EndpointId,
            result.Descriptor.Id);

        Assert.Same(
            result.DescriptorReference,
            repository.Reference);
    }

    [Fact]
    public void Connection_ResultAwareConstructor_ShouldExposeResult()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "uno-01");

        var descriptorDefinition =
            new EndpointDescriptorDefinition();

        var initializationResult =
            new CompactEndpointInitializationResult(
                endpointId,
                new DescriptorReference(
                    new DescriptorId(
                        "arduino-uno-environment"),
                    version: 3),
                descriptorDefinition,
                descriptorDefinition.Materialize(
                    endpointId));

        var protocolConnection =
            new StubCompactSerialProtocolConnection();

        // Act
        var connection =
            new CompactEndpointConnection(
                protocolConnection,
                initializationResult);

        // Assert
        Assert.Same(
            initializationResult,
            connection.InitializationResult);

        Assert.Same(
            initializationResult.Descriptor,
            connection.Descriptor);

        Assert.Same(
            protocolConnection,
            connection.Connection);
    }

    [Fact]
    public void Connection_LegacyConstructor_ShouldExposeNoInitializationResult()
    {
        // Arrange
        var descriptor =
            new EndpointDescriptorDefinition()
                .Materialize(
                    new EndpointId(
                        "uno-01"));

        // Act
        var connection =
            new CompactEndpointConnection(
                descriptor,
                new StubCompactSerialProtocolConnection());

        // Assert
        Assert.Null(
            connection.InitializationResult);

        Assert.Same(
            descriptor,
            connection.Descriptor);
    }

    private sealed class StubEndpointDescriptorRepository
        : IEndpointDescriptorRepository
    {
        private readonly EndpointDescriptorDefinition _definition;

        public StubEndpointDescriptorRepository(
            EndpointDescriptorDefinition definition)
        {
            _definition =
                definition;
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
            cancellationToken
                .ThrowIfCancellationRequested();

            Reference =
                reference;

            return ValueTask.FromResult<
                EndpointDescriptorDefinition?>(
                    _definition);
        }
    }

    private sealed class StubCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private readonly CompactSerialFrame? _response;

        public StubCompactSerialProtocolConnection(
            CompactSerialFrame? response = null)
        {
            _response =
                response;
        }

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

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _response
                ?? throw new InvalidOperationException(
                    "No compact response was configured."));
        }

        public void Invalidate()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
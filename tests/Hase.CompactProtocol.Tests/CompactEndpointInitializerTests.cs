using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Transport;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointInitializerTests
{
    [Fact]
    public async Task InitializeAsync_ValidBootstrap_ShouldReturnMaterializedEndpointDescriptor()
    {
        var endpointId =
            new EndpointId(
                "uno-01");

        var descriptorReference =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 3);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactBootstrapCodec.EncodeResponse(
                    new CompactBootstrapResponse(
                        correlationId: 0x2A,
                        endpointId,
                        descriptorReference)));

        var repository =
            new TestEndpointDescriptorRepository(
                new EndpointDescriptorDefinition());

        var initializer =
            new CompactEndpointInitializer(
                connection,
                repository);

        EndpointDescriptor result =
            await initializer.InitializeAsync(
                expectedEndpointId: null);

        Assert.Equal(
            endpointId.Value,
            result.Id.Value);

        Assert.NotNull(
            repository.Reference);

        Assert.Equal(
            descriptorReference.Id.Value,
            repository.Reference.Id.Value);

        Assert.Equal(
            descriptorReference.Version,
            repository.Reference.Version);

        Assert.Equal(
            1,
            repository.CallCount);

        Assert.Single(
            connection.Requests);
    }

    [Fact]
    public async Task InitializeAsync_MatchingExpectedEndpointIdentity_ShouldSucceed()
    {
        var connection =
            CreateConnection(
                endpointIdValue:
                    "uno-01");

        var repository =
            new TestEndpointDescriptorRepository(
                new EndpointDescriptorDefinition());

        var initializer =
            new CompactEndpointInitializer(
                connection,
                repository);

        EndpointDescriptor result =
            await initializer.InitializeAsync(
                new EndpointId(
                    "uno-01"));

        Assert.Equal(
            "uno-01",
            result.Id.Value);

        Assert.Equal(
            1,
            repository.CallCount);
    }

    [Fact]
    public async Task InitializeAsync_MismatchedExpectedEndpointIdentity_ShouldNotResolveDescriptor()
    {
        var connection =
            CreateConnection(
                endpointIdValue:
                    "uno-02");

        var repository =
            new TestEndpointDescriptorRepository(
                new EndpointDescriptorDefinition());

        var initializer =
            new CompactEndpointInitializer(
                connection,
                repository);

        async Task Act()
        {
            _ = await initializer.InitializeAsync(
                new EndpointId(
                    "uno-01"));
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Equal(
            0,
            repository.CallCount);
    }

    [Fact]
    public async Task InitializeAsync_ExactBootstrapDescriptorReference_ShouldBeResolved()
    {
        var descriptorReference =
            new DescriptorReference(
                new DescriptorId(
                    "arduino-uno-environment"),
                version: 7);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactBootstrapCodec.EncodeResponse(
                    new CompactBootstrapResponse(
                        correlationId: 0x2A,
                        endpointId: new EndpointId(
                            "uno-01"),
                        descriptorReference)));

        var repository =
            new TestEndpointDescriptorRepository(
                new EndpointDescriptorDefinition());

        var initializer =
            new CompactEndpointInitializer(
                connection,
                repository);

        _ = await initializer.InitializeAsync(
            expectedEndpointId: null);

        Assert.NotNull(
            repository.Reference);

        Assert.Equal(
            descriptorReference.Id.Value,
            repository.Reference.Id.Value);

        Assert.Equal(
            descriptorReference.Version,
            repository.Reference.Version);
    }

    [Fact]
    public async Task InitializeAsync_MissingDescriptorDefinition_ShouldThrow()
    {
        var initializer =
            new CompactEndpointInitializer(
                CreateConnection(
                    endpointIdValue:
                        "uno-01"),
                new TestEndpointDescriptorRepository(
                    definition: null));

        async Task Act()
        {
            _ = await initializer.InitializeAsync(
                expectedEndpointId: null);
        }

        CompactDescriptorNotFoundException exception =
            await Assert.ThrowsAsync<CompactDescriptorNotFoundException>(
                Act);

        Assert.Contains(
            "arduino-uno-environment",
            exception.Message);

        Assert.Contains(
            "3",
            exception.Message);
    }

    [Fact]
    public async Task InitializeAsync_CancelledToken_ShouldNotExchangeOrResolveDescriptor()
    {
        var connection =
            CreateConnection(
                endpointIdValue:
                    "uno-01");

        var repository =
            new TestEndpointDescriptorRepository(
                new EndpointDescriptorDefinition());

        var initializer =
            new CompactEndpointInitializer(
                connection,
                repository);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await initializer.InitializeAsync(
                expectedEndpointId: null,
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Empty(
            connection.Requests);

        Assert.Equal(
            0,
            repository.CallCount);
    }

    [Fact]
    public void Constructor_NullConnection_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEndpointInitializer(
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
            _ = new CompactEndpointInitializer(
                CreateConnection(
                    endpointIdValue:
                        "uno-01"),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static TestCompactSerialProtocolConnection CreateConnection(
        string endpointIdValue)
    {
        return new TestCompactSerialProtocolConnection(
            CompactBootstrapCodec.EncodeResponse(
                new CompactBootstrapResponse(
                    correlationId: 1,
                    endpointId: new EndpointId(
                        endpointIdValue),
                    descriptorReference: new DescriptorReference(
                        new DescriptorId(
                            "arduino-uno-environment"),
                        version: 3))));
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

    private sealed class TestCompactSerialProtocolConnection
        : ICompactSerialProtocolConnection
    {
        private readonly CompactSerialFrame _response;

        public TestCompactSerialProtocolConnection(
            CompactSerialFrame response)
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

        public List<CompactSerialFrame> Requests
        {
            get;
        } =
            [];

        public Task<CompactSerialFrame> ExchangeAsync(
            CompactSerialFrame request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Requests.Add(
                request);

            return Task.FromResult(
                _response);
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
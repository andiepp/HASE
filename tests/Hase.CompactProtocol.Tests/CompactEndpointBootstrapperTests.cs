using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Transport;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactEndpointBootstrapperTests
{
    [Fact]
    public async Task BootstrapAsync_ValidResponse_ShouldReturnAuthoritativeResult()
    {
        var response =
            CreateResponse(
                endpointIdValue:
                    "uno-01",
                correlationId:
                    0x2A);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactBootstrapCodec.EncodeResponse(
                    response));

        var bootstrapper =
            new CompactEndpointBootstrapper(
                connection,
                correlationIdFactory:
                    () => 0x2A);

        CompactBootstrapResponse result =
            await bootstrapper.BootstrapAsync(
                expectedEndpointId: null);

        Assert.Equal(
            response,
            result);

        CompactSerialFrame requestFrame =
            Assert.Single(
                connection.Requests);

        CompactBootstrapRequest request =
            CompactBootstrapCodec.DecodeRequest(
                requestFrame);

        Assert.Equal(
            0x2A,
            request.CorrelationId);
    }

    [Fact]
    public async Task BootstrapAsync_MatchingExpectedIdentity_ShouldSucceed()
    {
        var response =
            CreateResponse(
                endpointIdValue:
                    "uno-01",
                correlationId:
                    0x2A);

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactBootstrapCodec.EncodeResponse(
                    response));

        var bootstrapper =
            new CompactEndpointBootstrapper(
                connection,
                correlationIdFactory:
                    () => 0x2A);

        CompactBootstrapResponse result =
            await bootstrapper.BootstrapAsync(
                new EndpointId(
                    "uno-01"));

        Assert.Equal(
            "uno-01",
            result.EndpointId.Value);
    }

    [Fact]
    public async Task BootstrapAsync_MismatchedExpectedIdentity_ShouldThrow()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CompactBootstrapCodec.EncodeResponse(
                    CreateResponse(
                        endpointIdValue:
                            "uno-02",
                        correlationId:
                            0x2A)));

        var bootstrapper =
            new CompactEndpointBootstrapper(
                connection,
                correlationIdFactory:
                    () => 0x2A);

        async Task Act()
        {
            _ = await bootstrapper.BootstrapAsync(
                new EndpointId(
                    "uno-01"));
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task BootstrapAsync_WrongResponseType_ShouldThrow()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                new CompactSerialFrame(
                    (byte)CompactSerialMessageType.BootstrapRequest,
                    correlationId: 0x2A,
                    payload: []));

        var bootstrapper =
            new CompactEndpointBootstrapper(
                connection,
                correlationIdFactory:
                    () => 0x2A);

        async Task Act()
        {
            _ = await bootstrapper.BootstrapAsync(
                expectedEndpointId: null);
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task BootstrapAsync_CancelledToken_ShouldNotAllocateOrExchange()
    {
        int allocatorCallCount =
            0;

        var connection =
            new TestCompactSerialProtocolConnection(
                CompactBootstrapCodec.EncodeResponse(
                    CreateResponse(
                        endpointIdValue:
                            "uno-01",
                        correlationId:
                            0x2A)));

        var bootstrapper =
            new CompactEndpointBootstrapper(
                connection,
                correlationIdFactory:
                    () =>
                    {
                        allocatorCallCount++;

                        return 0x2A;
                    });

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await bootstrapper.BootstrapAsync(
                expectedEndpointId: null,
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            0,
            allocatorCallCount);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public async Task BootstrapAsync_ZeroAllocatedCorrelation_ShouldThrowWithoutExchange()
    {
        var connection =
            new TestCompactSerialProtocolConnection(
                CompactBootstrapCodec.EncodeResponse(
                    CreateResponse(
                        endpointIdValue:
                            "uno-01",
                        correlationId:
                            0x2A)));

        var bootstrapper =
            new CompactEndpointBootstrapper(
                connection,
                correlationIdFactory:
                    () => 0);

        async Task Act()
        {
            _ = await bootstrapper.BootstrapAsync(
                expectedEndpointId: null);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Empty(
            connection.Requests);
    }

    [Fact]
    public void Constructor_NullConnection_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEndpointBootstrapper(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullCorrelationFactory_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactEndpointBootstrapper(
                new TestCompactSerialProtocolConnection(
                    new CompactSerialFrame(
                        (byte)CompactSerialMessageType.BootstrapResponse,
                        correlationId: 0x2A,
                        payload: [])),
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactBootstrapResponse CreateResponse(
        string endpointIdValue,
        byte correlationId)
    {
        return new CompactBootstrapResponse(
            correlationId,
            new EndpointId(
                endpointIdValue),
            new DescriptorReference(
                new DescriptorId(
                    "env-uno"),
                version: 1));
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
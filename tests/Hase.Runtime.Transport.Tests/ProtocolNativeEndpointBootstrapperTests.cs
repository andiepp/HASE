using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolNativeEndpointBootstrapperTests
{
    [Fact]
    public async Task BootstrapAsync_ValidResponses_ShouldReturnResult()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "bootstrap-endpoint");

        var descriptor =
            new EndpointDescriptor(
                endpointId);

        var connection =
            new ScriptedProtocolConnection(
                request =>
                    request switch
                    {
                        DiscoverRequest discoverRequest =>
                            new DiscoverResponse(
                                discoverRequest.CorrelationId,
                                endpointId,
                                Array.Empty<InstrumentId>()),

                        ReadEndpointDescriptorRequest descriptorRequest =>
                            new ReadEndpointDescriptorResponse(
                                descriptorRequest.CorrelationId,
                                ProtocolResult.Success,
                                descriptor),

                        _ =>
                            throw new InvalidOperationException(
                                "Unexpected bootstrap request.")
                    });

        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        // Act
        NativeEndpointBootstrapResult result =
            await bootstrapper.BootstrapAsync(
                connection,
                endpointId);

        // Assert
        Assert.Same(
            endpointId,
            result.EndpointId);

        Assert.Same(
            descriptor,
            result.Descriptor);

        Assert.Equal(
            2,
            connection.Requests.Count);

        DiscoverRequest discoverRequest =
            Assert.IsType<DiscoverRequest>(
                connection.Requests[0]);

        ReadEndpointDescriptorRequest descriptorRequest =
            Assert.IsType<ReadEndpointDescriptorRequest>(
                connection.Requests[1]);

        Assert.NotEqual(
            CorrelationId.None,
            discoverRequest.CorrelationId);

        Assert.NotEqual(
            CorrelationId.None,
            descriptorRequest.CorrelationId);

        Assert.NotEqual(
            discoverRequest.CorrelationId,
            descriptorRequest.CorrelationId);

        Assert.Same(
            endpointId,
            descriptorRequest.EndpointId);
    }

    [Fact]
    public async Task BootstrapAsync_NoExpectedIdentity_ShouldAcceptDiscovery()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "discovered-endpoint");

        var descriptor =
            new EndpointDescriptor(
                endpointId);

        var connection =
            CreateSuccessfulConnection(
                endpointId,
                descriptor);

        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        // Act
        NativeEndpointBootstrapResult result =
            await bootstrapper.BootstrapAsync(
                connection,
                expectedEndpointId: null);

        // Assert
        Assert.Same(
            endpointId,
            result.EndpointId);
    }

    [Fact]
    public async Task BootstrapAsync_NullConnection_ShouldThrow()
    {
        // Arrange
        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                null!,
                expectedEndpointId: null);
        }

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task BootstrapAsync_UnexpectedIdentity_ShouldThrow()
    {
        // Arrange
        var actualEndpointId =
            new EndpointId(
                "actual-endpoint");

        var connection =
            CreateSuccessfulConnection(
                actualEndpointId,
                new EndpointDescriptor(
                    actualEndpointId));

        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                connection,
                new EndpointId(
                    "expected-endpoint"));
        }

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Single(
            connection.Requests);
    }

    [Fact]
    public async Task BootstrapAsync_UnexpectedDiscoverResponse_ShouldThrow()
    {
        // Arrange
        var connection =
            new ScriptedProtocolConnection(
                request =>
                    new ReadEndpointDescriptorResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        null));

        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                connection,
                expectedEndpointId: null);
        }

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Single(
            connection.Requests);
    }

    [Fact]
    public async Task BootstrapAsync_UnexpectedDescriptorResponse_ShouldThrow()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "bootstrap-endpoint");

        var connection =
            new ScriptedProtocolConnection(
                request =>
                    request switch
                    {
                        DiscoverRequest discoverRequest =>
                            new DiscoverResponse(
                                discoverRequest.CorrelationId,
                                endpointId,
                                Array.Empty<InstrumentId>()),

                        ReadEndpointDescriptorRequest descriptorRequest =>
                            new DiscoverResponse(
                                descriptorRequest.CorrelationId,
                                endpointId,
                                Array.Empty<InstrumentId>()),

                        _ =>
                            throw new InvalidOperationException(
                                "Unexpected bootstrap request.")
                    });

        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                connection,
                endpointId);
        }

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Equal(
            2,
            connection.Requests.Count);
    }

    [Fact]
    public async Task BootstrapAsync_UnsuccessfulDescriptorResult_ShouldThrow()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "bootstrap-endpoint");

        var connection =
            CreateConnectionWithDescriptorResponse(
                endpointId,
                ProtocolResult.NotFound,
                descriptor: null);

        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                connection,
                endpointId);
        }

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task BootstrapAsync_MissingDescriptor_ShouldThrow()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "bootstrap-endpoint");

        var connection =
            CreateConnectionWithDescriptorResponse(
                endpointId,
                ProtocolResult.Success,
                descriptor: null);

        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                connection,
                endpointId);
        }

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task BootstrapAsync_DescriptorIdentityMismatch_ShouldThrow()
    {
        // Arrange
        var endpointId =
            new EndpointId(
                "bootstrap-endpoint");

        var connection =
            CreateConnectionWithDescriptorResponse(
                endpointId,
                ProtocolResult.Success,
                new EndpointDescriptor(
                    new EndpointId(
                        "different-endpoint")));

        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                connection,
                endpointId);
        }

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            Act);
    }

    [Fact]
    public async Task BootstrapAsync_CallerCancellation_ShouldPropagate()
    {
        // Arrange
        var connection =
            new ScriptedProtocolConnection(
                request =>
                    throw new InvalidOperationException(
                        "No request should be sent."));

        var bootstrapper =
            new ProtocolNativeEndpointBootstrapper();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Task Act()
        {
            return bootstrapper.BootstrapAsync(
                connection,
                expectedEndpointId: null,
                cancellationTokenSource.Token);
        }

        // Assert
        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                Act);

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);

        Assert.Empty(
            connection.Requests);
    }

    private static ScriptedProtocolConnection
        CreateSuccessfulConnection(
            EndpointId endpointId,
            EndpointDescriptor descriptor)
    {
        return CreateConnectionWithDescriptorResponse(
            endpointId,
            ProtocolResult.Success,
            descriptor);
    }

    private static ScriptedProtocolConnection
        CreateConnectionWithDescriptorResponse(
            EndpointId endpointId,
            ProtocolResult result,
            EndpointDescriptor? descriptor)
    {
        return new ScriptedProtocolConnection(
            request =>
                request switch
                {
                    DiscoverRequest discoverRequest =>
                        new DiscoverResponse(
                            discoverRequest.CorrelationId,
                            endpointId,
                            Array.Empty<InstrumentId>()),

                    ReadEndpointDescriptorRequest descriptorRequest =>
                        new ReadEndpointDescriptorResponse(
                            descriptorRequest.CorrelationId,
                            result,
                            descriptor),

                    _ =>
                        throw new InvalidOperationException(
                            "Unexpected bootstrap request.")
                });
    }

    private sealed class ScriptedProtocolConnection
        : IRuntimeProtocolConnection
    {
        private readonly Func<
            ProtocolMessage,
            ProtocolMessage> _createResponse;

        private readonly List<ProtocolMessage> _requests =
            [];

        public ScriptedProtocolConnection(
            Func<ProtocolMessage, ProtocolMessage> createResponse)
        {
            _createResponse =
                createResponse;
        }

        public IReadOnlyList<ProtocolMessage> Requests =>
            _requests;

        public Task<ProtocolMessage> SendAsync(
            ProtocolMessage request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            _requests.Add(
                request);

            return Task.FromResult(
                _createResponse(
                    request));
        }
    }
}
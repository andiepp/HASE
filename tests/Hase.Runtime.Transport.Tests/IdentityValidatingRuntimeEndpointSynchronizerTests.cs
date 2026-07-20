using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class IdentityValidatingRuntimeEndpointSynchronizerTests
{
    [Fact]
    public async Task SynchronizeAsync_MatchingIdentity_ShouldDelegate()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                "operational-endpoint");

        var connection =
            new ScriptedProtocolConnection(
                request =>
                    new DiscoverResponse(
                        request.CorrelationId,
                        runtimeEndpoint.Descriptor.Id,
                        Array.Empty<InstrumentId>()));

        var innerSynchronizer =
            new TestSynchronizer();

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            new IdentityValidatingRuntimeEndpointSynchronizer(
                innerSynchronizer);

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint);

        // Assert
        DiscoverRequest request =
            Assert.IsType<DiscoverRequest>(
                Assert.Single(
                    connection.Requests));

        Assert.NotEqual(
            CorrelationId.None,
            request.CorrelationId);

        Assert.Equal(
            1,
            innerSynchronizer.ProtocolCallCount);

        Assert.Same(
            connection,
            innerSynchronizer.ProtocolConnection);

        Assert.Same(
            runtimeEndpoint,
            innerSynchronizer.RuntimeEndpoint);
    }

    [Fact]
    public async Task SynchronizeAsync_MismatchedIdentity_ShouldNotDelegate()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                "expected-endpoint");

        var connection =
            new ScriptedProtocolConnection(
                request =>
                    new DiscoverResponse(
                        request.CorrelationId,
                        new EndpointId(
                            "different-endpoint"),
                        Array.Empty<InstrumentId>()));

        var innerSynchronizer =
            new TestSynchronizer();

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            new IdentityValidatingRuntimeEndpointSynchronizer(
                innerSynchronizer);

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
            0,
            innerSynchronizer.ProtocolCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_UnexpectedResponse_ShouldNotDelegate()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                "operational-endpoint");

        var connection =
            new ScriptedProtocolConnection(
                request =>
                    new ReadEndpointDescriptorResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        runtimeEndpoint.Descriptor));

        var innerSynchronizer =
            new TestSynchronizer();

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            new IdentityValidatingRuntimeEndpointSynchronizer(
                innerSynchronizer);

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
            0,
            innerSynchronizer.ProtocolCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_MissingIdentity_ShouldNotDelegate()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                "operational-endpoint");

        var connection =
            new ScriptedProtocolConnection(
                request =>
                    new DiscoverResponse(
                        request.CorrelationId,
                        null!,
                        Array.Empty<InstrumentId>()));

        var innerSynchronizer =
            new TestSynchronizer();

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            new IdentityValidatingRuntimeEndpointSynchronizer(
                innerSynchronizer);

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
            0,
            innerSynchronizer.ProtocolCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_CallerCancellation_ShouldPropagate()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                "operational-endpoint");

        var connection =
            new ScriptedProtocolConnection(
                request =>
                    throw new InvalidOperationException(
                        "No request should be sent."));

        var innerSynchronizer =
            new TestSynchronizer();

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            new IdentityValidatingRuntimeEndpointSynchronizer(
                innerSynchronizer);

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

        Assert.Empty(
            connection.Requests);

        Assert.Equal(
            0,
            innerSynchronizer.ProtocolCallCount);
    }

    [Fact]
    public async Task SynchronizeAsync_NullProtocolConnection_ShouldThrow()
    {
        // Arrange
        var innerSynchronizer =
            new TestSynchronizer();

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            new IdentityValidatingRuntimeEndpointSynchronizer(
                innerSynchronizer);

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                null!,
                CreateRuntimeEndpoint(
                    "operational-endpoint"));
        }

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public async Task SynchronizeAsync_NullRuntimeEndpoint_ShouldThrow()
    {
        // Arrange
        var innerSynchronizer =
            new TestSynchronizer();

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            new IdentityValidatingRuntimeEndpointSynchronizer(
                innerSynchronizer);

        var connection =
            new ScriptedProtocolConnection(
                request =>
                    throw new InvalidOperationException(
                        "No request should be sent."));

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                null!);
        }

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullSynchronizer_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new IdentityValidatingRuntimeEndpointSynchronizer(
                null!);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_TransportOnlySynchronizer_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new IdentityValidatingRuntimeEndpointSynchronizer(
                new TransportOnlySynchronizer());
        }

        // Assert
        Assert.Throws<ArgumentException>(
            Act);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        string endpointId)
    {
        return new RuntimeContext()
            .CreateEndpoint(
                new EndpointDescriptor(
                    new EndpointId(
                        endpointId)));
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
            cancellationToken.ThrowIfCancellationRequested();

            _requests.Add(
                request);

            return Task.FromResult(
                _createResponse(
                    request));
        }
    }

    private sealed class TestSynchronizer
        : IRuntimeEndpointSynchronizer,
          IRuntimeProtocolEndpointSynchronizer
    {
        public int ProtocolCallCount
        {
            get;
            private set;
        }

        public IRuntimeProtocolConnection? ProtocolConnection
        {
            get;
            private set;
        }

        public RuntimeEndpoint? RuntimeEndpoint
        {
            get;
            private set;
        }

        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The transport synchronization contract must not "
                + "be selected.");
        }

        public Task SynchronizeAsync(
            IRuntimeProtocolConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProtocolCallCount++;

            ProtocolConnection =
                connection;

            RuntimeEndpoint =
                runtimeEndpoint;

            return Task.CompletedTask;
        }
    }

    private sealed class TransportOnlySynchronizer
        : IRuntimeEndpointSynchronizer
    {
        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    ProtocolRuntimeEndpointSynchronizerProtocolConnectionValidationTests
{
    [Fact]
    public async Task SynchronizeAsync_NullProtocolConnection_ShouldThrow()
    {
        // Arrange
        IRuntimeProtocolEndpointSynchronizer synchronizer =
            CreateSynchronizer();

        // Act
        async Task Act()
        {
            await synchronizer.SynchronizeAsync(
                null!,
                null!);
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
        var connection =
            new TestRuntimeProtocolConnection();

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            CreateSynchronizer();

        // Act
        async Task Act()
        {
            await synchronizer.SynchronizeAsync(
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
            connection.SendCount);
    }

    [Fact]
    public async Task SynchronizeAsync_PreCancelled_ShouldNotSendRequest()
    {
        // Arrange
        var connection =
            new TestRuntimeProtocolConnection();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                CreateDescriptor());

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            CreateSynchronizer();

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        // Act
        async Task Act()
        {
            await synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint,
                cancellationSource.Token);
        }

        // Assert
        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                Act);

        Assert.Equal(
            cancellationSource.Token,
            exception.CancellationToken);

        Assert.Equal(
            0,
            connection.SendCount);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldReadEndpointDescriptor()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var connection =
            new TestRuntimeProtocolConnection
            {
                ResponseFactory =
                    request =>
                    {
                        ReadEndpointDescriptorRequest
                            descriptorRequest =
                                Assert.IsType<
                                    ReadEndpointDescriptorRequest>(
                                    request);

                        return new ReadEndpointDescriptorResponse(
                            descriptorRequest.CorrelationId,
                            ProtocolResult.Success,
                            descriptor);
                    }
            };

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            CreateSynchronizer();

        using var cancellationSource =
            new CancellationTokenSource();

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint,
            cancellationSource.Token);

        // Assert
        Assert.Equal(
            1,
            connection.SendCount);

        ReadEndpointDescriptorRequest request =
            Assert.IsType<ReadEndpointDescriptorRequest>(
                connection.LastRequest);

        Assert.Equal(
            descriptor.Id,
            request.EndpointId);

        Assert.False(
            request.CorrelationId.IsNone);

        Assert.Equal(
            cancellationSource.Token,
            connection.LastCancellationToken);
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

    private static EndpointDescriptor CreateDescriptor()
    {
        return new EndpointDescriptor(
            new EndpointId(
                "endpoint-01"))
        {
            Metadata =
                new EndpointMetadata
                {
                    DisplayName =
                        "Environment Endpoint",
                    Description =
                        "Endpoint used by protocol synchronizer tests."
                }
        };
    }

    private sealed class TestRuntimeProtocolConnection
        : IRuntimeProtocolConnection
    {
        public int SendCount
        {
            get;
            private set;
        }

        public ProtocolMessage? LastRequest
        {
            get;
            private set;
        }

        public CancellationToken LastCancellationToken
        {
            get;
            private set;
        }

        public Func<ProtocolMessage, ProtocolMessage>?
            ResponseFactory
        {
            get;
            init;
        }

        public Task<ProtocolMessage> SendAsync(
            ProtocolMessage request,
            CancellationToken cancellationToken = default)
        {
            SendCount++;

            LastRequest =
                request;

            LastCancellationToken =
                cancellationToken;

            ProtocolMessage response =
                ResponseFactory?.Invoke(
                    request)
                ?? throw new InvalidOperationException(
                    "No protocol response was configured.");

            return Task.FromResult(
                response);
        }
    }
}
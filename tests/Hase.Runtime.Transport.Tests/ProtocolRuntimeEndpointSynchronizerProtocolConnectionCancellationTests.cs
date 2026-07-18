using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    ProtocolRuntimeEndpointSynchronizerProtocolConnectionCancellationTests
{
    [Fact]
    public async Task SynchronizeAsync_CancelledDuringSend_ShouldCancel()
    {
        // Arrange
        var connection =
            new CancellationObservingRuntimeProtocolConnection();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            new ProtocolRuntimeEndpointSynchronizer(
                new EndpointDescriptorCompatibilityValidator());

        using var cancellationSource =
            new CancellationTokenSource();

        Task synchronizationTask =
            synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint,
                cancellationSource.Token);

        await connection.SendStarted;

        // Act
        cancellationSource.Cancel();

        // Assert
        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await synchronizationTask);

        Assert.Equal(
            cancellationSource.Token,
            exception.CancellationToken);

        Assert.Equal(
            1,
            connection.SendCount);

        Assert.Equal(
            cancellationSource.Token,
            connection.ReceivedCancellationToken);

        ReadEndpointDescriptorRequest request =
            Assert.IsType<ReadEndpointDescriptorRequest>(
                connection.ReceivedRequest);

        Assert.Equal(
            runtimeEndpoint.Descriptor.Id,
            request.EndpointId);

        Assert.False(
            request.CorrelationId.IsNone);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var descriptor =
            new EndpointDescriptor(
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

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private sealed class
        CancellationObservingRuntimeProtocolConnection
        : IRuntimeProtocolConnection
    {
        private readonly TaskCompletionSource _sendStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendStarted =>
            _sendStarted.Task;

        public int SendCount
        {
            get;
            private set;
        }

        public ProtocolMessage? ReceivedRequest
        {
            get;
            private set;
        }

        public CancellationToken ReceivedCancellationToken
        {
            get;
            private set;
        }

        public async Task<ProtocolMessage> SendAsync(
            ProtocolMessage request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            SendCount++;

            ReceivedRequest =
                request;

            ReceivedCancellationToken =
                cancellationToken;

            _sendStarted.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            throw new InvalidOperationException(
                "The cancelled protocol send unexpectedly continued.");
        }
    }
}
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorDiagnosticsExtensionsTests
{
    [Fact]
    public void GetDiagnostics_NullSupervisor_ShouldThrow()
    {
        // Arrange
        RuntimeEndpointConnectionSupervisor? supervisor =
            null;

        // Act
        void Act()
        {
            _ = supervisor!.GetDiagnostics();
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<
                ArgumentNullException>(
                    Act);

        Assert.Equal(
            "supervisor",
            exception.ParamName);
    }

    [Fact]
    public async Task GetDiagnostics_BeforeSupervision_ShouldReturnEmptyDiagnostics()
    {
        // Arrange
        await using var connectionManager =
            new TransportConnectionManager(
                new TestTransportFactory());

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                new TestRuntimeEndpointSynchronizer());

        var supervisor =
            new RuntimeEndpointConnectionSupervisor(
                coordinator,
                new TestReconnectPolicy());

        // Act
        RuntimeEndpointConnectionDiagnostics diagnostics =
            supervisor.GetDiagnostics();

        // Assert
        Assert.Equal(
            RuntimeEndpointConnectionDiagnostics.Empty,
            diagnostics);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            new EndpointDescriptor(
                new EndpointId(
                    "Endpoint")));
    }

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "A transport connection was not expected.");
        }
    }

    private sealed class TestRuntimeEndpointSynchronizer
        : IRuntimeEndpointSynchronizer
    {
        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Endpoint synchronization was not expected.");
        }
    }

    private sealed class TestReconnectPolicy
        : IRuntimeEndpointReconnectPolicy
    {
        public TimeSpan GetDelay(
            int retryAttempt)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(
                retryAttempt);

            return TimeSpan.Zero;
        }
    }
}
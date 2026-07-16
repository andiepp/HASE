using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeTransportConnectionBridgeTests
{
    [Fact]
    public void Constructor_NullConnectionManager_ShouldThrow()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        // Act
        void Act()
        {
            _ = new RuntimeTransportConnectionBridge(
                null!,
                runtimeEndpoint);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "connectionManager",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NullRuntimeEndpoint_ShouldThrow()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        var connectionManager =
            new TransportConnectionManager(
                factory);

        // Act
        void Act()
        {
            _ = new RuntimeTransportConnectionBridge(
                connectionManager,
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "runtimeEndpoint",
            exception.ParamName);
    }

    [Fact]
    public async Task Constructor_WithoutTransportConnection_ShouldMapDisconnected()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        // Act
        using var bridge =
            new RuntimeTransportConnectionBridge(
                connectionManager,
                runtimeEndpoint);

        // Assert
        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Null(
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "No transport connection is currently available.",
            runtimeEndpoint.ConnectionStatus.Detail);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var context =
            new RuntimeContext();

        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "test-endpoint"));

        return context.AddEndpoint(
            descriptor);
    }

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "No test transport connection was configured.");
        }
    }
}
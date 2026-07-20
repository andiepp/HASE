using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport;
using Hase.Transport.Tcp;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class NativeEndpointOperationalResourcesTests
{
    [Fact]
    public async Task CreateNetwork_ShouldAssembleOperationalResourceGraph()
    {
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        var reconnectPolicy =
            new DefaultRuntimeEndpointReconnectPolicy();

        NativeEndpointOperationalResources resources =
            NativeEndpointOperationalResources.CreateNetwork(
                CreateConnectionDefinition(),
                runtimeEndpoint,
                synchronizer,
                reconnectPolicy);

        Assert.Same(
            runtimeEndpoint,
            resources.Coordinator.RuntimeEndpoint);

        Assert.Same(
            resources.ConnectionManager,
            resources.Coordinator.ConnectionManager);

        Assert.IsType<
            IdentityValidatingRuntimeEndpointSynchronizer>(
                resources.Coordinator.Synchronizer);

        Assert.Collection(
            resources.ResourcesAfterSupervision,
            resource =>
                Assert.Same(
                    resources.Coordinator,
                    resource),
            resource =>
                Assert.Same(
                    resources.ConnectionManager,
                    resource));

        await resources.SupervisionLifetime.StopAsync();

        foreach (
            IAsyncDisposable resource
            in resources.ResourcesAfterSupervision)
        {
            await resource.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreateNetwork_ShouldNotConnectOrPublishEndpoint()
    {
        var runtimeContext =
            new RuntimeContext();

        RuntimeEndpoint runtimeEndpoint =
            runtimeContext.CreateEndpoint(
                CreateDescriptor());

        NativeEndpointOperationalResources resources =
            NativeEndpointOperationalResources.CreateNetwork(
                CreateConnectionDefinition(),
                runtimeEndpoint,
                new TestRuntimeEndpointSynchronizer(),
                new DefaultRuntimeEndpointReconnectPolicy());

        Assert.Null(
            resources.ConnectionManager.CurrentConnection);

        Assert.Empty(
            runtimeContext.Endpoints);

        await resources.SupervisionLifetime.StopAsync();

        foreach (
            IAsyncDisposable resource
            in resources.ResourcesAfterSupervision)
        {
            await resource.DisposeAsync();
        }
    }

    [Fact]
    public void CreateNetwork_NegativeMaximumPayloadLength_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NativeEndpointOperationalResources.CreateNetwork(
                CreateConnectionDefinition(),
                CreateRuntimeEndpoint(),
                new TestRuntimeEndpointSynchronizer(),
                new DefaultRuntimeEndpointReconnectPolicy(),
                maximumPayloadLength: -1));
    }

    [Fact]
    public void CreateNetwork_NullSynchronizer_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => NativeEndpointOperationalResources.CreateNetwork(
                CreateConnectionDefinition(),
                CreateRuntimeEndpoint(),
                null!,
                new DefaultRuntimeEndpointReconnectPolicy()));
    }

    private static NetworkEndpointConnectionDefinition
        CreateConnectionDefinition()
    {
        return NetworkEndpointConnectionDefinition.FromConfiguration(
            new TcpTransportOptions(
                "192.0.2.1",
                5000),
            new EndpointId(
                "operational-resources-endpoint"));
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        return new RuntimeContext()
            .CreateEndpoint(
                CreateDescriptor());
    }

    private static EndpointDescriptor CreateDescriptor()
    {
        return new EndpointDescriptor(
            new EndpointId(
                "operational-resources-endpoint"));
    }

    private sealed class TestRuntimeEndpointSynchronizer
        : IRuntimeEndpointSynchronizer,
          IRuntimeProtocolEndpointSynchronizer
    {
        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The operational resources must not connect during "
                + "construction.");
        }

        Task IRuntimeProtocolEndpointSynchronizer.SynchronizeAsync(
            IRuntimeProtocolConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "The operational resources must not synchronize during "
                + "construction.");
        }
    }
}
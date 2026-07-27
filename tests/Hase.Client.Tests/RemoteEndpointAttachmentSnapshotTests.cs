using Hase.Client;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Client.Tests;

public sealed class RemoteEndpointAttachmentSnapshotTests
{
    [Fact]
    public void Constructor_Values_ShouldCreateGenerationScopedSnapshot()
    {
        var generation =
            CreateGeneration();
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "endpoint-01"));
        var connectionStatus =
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready);

        var snapshot =
            new RemoteEndpointAttachmentSnapshot(
                generation,
                descriptor,
                connectionStatus);

        Assert.Equal(
            descriptor.Id,
            snapshot.EndpointId);
        Assert.Same(
            generation,
            snapshot.Generation);
        Assert.Equal(
            new RemoteEndpointAttachmentKey(
                descriptor.Id,
                generation),
            snapshot.Key);
        Assert.Same(
            descriptor,
            snapshot.Descriptor);
        Assert.Same(
            connectionStatus,
            snapshot.ConnectionStatus);
    }

    [Fact]
    public void Constructor_NullGeneration_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "generation",
            () => new RemoteEndpointAttachmentSnapshot(
                null!,
                CreateDescriptor(),
                CreateConnectionStatus()));
    }

    [Fact]
    public void Constructor_NullDescriptor_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "descriptor",
            () => new RemoteEndpointAttachmentSnapshot(
                CreateGeneration(),
                null!,
                CreateConnectionStatus()));
    }

    [Fact]
    public void Constructor_NullConnectionStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "connectionStatus",
            () => new RemoteEndpointAttachmentSnapshot(
                CreateGeneration(),
                CreateDescriptor(),
                null!));
    }

    private static RemoteEndpointAttachmentGeneration CreateGeneration()
    {
        return new RemoteEndpointAttachmentGeneration(
            Guid.Parse(
                "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8"));
    }

    private static EndpointDescriptor CreateDescriptor()
    {
        return new EndpointDescriptor(
            new EndpointId(
                "endpoint-01"));
    }

    private static RemoteEndpointConnectionStatus CreateConnectionStatus()
    {
        return new RemoteEndpointConnectionStatus(
            RemoteEndpointConnectionState.Ready);
    }
}

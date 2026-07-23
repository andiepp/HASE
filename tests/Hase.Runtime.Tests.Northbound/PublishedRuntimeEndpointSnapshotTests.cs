using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests.Northbound;

public sealed class PublishedRuntimeEndpointSnapshotTests
{
    [Fact]
    public void Constructor_StoresApplicationFacingState()
    {
        var generation =
            new RuntimeEndpointAttachmentGeneration(
                Guid.Parse(
                    "e59143bf-df9a-4d77-a759-06fdb25be3ba"));

        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "northbound-endpoint"));

        var connectionStatus =
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready,
                new DateTimeOffset(
                    2026,
                    7,
                    23,
                    10,
                    0,
                    0,
                    TimeSpan.Zero));

        var snapshot =
            new PublishedRuntimeEndpointSnapshot(
                generation,
                descriptor,
                connectionStatus);

        Assert.Equal(
            descriptor.Id,
            snapshot.EndpointId);

        Assert.Same(
            generation,
            snapshot.Generation);

        Assert.Same(
            descriptor,
            snapshot.Descriptor);

        Assert.Same(
            connectionStatus,
            snapshot.ConnectionStatus);
    }

    [Fact]
    public void Constructor_NullGeneration_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PublishedRuntimeEndpointSnapshot(
                null!,
                CreateDescriptor(),
                CreateConnectionStatus()));
    }

    [Fact]
    public void Constructor_NullDescriptor_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PublishedRuntimeEndpointSnapshot(
                CreateGeneration(),
                null!,
                CreateConnectionStatus()));
    }

    [Fact]
    public void Constructor_NullConnectionStatus_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PublishedRuntimeEndpointSnapshot(
                CreateGeneration(),
                CreateDescriptor(),
                null!));
    }

    [Fact]
    public void Snapshot_ExposesGetOnlyStateWithoutMutableRuntimeObjects()
    {
        Type snapshotType =
            typeof(PublishedRuntimeEndpointSnapshot);

        System.Reflection.PropertyInfo[] properties =
            snapshotType.GetProperties();

        Assert.All(
            properties,
            property =>
                Assert.False(
                    property.CanWrite));

        Assert.DoesNotContain(
            properties,
            property =>
                typeof(RuntimeEndpoint)
                    .IsAssignableFrom(
                        property.PropertyType));

        Assert.DoesNotContain(
            properties,
            property =>
                typeof(RuntimeInstrument)
                    .IsAssignableFrom(
                        property.PropertyType));

        Assert.DoesNotContain(
            properties,
            property =>
                typeof(RuntimeProperty)
                    .IsAssignableFrom(
                        property.PropertyType));

        Assert.DoesNotContain(
            properties,
            property =>
                typeof(RuntimeCommand)
                    .IsAssignableFrom(
                        property.PropertyType));

        Assert.DoesNotContain(
            properties,
            property =>
                typeof(RuntimeEvent)
                    .IsAssignableFrom(
                        property.PropertyType));
    }

    private static RuntimeEndpointAttachmentGeneration CreateGeneration()
    {
        return new RuntimeEndpointAttachmentGeneration(
            Guid.Parse(
                "1cdca5ef-20a4-4d9e-90ec-e36c02237352"));
    }

    private static EndpointDescriptor CreateDescriptor()
    {
        return new EndpointDescriptor(
            new EndpointId(
                "northbound-endpoint"));
    }

    private static EndpointConnectionStatus CreateConnectionStatus()
    {
        return new EndpointConnectionStatus(
            EndpointConnectionState.Ready);
    }
}
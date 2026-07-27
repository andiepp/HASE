using Hase.Client;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Tests;

public sealed class RemotePublishedPropertySnapshotTests
{
    [Fact]
    public void Constructor_Values_ShouldPreserveSnapshot()
    {
        RemotePropertyTarget target =
            CreateTarget(
                "property-01");
        PropertyDescriptor descriptor =
            CreateDescriptor(
                "property-01");
        var connectionStatus =
            new RemoteEndpointConnectionStatus(
                RemoteEndpointConnectionState.Ready);
        var currentValue =
            new RemotePropertyValue(
                RemoteValue.FromBoolean(
                    true),
                DateTimeOffset.UnixEpoch,
                RemotePropertyQuality.Good);

        var snapshot =
            new RemotePublishedPropertySnapshot(
                target,
                descriptor,
                connectionStatus,
                currentValue);

        Assert.Same(
            target,
            snapshot.Target);
        Assert.Same(
            descriptor,
            snapshot.Descriptor);
        Assert.Same(
            connectionStatus,
            snapshot.ConnectionStatus);
        Assert.Same(
            currentValue,
            snapshot.CurrentValue);
        Assert.True(
            snapshot.IsKnown);
    }

    [Fact]
    public void Constructor_UnknownValue_ShouldSucceed()
    {
        var snapshot =
            new RemotePublishedPropertySnapshot(
                CreateTarget(
                    "property-01"),
                CreateDescriptor(
                    "property-01"),
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Disconnected),
                null);

        Assert.Null(
            snapshot.CurrentValue);
        Assert.False(
            snapshot.IsKnown);
    }

    [Fact]
    public void Constructor_NullTarget_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "target",
            () => new RemotePublishedPropertySnapshot(
                null!,
                CreateDescriptor(
                    "property-01"),
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready),
                null));
    }

    [Fact]
    public void Constructor_NullDescriptor_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "descriptor",
            () => new RemotePublishedPropertySnapshot(
                CreateTarget(
                    "property-01"),
                null!,
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready),
                null));
    }

    [Fact]
    public void Constructor_NullConnectionStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "connectionStatus",
            () => new RemotePublishedPropertySnapshot(
                CreateTarget(
                    "property-01"),
                CreateDescriptor(
                    "property-01"),
                null!,
                null));
    }

    [Fact]
    public void Constructor_MismatchedDescriptorIdentity_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            "descriptor",
            () => new RemotePublishedPropertySnapshot(
                CreateTarget(
                    "property-01"),
                CreateDescriptor(
                    "property-02"),
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready),
                null));
    }

    private static RemotePropertyTarget CreateTarget(
        string propertyId)
    {
        return new RemotePropertyTarget(
            new RemoteEndpointAttachmentKey(
                new EndpointId(
                    "endpoint-01"),
                new RemoteEndpointAttachmentGeneration(
                    Guid.Parse(
                        "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8"))),
            new InstrumentId(
                "instrument-01"),
            new PropertyId(
                propertyId));
    }

    private static PropertyDescriptor CreateDescriptor(
        string propertyId)
    {
        return new PropertyDescriptor(
            new PropertyId(
                propertyId),
            new DescriptorPath(
                "Property"),
            "Property",
            new BooleanDataDescriptor());
    }
}

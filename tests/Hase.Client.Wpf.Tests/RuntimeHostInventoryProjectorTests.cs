using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Wpf.Tests;

public sealed class RuntimeHostInventoryProjectorTests
{
    [Fact]
    public void Project_NullState_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "state",
            () =>
                RuntimeHostInventoryProjector.Project(
                    null!));
    }

    [Fact]
    public void Project_EmptyState_ShouldReturnEmptyInventory()
    {
        Assert.Empty(
            RuntimeHostInventoryProjector.Project(
                RemoteObservationState.Empty));
    }

    [Fact]
    public void Project_InitializedState_ShouldPreserveIdentityAndInstruments()
    {
        Guid generationValue =
            Guid.Parse(
                "20d9ef89-267a-4de4-9ed1-04848635e6ab");
        var instrument =
            new InstrumentDescriptor(
                new InstrumentId(
                    "sensor-01"),
                "Environment Sensor",
                new InstrumentKind(
                    "Sensor"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            new PropertyDescriptor(
                                new PropertyId(
                                    "temperature"),
                                DescriptorPath.Parse(
                                    "Environment.Temperature"),
                                "Temperature",
                                new StringDataDescriptor())
                            {
                                AccessMode =
                                    PropertyAccessMode.Read
                            }
                        ])
            };
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "endpoint-01"),
                [instrument])
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Test Endpoint"
                    }
            };
        var attachment =
            new RemoteEndpointAttachmentSnapshot(
                new RemoteEndpointAttachmentGeneration(
                    generationValue),
                descriptor,
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Ready));
        RemoteObservationState state =
            new RemoteObservationReducer().Initialize(
                RemoteObservationState.Empty,
                new RemoteObservationInitialSnapshot(
                    new RemoteRuntimeHostSnapshot(
                        new RemoteRuntimeHostId(
                            "runtime-01"),
                        RuntimeHostClientApiVersion.Current,
                        [attachment]),
                    new RemoteObservationSequence(
                        1)));

        EndpointInventoryItemViewModel endpoint =
            Assert.Single(
                RuntimeHostInventoryProjector.Project(
                    state));

        Assert.Equal(
            "endpoint-01",
            endpoint.EndpointId);
        Assert.Equal(
            generationValue.ToString(
                "D"),
            endpoint.AttachmentGeneration);
        Assert.Equal(
            "Test Endpoint",
            endpoint.DisplayName);
        Assert.Equal(
            "Ready",
            endpoint.ConnectionState);
        InstrumentInventoryItemViewModel projectedInstrument =
            Assert.Single(
                endpoint.Instruments);
        Assert.Equal(
            "sensor-01",
            projectedInstrument.InstrumentId);
        Assert.Equal(
            "Environment Sensor",
            projectedInstrument.Name);
        PropertyInventoryItemViewModel property =
            Assert.Single(
                projectedInstrument.Properties);
        Assert.Equal(
            "temperature",
            property.PropertyId);
        Assert.Equal(
            "Environment.Temperature",
            property.Path);
        Assert.Equal(
            "Read",
            property.AccessMode);
        Assert.Equal(
            "String",
            property.DataType);
        Assert.Equal(
            "No cached value",
            property.Value);
        Assert.Equal(
            endpoint.Key,
            property.Target.Attachment);
    }
}

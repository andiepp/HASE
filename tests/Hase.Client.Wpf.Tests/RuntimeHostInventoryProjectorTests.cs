using Hase.Client.Wpf.Services;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Commands;
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
                        ],
                        commands:
                        [
                            new CommandDescriptor(
                                DescriptorPath.Parse(
                                    "Controller.Reset"),
                                "Reset"),
                            new CommandDescriptor(
                                DescriptorPath.Parse(
                                    "Controller.Send"),
                                "Send",
                                new CommandArgumentDescriptor(
                                    "Payload",
                                    new ByteArrayDataDescriptor())
                                {
                                    Description =
                                        "Opaque payload"
                                })
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
        Assert.True(
            endpoint.IsReady);
        Assert.False(
            endpoint.IsStale);
        InstrumentInventoryItemViewModel projectedInstrument =
            Assert.Single(
                endpoint.Instruments);
        Assert.Equal(
            "sensor-01",
            projectedInstrument.InstrumentId);
        Assert.Equal(
            "Environment Sensor",
            projectedInstrument.Name);
        Assert.Collection(
            projectedInstrument.Commands,
            command =>
            {
                Assert.Equal(
                    "Reset",
                    command.DisplayName);
                Assert.False(
                    command.RequiresArgument);
                Assert.Null(
                    command.ArgumentDisplayName);
                Assert.Null(
                    command.ArgumentDataType);
                Assert.True(
                    command.CanExecute);
            },
            command =>
            {
                Assert.Equal(
                    "Send",
                    command.DisplayName);
                Assert.True(
                    command.RequiresArgument);
                Assert.Equal(
                    "Payload",
                    command.ArgumentDisplayName);
                Assert.Equal(
                    "Opaque payload",
                    command.ArgumentDescription);
                Assert.Equal(
                    "ByteArray",
                    command.ArgumentDataType);
                Assert.False(
                    command.CanExecute);
            });
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
        Assert.False(
            property.IsStale);
        Assert.True(
            property.SupportsRead);
        Assert.True(
            property.CanRead);
        Assert.False(
            property.SupportsBooleanWrite);
        Assert.False(
            property.CanWrite);
    }

    [Fact]
    public void Project_UnavailableEndpoint_ShouldMarkInventoryStale()
    {
        var attachment =
            new RemoteEndpointAttachmentSnapshot(
                new RemoteEndpointAttachmentGeneration(
                    Guid.Parse(
                        "30d9ef89-267a-4de4-9ed1-04848635e6ab")),
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-02")),
                new RemoteEndpointConnectionStatus(
                    RemoteEndpointConnectionState.Reconnecting));
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

        Assert.False(
            endpoint.IsReady);
        Assert.True(
            endpoint.IsStale);
        Assert.Equal(
            "Reconnecting",
            endpoint.ConnectionState);
    }

    [Fact]
    public void Project_ObservationRefresh_ShouldPreserveRequestedBooleanValue()
    {
        var instrument =
            new InstrumentDescriptor(
                new InstrumentId(
                    "controller-01"),
                "Controller",
                new InstrumentKind(
                    "Controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            new PropertyDescriptor(
                                new PropertyId(
                                    "led-enabled"),
                                DescriptorPath.Parse(
                                    "Controller.LedEnabled"),
                                "LED Enabled",
                                new BooleanDataDescriptor())
                            {
                                AccessMode =
                                    PropertyAccessMode.ReadWrite
                            }
                        ])
            };
        var attachment =
            new RemoteEndpointAttachmentSnapshot(
                new RemoteEndpointAttachmentGeneration(
                    Guid.Parse(
                        "40d9ef89-267a-4de4-9ed1-04848635e6ab")),
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-03"),
                    [instrument]),
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
        var target =
            new RemotePropertyTarget(
                attachment.Key,
                instrument.Id,
                new PropertyId(
                    "led-enabled"));

        PropertyInventoryItemViewModel property =
            Assert.Single(
                Assert.Single(
                    RuntimeHostInventoryProjector.Project(
                        state,
                        confirmedReads:
                            null,
                        requestedBooleanValues:
                            new Dictionary<
                                RemotePropertyTarget,
                                bool>
                            {
                                [target] =
                                    true
                            }))
                    .Instruments)
                .Properties
                .Single();

        Assert.True(
            property.SupportsBooleanWrite);
        Assert.True(
            property.CanWrite);
        Assert.True(
            property.RequestedBooleanValue);
    }
}

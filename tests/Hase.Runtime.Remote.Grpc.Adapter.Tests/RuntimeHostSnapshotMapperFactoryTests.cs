using Google.Protobuf.WellKnownTypes;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostSnapshotMapperFactoryTests
{
    [Fact]
    public void Create_ComposedMapper_ShouldMapCompleteRuntimeHostSnapshot()
    {
        DateTimeOffset changedAtUtc =
            new(
                2026,
                7,
                25,
                13,
                0,
                0,
                TimeSpan.Zero);

        var property =
            new PropertyDescriptor(
                new PropertyId(
                    "temperature"),
                new DescriptorPath(
                    "Environment",
                    "Temperature"),
                "Temperature",
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        -40.0,
                        85.0),
                    new Resolution(
                        0.01)))
            {
                Description =
                    "Measured temperature.",
                AccessMode =
                    PropertyAccessMode.Read
            };

        var command =
            new CommandDescriptor(
                new DescriptorPath(
                    "Acquisition",
                    "Start"),
                "Start acquisition")
            {
                Description =
                    "Starts acquisition."
            };

        var eventDescriptor =
            new EventDescriptor(
                new DescriptorPath(
                    "Input",
                    "Pressed"),
                "Input pressed")
            {
                Description =
                    "A physical input was pressed."
            };

        var instrument =
            new InstrumentDescriptor(
                new InstrumentId(
                    "environment-sensor"),
                "Environment Sensor",
                new InstrumentKind(
                    "sensor"))
            {
                Metadata =
                    new InstrumentMetadata
                    {
                        Manufacturer =
                            "HASE",
                        Model =
                            "Validation Sensor",
                        SerialNumber =
                            "SN-1",
                        FirmwareVersion =
                            "1.2.3",
                        HardwareRevision =
                            "A",
                        Description =
                            "Validation instrument."
                    },
                Interface =
                    new InstrumentInterface(
                        new[] { property },
                        new[] { command },
                        new[] { eventDescriptor })
            };

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "endpoint-1"),
                new[] { instrument })
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Endpoint 1",
                        Description =
                            "Validation endpoint."
                    }
            };

        var endpointSnapshot =
            new Northbound.PublishedRuntimeEndpointSnapshot(
                new Northbound.RuntimeEndpointAttachmentGeneration(
                    Guid.Parse(
                        "9f4c8c0d-41a8-4dd8-b3bb-0af76a629e04")),
                endpointDescriptor,
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready,
                    changedAtUtc,
                    "Endpoint synchronized."));

        RuntimeHostSnapshotMapper mapper =
            RuntimeHostSnapshotMapperFactory.Create();

        GrpcV1.GetSnapshotResponse result =
            mapper.Map(
                new Northbound.PublishedRuntimeHostSnapshot(
                    new Northbound.RuntimeHostId(
                        "runtime-host-1"),
                    Northbound.RuntimeHostApiVersion.Current,
                    new[] { endpointSnapshot }));

        Assert.Equal(
            "runtime-host-1",
            result.RuntimeHostId);
        Assert.Equal(
            1U,
            result.ApiVersion.Major);
        Assert.Equal(
            0U,
            result.ApiVersion.Minor);

        GrpcV1.PublishedRuntimeEndpointSnapshot mappedEndpoint =
            Assert.Single(
                result.Endpoints);

        Assert.Equal(
            "endpoint-1",
            mappedEndpoint.EndpointId);
        Assert.Equal(
            "9f4c8c0d-41a8-4dd8-b3bb-0af76a629e04",
            mappedEndpoint.AttachmentGeneration);
        Assert.Equal(
            "Endpoint 1",
            mappedEndpoint.Descriptor_.DisplayName);
        Assert.Equal(
            "Validation endpoint.",
            mappedEndpoint.Descriptor_.Description);
        Assert.Equal(
            4,
            (int)mappedEndpoint.ConnectionStatus.State);
        Assert.Equal(
            Timestamp.FromDateTimeOffset(
                changedAtUtc),
            mappedEndpoint.ConnectionStatus.ChangedAtUtc);
        Assert.Equal(
            "Endpoint synchronized.",
            mappedEndpoint.ConnectionStatus.Detail);

        GrpcV1.InstrumentDescriptor mappedInstrument =
            Assert.Single(
                mappedEndpoint.Descriptor_.Instruments);

        Assert.Equal(
            "environment-sensor",
            mappedInstrument.InstrumentId);
        Assert.Equal(
            "Environment Sensor",
            mappedInstrument.Name);
        Assert.Equal(
            "sensor",
            mappedInstrument.Kind);
        Assert.Equal("HASE", mappedInstrument.Manufacturer);
        Assert.Equal("Validation Sensor", mappedInstrument.Model);
        Assert.Equal("SN-1", mappedInstrument.SerialNumber);
        Assert.Equal("1.2.3", mappedInstrument.FirmwareVersion);
        Assert.Equal("A", mappedInstrument.HardwareRevision);
        Assert.Equal("Validation instrument.", mappedInstrument.Description);

        GrpcV1.PropertyDescriptor mappedProperty =
            Assert.Single(
                mappedInstrument.Properties);

        Assert.Equal(
            "temperature",
            mappedProperty.PropertyId);
        Assert.Equal(
            new[]
            {
                "Environment",
                "Temperature"
            },
            mappedProperty.PathSegments.ToArray());
        Assert.Equal(
            1,
            (int)mappedProperty.AccessMode);
        Assert.Equal(
            GrpcV1.DataDescriptor.KindOneofCase.Numeric,
            mappedProperty.Data.KindCase);
        Assert.Equal(
            "temperature",
            mappedProperty.Data.Numeric.Quantity.Id);
        Assert.Equal(
            "celsius",
            mappedProperty.Data.Numeric.NativeUnit.Id);
        Assert.Equal(
            "temperature",
            mappedProperty.Data.Numeric.NativeUnit.Quantity.Id);
        Assert.Equal(
            -40.0,
            mappedProperty.Data.Numeric.Range.Minimum);
        Assert.Equal(
            85.0,
            mappedProperty.Data.Numeric.Range.Maximum);
        Assert.Equal(
            0.01,
            mappedProperty.Data.Numeric.Resolution.Value);

        GrpcV1.CommandDescriptor mappedCommand =
            Assert.Single(
                mappedInstrument.Commands);

        Assert.Equal(
            new[]
            {
                "Acquisition",
                "Start"
            },
            mappedCommand.PathSegments.ToArray());
        Assert.Equal(
            "Start acquisition",
            mappedCommand.DisplayName);
        Assert.Equal(
            "Starts acquisition.",
            mappedCommand.Description);

        GrpcV1.EventDescriptor mappedEvent =
            Assert.Single(
                mappedInstrument.Events);

        Assert.Equal(
            new[]
            {
                "Input",
                "Pressed"
            },
            mappedEvent.PathSegments.ToArray());
        Assert.Equal(
            "Input pressed",
            mappedEvent.DisplayName);
        Assert.Equal(
            "A physical input was pressed.",
            mappedEvent.Description);
    }
}
